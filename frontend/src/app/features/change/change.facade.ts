import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { ChangeApiService } from '../../core/api/change-api.service';
import { RiskApiService } from '../../core/api/risk-api.service';
import { ChangeDetail, ChangeListItem, ProposeChangeRequest, RiskListItem } from '../../core/models';

/**
 * Signal-based facade for Change Control. Drives the propose → link-risk →
 * approve/reject → close lifecycle and exposes the open-risk options used to
 * satisfy the "no approval without a linked risk" invariant.
 */
@Injectable({ providedIn: 'root' })
export class ChangeFacade {
  private readonly api = inject(ChangeApiService);
  private readonly risks = inject(RiskApiService);

  private readonly _list = signal<ChangeListItem[]>([]);
  private readonly _total = signal(0);
  private readonly _hasMore = signal(false);
  private readonly _selected = signal<ChangeDetail | null>(null);
  private readonly _riskOptions = signal<RiskListItem[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly list = this._list.asReadonly();
  /** Total matching records on the server (pagination envelope, API-004). */
  readonly total = this._total.asReadonly();
  /** True when more pages exist beyond the loaded slice. */
  readonly hasMore = this._hasMore.asReadonly();
  readonly selected = this._selected.asReadonly();
  readonly riskOptions = this._riskOptions.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  async loadList(status?: string): Promise<void> {
    await this.run(async () => {
      const page = await firstValueFrom(this.api.list(status));
      this._list.set(page.items);
      this._total.set(page.total);
      this._hasMore.set(page.hasMore);
    });
  }

  async loadDetail(id: string): Promise<void> {
    await this.run(async () => this._selected.set(await firstValueFrom(this.api.getById(id))));
  }

  /** Loads open (non-closed) risks for the risk-link picker. */
  async loadRiskOptions(): Promise<void> {
    await this.run(async () => this._riskOptions.set((await firstValueFrom(this.risks.list())).items));
  }

  async propose(request: ProposeChangeRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.propose(request))).id);
  }

  async linkRisk(id: string, riskItemId: string): Promise<void> {
    await this.mutate(id, () => this.api.linkRisk(id, { riskItemId }));
  }

  async approve(id: string): Promise<void> {
    await this.mutate(id, () => this.api.approve(id));
  }

  async reject(id: string, reason: string): Promise<void> {
    await this.mutate(id, () => this.api.reject(id, { reason }));
  }

  async close(id: string, implementationNotes: string): Promise<void> {
    await this.mutate(id, () => this.api.close(id, { implementationNotes }));
  }

  async review(id: string, effective: boolean, notes: string): Promise<void> {
    await this.mutate(id, () => this.api.review(id, { effective, notes }));
  }

  private async mutate(id: string, call: () => Observable<void>): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(call());
      this._selected.set(await firstValueFrom(this.api.getById(id)));
    });
  }

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
