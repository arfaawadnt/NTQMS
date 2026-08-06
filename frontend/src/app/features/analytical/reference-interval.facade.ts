import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { ReferenceIntervalApiService } from '../../core/api/reference-interval-api.service';
import {
  CreateReferenceIntervalStudyRequest, ReferenceIntervalDetail, ReferenceIntervalListItem,
} from '../../core/models';

/** Signal-based facade for reference-interval verification studies (CLSI EP28). */
@Injectable({ providedIn: 'root' })
export class ReferenceIntervalFacade {
  private readonly api = inject(ReferenceIntervalApiService);

  private readonly _list = signal<ReferenceIntervalListItem[]>([]);
  private readonly _selected = signal<ReferenceIntervalDetail | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly list = this._list.asReadonly();
  readonly selected = this._selected.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  async loadList(state?: string): Promise<void> {
    await this.run(async () => this._list.set(await firstValueFrom(this.api.list(state))));
  }

  async loadDetail(id: string): Promise<void> {
    await this.run(async () => this._selected.set(await firstValueFrom(this.api.getById(id))));
  }

  async create(request: CreateReferenceIntervalStudyRequest): Promise<string | null> {
    const id = await this.run(async () => (await firstValueFrom(this.api.create(request))).id);
    if (id) { await this.loadList(); }
    return id;
  }

  async addSample(id: string, value: number, subjectRef: string | null): Promise<void> {
    await this.mutate(id, () => this.api.addSample(id, value, subjectRef));
  }

  async removeSample(id: string, sampleId: string): Promise<void> {
    await this.mutate(id, () => this.api.removeSample(id, sampleId));
  }

  async calculate(id: string): Promise<void> { await this.mutate(id, () => this.api.calculate(id)); }

  async signOff(id: string, credentials: { password: string; pin: string }): Promise<void> { await this.mutate(id, () => this.api.signOff(id, credentials)); }

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
