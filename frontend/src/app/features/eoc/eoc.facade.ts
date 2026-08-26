import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { EocApiService } from '../../core/api/eoc-api.service';
import {
  AddFindingRequest, DrillDetail, DrillListItem, EocSummary, EvaluateDrillRequest, ExecuteDrillRequest,
  ResolveFindingRequest, RoundDetail, RoundListItem, ScheduleDrillRequest, ScheduleRoundRequest,
} from '../../core/models';

/**
 * Signal-based facade for Environment of Care (HQMS M15). Holds the safety-round register, the
 * drill register, the EOC summary, and the selected round/drill; refreshes after each write.
 */
@Injectable({ providedIn: 'root' })
export class EocFacade {
  private readonly api = inject(EocApiService);

  private readonly _rounds = signal<RoundListItem[]>([]);
  private readonly _drills = signal<DrillListItem[]>([]);
  private readonly _summary = signal<EocSummary | null>(null);
  private readonly _round = signal<RoundDetail | null>(null);
  private readonly _drill = signal<DrillDetail | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly rounds = this._rounds.asReadonly();
  readonly drills = this._drills.asReadonly();
  readonly summary = this._summary.asReadonly();
  readonly round = this._round.asReadonly();
  readonly drill = this._drill.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  async loadAll(): Promise<void> {
    await this.run(async () => {
      this._rounds.set(await firstValueFrom(this.api.listRounds()));
      this._drills.set(await firstValueFrom(this.api.listDrills()));
      this._summary.set(await firstValueFrom(this.api.summary()));
    });
  }

  async loadRound(id: string): Promise<void> {
    await this.run(async () => this._round.set(await firstValueFrom(this.api.getRound(id))));
  }

  async loadDrill(id: string): Promise<void> {
    await this.run(async () => this._drill.set(await firstValueFrom(this.api.getDrill(id))));
  }

  async scheduleRound(r: ScheduleRoundRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.scheduleRound(r))).id);
  }

  async scheduleDrill(r: ScheduleDrillRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.scheduleDrill(r))).id);
  }

  async startRound(id: string): Promise<void> { await this.roundMutate(id, () => this.api.startRound(id)); }
  async addFinding(id: string, r: AddFindingRequest): Promise<void> { await this.roundMutate(id, () => this.api.addFinding(id, r)); }
  async resolveFinding(id: string, findingId: string, r: ResolveFindingRequest): Promise<void> { await this.roundMutate(id, () => this.api.resolveFinding(id, findingId, r)); }
  async completeRound(id: string): Promise<void> { await this.roundMutate(id, () => this.api.completeRound(id)); }

  async executeDrill(id: string, r: ExecuteDrillRequest): Promise<void> { await this.drillMutate(id, () => this.api.executeDrill(id, r)); }
  async evaluateDrill(id: string, r: EvaluateDrillRequest): Promise<void> { await this.drillMutate(id, () => this.api.evaluateDrill(id, r)); }

  private async roundMutate<T>(id: string, call: () => Observable<T>): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(call());
      this._round.set(await firstValueFrom(this.api.getRound(id)));
    });
    if (this._error() === '') { await this.refreshLists(); }
  }

  private async drillMutate<T>(id: string, call: () => Observable<T>): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(call());
      this._drill.set(await firstValueFrom(this.api.getDrill(id)));
    });
    if (this._error() === '') { await this.refreshLists(); }
  }

  private async refreshLists(): Promise<void> {
    this._rounds.set(await firstValueFrom(this.api.listRounds()));
    this._drills.set(await firstValueFrom(this.api.listDrills()));
    this._summary.set(await firstValueFrom(this.api.summary()));
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
