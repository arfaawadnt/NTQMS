import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateRoleRequest, CreatedResource, PermissionCatalog, RoleDetail,
  RoleSummary, SetRolePermissionsRequest, UpdateRoleRequest,
} from '../models';

/** Typed client for roles & privileges administration (server-enforced). */
@Injectable({ providedIn: 'root' })
export class RolesApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/roles`;

  /** The permission catalogue the matrix renders. Identical for every tenant. */
  catalog(): Observable<PermissionCatalog> {
    return this.http.get<PermissionCatalog>(`${this.base}/catalog`);
  }

  list(): Observable<RoleSummary[]> {
    return this.http.get<RoleSummary[]>(this.base);
  }

  get(id: string): Observable<RoleDetail> {
    return this.http.get<RoleDetail>(`${this.base}/${id}`);
  }

  create(body: CreateRoleRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  update(id: string, body: UpdateRoleRequest): Observable<void> {
    return this.http.put<void>(`${this.base}/${id}`, body);
  }

  /** Replaces the role's grants; the reason lands in the audit trail. */
  setPermissions(id: string, body: SetRolePermissionsRequest): Observable<void> {
    return this.http.put<void>(`${this.base}/${id}/permissions`, body);
  }

  deactivate(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/deactivate`, {}); }
  reactivate(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/reactivate`, {}); }
}
