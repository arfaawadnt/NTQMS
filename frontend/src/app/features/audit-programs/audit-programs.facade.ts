import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { AuditProgramsApiService } from '../../core/api/audit-programs-api.service';
import {
  AddPlannedAuditRequest, AuditProgramDetail, AuditProgramListItem, CompletePlannedAuditRequest,
  CreateAuditProgramRequest, LinkScheduledAuditRequest,
} from '../../core/models';

/**
 * Signal-based facade for the annual audit programme (HQMS M05). Holds the register and
 * the loaded programme with its coverage, refreshing the detail after every write so the
 * coverage figure stays live.
 */
@Injectable({ providedIn: 'root' })
export class AuditProgramsFacade {
  private readonly api = inject(AuditProgramsApiService);

  private readonly _list = signal<AuditProgramListItem[]>([]);
  private readonly _selected = signal<AuditProgramDetail | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly list = this._list.asReadonly();
  readonly selected = this._selected.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  readonly activeCount = computed(() => this._list().filter((p) => p.status === 'Active').length);

  async loadList(status?: string): Promise<void> {
    await this.run(async () => this._list.set(await firstValueFrom(this.api.list(status))));
  }

  async loadDetail(id: string): Promise<void> {
    await this.run(async () => this._selected.set(await firstValueFrom(this.api.getById(id))));
  }

  async create(request: CreateAuditProgramRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.create(request))).id);
  }

  async addPlanned(id: string, r: AddPlannedAuditRequest): Promise<void> { await this.refresh(id, () => this.api.addPlanned(id, r)); }
  async activate(id: string): Promise<void> { await this.refresh(id, () => this.api.activate(id)); }
  async schedule(id: string, plannedId: string, r: LinkScheduledAuditRequest): Promise<void> { await this.refresh(id, () => this.api.schedule(id, plannedId, r)); }
  async complete(id: string, plannedId: string, r: CompletePlannedAuditRequest): Promise<void> { await this.refresh(id, () => this.api.complete(id, plannedId, r)); }
  async close(id: string): Promise<void> { await this.refresh(id, () => this.api.close(id)); }

  private async refresh<T>(id: string, call: () => Observable<T>): Promise<void> {
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
