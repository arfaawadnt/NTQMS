import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { TasksApiService } from '../../core/api/tasks-api.service';
import { CreateTaskRequest, SlaDefinition, UpsertSlaRequest, WorkTask } from '../../core/models';

/**
 * Signal store for the work-task queue and SLA definitions. "My tasks" covers
 * tasks assigned to the caller directly or to the caller's role; overdue is
 * computed server-side. SLA definitions drive the backend escalation sweep.
 */
@Injectable({ providedIn: 'root' })
export class TasksFacade {
  private readonly api = inject(TasksApiService);

  private readonly _tasks = signal<WorkTask[]>([]);
  private readonly _sla = signal<SlaDefinition[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly tasks = this._tasks.asReadonly();
  readonly sla = this._sla.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  async loadTasks(): Promise<void> {
    await this.run(async () => this._tasks.set(await firstValueFrom(this.api.mine())));
  }

  async createTask(request: CreateTaskRequest): Promise<boolean> {
    return await this.run(async () => {
      await firstValueFrom(this.api.create(request));
      this._tasks.set(await firstValueFrom(this.api.mine()));
      return true;
    }) ?? false;
  }

  async completeTask(id: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.complete(id));
      this._tasks.set(await firstValueFrom(this.api.mine()));
    });
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
