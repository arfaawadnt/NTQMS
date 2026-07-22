import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { RiskApiService } from '../../core/api/risk-api.service';
import { AddMitigationRequest, AssessRiskRequest, RiskDetail, RiskListItem } from '../../core/models';

/**
 * Signal-based facade for the Risk register. Exposes the register list and the
 * selected risk (with mitigation actions and residual assessment), and drives
 * the assess → mitigate → residual → close lifecycle.
 */
@Injectable({ providedIn: 'root' })
export class RiskFacade {
  private readonly api = inject(RiskApiService);

  private readonly _list = signal<RiskListItem[]>([]);
  private readonly _selected = signal<RiskDetail | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly list = this._list.asReadonly();
  readonly selected = this._selected.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  async loadList(status?: string): Promise<void> {
    await this.run(async () => this._list.set(await firstValueFrom(this.api.list(status))));
  }

  async loadDetail(id: string): Promise<void> {
    await this.run(async () => this._selected.set(await firstValueFrom(this.api.getById(id))));
  }

  async assess(request: AssessRiskRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.assess(request))).id);
  }

  async addMitigation(id: string, request: AddMitigationRequest): Promise<void> {
    await this.mutate(id, () => this.api.addMitigation(id, request));
  }

  async completeMitigation(id: string, actionId: string): Promise<void> {
    await this.mutate(id, () => this.api.completeMitigation(id, actionId));
  }

  async recordResidual(id: string, likelihood: number, impact: number): Promise<void> {
    await this.mutate(id, () => this.api.recordResidual(id, { likelihood, impact }));
  }

  async close(id: string): Promise<void> {
    await this.mutate(id, () => this.api.close(id));
  }

  private async mutate(id: string, call: () => Observable<unknown>): Promise<void> {
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
