import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateTaskRequest, CreatedResource, SlaDefinition, UpsertSlaRequest, WorkTask } from '../models';

/** Typed client for the work-task queue and SLA definitions (one method per endpoint). */
@Injectable({ providedIn: 'root' })
export class TasksApiService {
  private readonly http = inject(HttpClient);
  private readonly tasks = `${environment.apiBaseUrl}/tasks`;
  private readonly sla = `${environment.apiBaseUrl}/sla-definitions`;

  /** Tasks assigned to the caller directly or to the caller's role. */
  mine(): Observable<WorkTask[]> {
    return this.http.get<WorkTask[]>(`${this.tasks}/mine`);
  }

  create(body: CreateTaskRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.tasks, body);
  }

  complete(id: string): Observable<void> {
    return this.http.post<void>(`${this.tasks}/${id}/complete`, {});
  }

  slaDefinitions(): Observable<SlaDefinition[]> {
    return this.http.get<SlaDefinition[]>(this.sla);
  }

  upsertSla(body: UpsertSlaRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.sla, body);
  }
}
