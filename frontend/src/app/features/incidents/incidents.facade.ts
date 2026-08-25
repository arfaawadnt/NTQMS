import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { IncidentsApiService } from '../../core/api/incidents-api.service';
import {
  AddContributingFactorRequest, AddTimelineEntryRequest, AnonymousIncidentReceipt, CloseIncidentRequest,
  DeclareSentinelRequest, IncidentDetail, IncidentListItem, RecordInvestigationSummaryRequest,
  RejectIncidentRequest, ReportAnonymousIncidentRequest, ReportIncidentRequest, SignatureRecord,
  StartInvestigationRequest, TriageIncidentRequest,
} from '../../core/models';

/**
 * Signal-based facade (state store) for the Incident & Occurrence Reporting module.
 * Components stay presentational: they read the exposed signals and call intent
 * methods; all API orchestration, loading/error state and refresh-after-write live
 * here. Reporting volume is a safety-culture indicator, so the register is designed
 * for fast entry — the facade keeps that path free of ceremony.
 */
@Injectable({ providedIn: 'root' })
export class IncidentsFacade {
  private readonly api = inject(IncidentsApiService);

  private readonly _list = signal<IncidentListItem[]>([]);
  private readonly _total = signal(0);
  private readonly _hasMore = signal(false);
  private readonly _selected = signal<IncidentDetail | null>(null);
  private readonly _signatures = signal<SignatureRecord[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal('');
  /** 1-based page of the last fetched slice (R-3 load-more pager). */
  private readonly _page = signal(1);
  /** Filters of the last loadList, reused verbatim by loadMore. */
  private lastStatus?: string;
  private lastSearch?: string;
  private lastCategory?: string;
  private lastSentinelOnly = false;

  /** Current incident list (filtered server-side). */
  readonly list = this._list.asReadonly();
  /** Total matching records on the server (pagination envelope, API-004). */
  readonly total = this._total.asReadonly();
  /** True when more pages exist beyond the loaded slice. */
  readonly hasMore = this._hasMore.asReadonly();
  /** Currently loaded detail, or null. */
  readonly selected = this._selected.asReadonly();
  /** Part 11 §11.50 signature manifest for the loaded incident. */
  readonly signatures = this._signatures.asReadonly();
  /** True while any request is in flight. */
  readonly loading = this._loading.asReadonly();
  /** Last user-facing error message, or empty. */
  readonly error = this._error.asReadonly();

  /** Open (non-terminal) incident count, for dashboards. */
  readonly openCount = computed(() =>
    this._list().filter((i) => i.status !== 'Closed' && i.status !== 'Rejected').length);
  /** Sentinel-flagged count, for dashboards. */
  readonly sentinelCount = computed(() => this._list().filter((i) => i.isSentinel).length);

  /** Loads the first page under the given server-side filters (replaces the list). */
  async loadList(status?: string, search?: string, category?: string, sentinelOnly = false): Promise<void> {
    this.lastStatus = status;
    this.lastSearch = search;
    this.lastCategory = category;
    this.lastSentinelOnly = sentinelOnly;
    await this.run(async () => {
      const page = await firstValueFrom(this.api.list(status, search, category, sentinelOnly));
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
      const page = await firstValueFrom(
        this.api.list(this.lastStatus, this.lastSearch, this.lastCategory, this.lastSentinelOnly, next));
      this._page.set(next);
      this._list.update((items) => [...items, ...page.items]);
      this._total.set(page.total);
      this._hasMore.set(page.hasMore);
    });
  }

  /** Loads a single incident into `selected`, with its Part 11 signature manifest. */
  async loadDetail(id: string): Promise<void> {
    await this.run(async () => {
      this._selected.set(await firstValueFrom(this.api.getById(id)));
      this._signatures.set(await firstValueFrom(this.api.signatures(id)));
    });
  }

  /** Reports a new attributed incident; returns its id on success or null on failure. */
  async report(request: ReportIncidentRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.report(request))).id);
  }

  /** Reports anonymously; returns the one-time receipt (with follow-up reference) or null. */
  async reportAnonymous(request: ReportAnonymousIncidentRequest): Promise<AnonymousIncidentReceipt | null> {
    return this.run(async () => await firstValueFrom(this.api.reportAnonymous(request)));
  }

  async triage(id: string, r: TriageIncidentRequest): Promise<void> { await this.mutate(id, () => this.api.triage(id, r)); }
  async reject(id: string, r: RejectIncidentRequest): Promise<void> { await this.mutate(id, () => this.api.reject(id, r)); }
  async startInvestigation(id: string, r: StartInvestigationRequest): Promise<void> {
    await this.mutate(id, () => this.api.startInvestigation(id, r));
  }
  async addContributingFactor(id: string, r: AddContributingFactorRequest): Promise<void> {
    await this.mutate(id, () => this.api.addContributingFactor(id, r));
  }
  async addTimelineEntry(id: string, r: AddTimelineEntryRequest): Promise<void> {
    await this.mutate(id, () => this.api.addTimelineEntry(id, r));
  }
  async recordInvestigationSummary(id: string, r: RecordInvestigationSummaryRequest): Promise<void> {
    await this.mutate(id, () => this.api.recordInvestigationSummary(id, r));
  }
  async submitForReview(id: string): Promise<void> { await this.mutate(id, () => this.api.submitForReview(id)); }

  /** Closes the incident (signed); refreshes the §11.50 manifest on success. */
  async close(id: string, r: CloseIncidentRequest): Promise<void> {
    await this.mutate(id, () => this.api.close(id, r));
    if (this._error() === '') { await this.loadSignatures(id); }
  }

  /** Declares a sentinel event (signed); refreshes the §11.50 manifest on success. */
  async declareSentinel(id: string, r: DeclareSentinelRequest): Promise<void> {
    await this.mutate(id, () => this.api.declareSentinel(id, r));
    if (this._error() === '') { await this.loadSignatures(id); }
  }

  /** Raises a CAPA/Nonconformance from the incident and refreshes the record so the link shows. */
  async raiseCapa(id: string): Promise<void> { await this.mutate(id, () => this.api.raiseCapa(id)); }

  /** Refreshes just the signature manifest for the loaded incident. */
  private async loadSignatures(id: string): Promise<void> {
    const sigs = await this.run(() => firstValueFrom(this.api.signatures(id)));
    if (sigs) { this._signatures.set(sigs); }
  }

  /** Runs a state-changing call then reloads the detail so the UI reflects the new state. */
  private async mutate<T>(id: string, call: () => Observable<T>): Promise<void> {
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
