import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { AnalyticalApiService } from '../../core/api/analytical-api.service';
import {
  ConfigureStudyRequest, EnterReplicateRequest, ValidationStudyDetail, ValidationStudyListItem,
} from '../../core/models';

/**
 * Signal store for method-validation studies: configure protocol → enter
 * replicates → calculate statistics (bias/CV vs total allowable error) →
 * QM sign-off, mirroring the backend state machine.
 */
@Injectable({ providedIn: 'root' })
export class ValidationFacade {
  private readonly api = inject(AnalyticalApiService);

  private readonly _list = signal<ValidationStudyListItem[]>([]);
  private readonly _selected = signal<ValidationStudyDetail | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly list = this._list.asReadonly();
  readonly selected = this._selected.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  async loadList(state?: string): Promise<void> {
    await this.run(async () => this._list.set(await firstValueFrom(this.api.studiesList(state))));
  }

  async loadDetail(id: string): Promise<void> {
    await this.run(async () => this._selected.set(await firstValueFrom(this.api.studyById(id))));
  }

  async configure(request: ConfigureStudyRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.configureStudy(request))).id);
  }

  async enterReplicate(id: string, request: EnterReplicateRequest): Promise<void> {
    await this.mutate(id, () => this.api.enterReplicate(id, request));
  }

  async calculate(id: string): Promise<void> {
    await this.mutate(id, () => this.api.calculateStudy(id));
  }

  async signOff(id: string, credentials: { password: string; pin: string }): Promise<void> {
    await this.mutate(id, () => this.api.signOffStudy(id, credentials));
  }

  private async mutate(id: string, call: () => Observable<void>): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(call());
      this._selected.set(await firstValueFrom(this.api.studyById(id)));
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
