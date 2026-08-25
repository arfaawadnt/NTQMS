import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { StandardsApiService } from '../../core/api/standards-api.service';
import {
  AddStandardElementRequest, AssessElementRequest, DefineStandardSetRequest, GapItem, LinkEvidenceRequest,
  ReadinessDashboard, StandardSetDetail, StandardSetListItem,
} from '../../core/models';

/**
 * Signal-based facade for the Accreditation &amp; Standards Compliance module (HQMS M07).
 * Holds the register, the loaded set with its elements, and the derived readiness and
 * gap-analysis views, refreshing them together after every write so compliance status
 * on screen is always the live figure.
 */
@Injectable({ providedIn: 'root' })
export class StandardsFacade {
  private readonly api = inject(StandardsApiService);

  private readonly _list = signal<StandardSetListItem[]>([]);
  private readonly _selected = signal<StandardSetDetail | null>(null);
  private readonly _readiness = signal<ReadinessDashboard | null>(null);
  private readonly _gaps = signal<GapItem[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly list = this._list.asReadonly();
  readonly selected = this._selected.asReadonly();
  readonly readiness = this._readiness.asReadonly();
  readonly gaps = this._gaps.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  readonly activeCount = computed(() => this._list().filter((s) => s.status === 'Active').length);

  async loadList(status?: string): Promise<void> {
    await this.run(async () => this._list.set(await firstValueFrom(this.api.list(status))));
  }

  /** Loads a set with its elements, readiness dashboard and gap analysis together. */
  async loadDetail(id: string): Promise<void> {
    await this.run(async () => {
      this._selected.set(await firstValueFrom(this.api.getById(id)));
      this._readiness.set(await firstValueFrom(this.api.readiness(id)));
      this._gaps.set(await firstValueFrom(this.api.gapAnalysis(id)));
    });
  }

  async define(request: DefineStandardSetRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.define(request))).id);
  }

  async addElement(id: string, r: AddStandardElementRequest): Promise<void> { await this.refresh(id, () => this.api.addElement(id, r)); }
  async activate(id: string): Promise<void> { await this.refresh(id, () => this.api.activate(id)); }
  async archive(id: string): Promise<void> { await this.refresh(id, () => this.api.archive(id)); }
  async assess(id: string, elementId: string, r: AssessElementRequest): Promise<void> { await this.refresh(id, () => this.api.assess(id, elementId, r)); }
  async linkEvidence(id: string, r: LinkEvidenceRequest): Promise<void> { await this.refresh(id, () => this.api.linkEvidence(id, r)); }

  /** Runs a write, then reloads the set + readiness + gaps so all three stay consistent. */
  private async refresh<T>(id: string, call: () => Observable<T>): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(call());
      this._selected.set(await firstValueFrom(this.api.getById(id)));
      this._readiness.set(await firstValueFrom(this.api.readiness(id)));
      this._gaps.set(await firstValueFrom(this.api.gapAnalysis(id)));
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
