import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { NcApiService } from '../../core/api/nc-api.service';
import {
  ConfirmEffectivenessRequest, NcDetail, NcListItem, PlanCapaActionRequest,
  RaiseNcRequest, RecordRcaRequest, RejectNcRequest, SignatureRecord, TriageNcRequest, VerifyNcRequest,
} from '../../core/models';

/**
 * Signal-based facade (state store) for the NC/CAPA module. Components stay
 * presentational: they read the exposed signals and call intent methods; all
 * API orchestration, loading/error state and refresh-after-write live here.
 */
@Injectable({ providedIn: 'root' })
export class NcFacade {
  private readonly api = inject(NcApiService);

  private readonly _list = signal<NcListItem[]>([]);
  private readonly _total = signal(0);
  private readonly _hasMore = signal(false);
  private readonly _selected = signal<NcDetail | null>(null);
  private readonly _signatures = signal<SignatureRecord[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal('');
  /** 1-based page of the last fetched slice (R-3 load-more pager). */
  private readonly _page = signal(1);
  /** Filters of the last loadList, reused verbatim by loadMore. */
  private lastStatus?: string;
  private lastSearch?: string;

  /** Current nonconformance list (filtered server-side). */
  readonly list = this._list.asReadonly();
  /** Total matching records on the server (pagination envelope, API-004). */
  readonly total = this._total.asReadonly();
  /** True when more pages exist beyond the loaded slice. */
  readonly hasMore = this._hasMore.asReadonly();
  /** Currently loaded detail, or null. */
  readonly selected = this._selected.asReadonly();
  /** Part 11 §11.50 signature manifest for the loaded nonconformance. */
  readonly signatures = this._signatures.asReadonly();
  /** True while any request is in flight. */
  readonly loading = this._loading.asReadonly();
  /** Last user-facing error message, or empty. */
  readonly error = this._error.asReadonly();

  /** Open (non-terminal) nonconformance count, for dashboards. */
  readonly openCount = computed(() =>
    this._list().filter((n) => n.status !== 'Closed' && n.status !== 'Rejected').length);
  /** High-risk (RPN > 12) count, for dashboards. */
  readonly highRpnCount = computed(() => this._list().filter((n) => n.rpn > 12).length);

  /** Loads the first page, optionally filtered by status/search text (replaces the list). */
  async loadList(status?: string, search?: string): Promise<void> {
    this.lastStatus = status;
    this.lastSearch = search;
    await this.run(async () => {
      const page = await firstValueFrom(this.api.list(status, search));
      this._page.set(1);
      this._list.set(page.items);
      this._total.set(page.total);
      this._hasMore.set(page.hasMore);
    });
  }

  /** Appends the next page under the current filters (R-3); no-op while loading or exhausted. */
  async loadMore(): Promise<void> {
    if (this._loading() || !this._hasMore()) { return; }
    await this.run(async () => {
      const next = this._page() + 1;
      const page = await firstValueFrom(this.api.list(this.lastStatus, this.lastSearch, next));
      this._page.set(next);
      this._list.update((items) => [...items, ...page.items]);
      this._total.set(page.total);
      this._hasMore.set(page.hasMore);
    });
  }

  /** Loads a single nonconformance into `selected`, with its Part 11 signature manifest. */
  async loadDetail(id: string): Promise<void> {
    await this.run(async () => {
      this._selected.set(await firstValueFrom(this.api.getById(id)));
      this._signatures.set(await firstValueFrom(this.api.signatures(id)));
    });
  }

  /** Raises a new draft nonconformance; returns its id on success or null on failure. */
  async raise(request: RaiseNcRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.raise(request))).id);
  }

  async submit(id: string): Promise<void> { await this.mutate(id, () => this.api.submit(id)); }
  async triage(id: string, r: TriageNcRequest): Promise<void> { await this.mutate(id, () => this.api.triage(id, r)); }
  async reject(id: string, r: RejectNcRequest): Promise<void> { await this.mutate(id, () => this.api.reject(id, r)); }
  async recordRca(id: string, r: RecordRcaRequest): Promise<void> { await this.mutate(id, () => this.api.recordRca(id, r)); }
  async planAction(id: string, r: PlanCapaActionRequest): Promise<void> { await this.mutate(id, () => this.api.planAction(id, r)); }
  async completeAction(id: string, actionId: string): Promise<void> { await this.mutate(id, () => this.api.completeAction(id, actionId)); }
  async submitForVerification(id: string): Promise<void> { await this.mutate(id, () => this.api.submitForVerification(id)); }
  async verify(id: string, r: VerifyNcRequest): Promise<void> {
    await this.mutate(id, () => this.api.verify(id, r));
    // A successful verification mints a §11.50 signature; refresh the manifest.
    if (this._error() === '') { await this.loadSignatures(id); }
  }

  /** Refreshes just the signature manifest for the loaded nonconformance. */
  private async loadSignatures(id: string): Promise<void> {
    const sigs = await this.run(() => firstValueFrom(this.api.signatures(id)));
    if (sigs) { this._signatures.set(sigs); }
  }
  async confirmEffectiveness(id: string, r: ConfirmEffectivenessRequest): Promise<void> {
    await this.mutate(id, () => this.api.confirmEffectiveness(id, r));
    // A successful (effective) close mints a §11.50 signature; refresh the manifest.
    if (this._error() === '') { await this.loadSignatures(id); }
  }

  /** Runs a state-changing call then reloads the detail so the UI reflects the new state. */
  private async mutate<T>(id: string, call: () => import('rxjs').Observable<T>): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(call());
      this._selected.set(await firstValueFrom(this.api.getById(id)));
    });
  }

  /** Shared loading/error wrapper; returns the operation result or null on error. */
  private async run<T>(operation: () => Promise<T>): Promise<T | null> {
    this._loading.set(true);
    this._error.set('');
    try {
      return await operation();
    } catch (err) {
      this._error.set(this.describe(err));
      return null;
    } finally {
      this._loading.set(false);
    }
  }

  private describe(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      return (err.error as { title?: string } | null)?.title ?? `Request failed (${err.status}).`;
    }
    return 'Unexpected error.';
  }
}
