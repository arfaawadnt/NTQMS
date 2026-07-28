import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateTaskRequest, CreatedResource, DEFAULT_PAGE_SIZE, Paged, SlaDefinition, UpsertSlaRequest, WorkTask } from '../models';

/** Typed client for the work-task queue and SLA definitions (one method per endpoint). */
@Injectable({ providedIn: 'root' })
export class TasksApiService {
  private readonly http = inject(HttpClient);
  private readonly tasks = `${environment.apiBaseUrl}/tasks`;
  private readonly sla = `${environment.apiBaseUrl}/sla-definitions`;

  /** Tasks assigned to the caller directly or to the caller's role. */
  mine(page = 1, pageSize = DEFAULT_PAGE_SIZE): Observable<Paged<WorkTask>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<Paged<WorkTask>>(`${this.tasks}/mine`, { params });
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
