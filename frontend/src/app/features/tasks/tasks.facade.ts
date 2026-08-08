import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { TasksApiService } from '../../core/api/tasks-api.service';
import { CreateTaskRequest, MyAction, Paged, SlaDefinition, UpsertSlaRequest, WorkTask } from '../../core/models';

/**
 * Signal store for the work-task queue and SLA definitions. "My tasks" covers
 * tasks assigned to the caller directly or to the caller's role; overdue is
 * computed server-side. SLA definitions drive the backend escalation sweep.
 */
@Injectable({ providedIn: 'root' })
export class TasksFacade {
  private readonly api = inject(TasksApiService);

  private readonly _actions = signal<MyAction[]>([]);
  private readonly _tasks = signal<WorkTask[]>([]);
  private readonly _total = signal(0);
  private readonly _hasMore = signal(false);
  private readonly _sla = signal<SlaDefinition[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal('');
  /** 1-based page of the last fetched slice (R-3 load-more pager). */
  private readonly _page = signal(1);

  /** The unified action feed: every pending action across the system awaiting the caller. */
  readonly actions = this._actions.asReadonly();
  readonly tasks = this._tasks.asReadonly();
  /** Total matching tasks on the server (pagination envelope, API-004). */
  readonly total = this._total.asReadonly();
  /** True when more pages exist beyond the loaded slice. */
  readonly hasMore = this._hasMore.asReadonly();
  readonly sla = this._sla.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  async loadTasks(): Promise<void> {
    await this.run(async () => this.applyFirstPage(await firstValueFrom(this.api.mine())));
  }

  /** Loads the unified action feed (the primary content of the page). */
  async loadActions(): Promise<void> {
    await this.run(async () => this._actions.set(await firstValueFrom(this.api.myActions())));
  }

  /** Appends the next page of my tasks (R-3); no-op while loading or exhausted. */
  async loadMore(): Promise<void> {
    if (this._loading() || !this._hasMore()) { return; }
    await this.run(async () => {
      const next = this._page() + 1;
      const page = await firstValueFrom(this.api.mine(next));
      this._page.set(next);
      this._tasks.update((items) => [...items, ...page.items]);
      this._total.set(page.total);
      this._hasMore.set(page.hasMore);
    });
  }

  async createTask(request: CreateTaskRequest): Promise<boolean> {
    return await this.run(async () => {
      await firstValueFrom(this.api.create(request));
      this.applyFirstPage(await firstValueFrom(this.api.mine()));
      this._actions.set(await firstValueFrom(this.api.myActions()));
      return true;
    }) ?? false;
  }

  async completeTask(id: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.complete(id));
      this.applyFirstPage(await firstValueFrom(this.api.mine()));
      this._actions.set(await firstValueFrom(this.api.myActions()));
    });
  }

  /** Unwraps a first-page envelope into the task signals, resetting the pager. */
  private applyFirstPage(page: Paged<WorkTask>): void {
    this._page.set(1);
    this._tasks.set(page.items);
    this._total.set(page.total);
    this._hasMore.set(page.hasMore);
  }

  async loadSla(): Promise<void> {
    await this.run(async () => this._sla.set(await firstValueFrom(this.api.slaDefinitions())));
  }

  async upsertSla(request: UpsertSlaRequest): Promise<boolean> {
    return await this.run(async () => {
      await firstValueFrom(this.api.upsertSla(request));
      this._sla.set(await firstValueFrom(this.api.slaDefinitions()));
      return true;
    }) ?? false;
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
