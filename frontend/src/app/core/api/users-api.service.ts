import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AssignUserRoleRequest, ChangeUserRoleRequest, CreatedResource, RegisterUserRequest,
  ResetUserPasswordRequest, SetUserLanguageRequest, SetUserScopeRequest,
  UserAccount, UserDirectoryEntry,
} from '../models';

/** Typed client for tenant user administration (tenant-admin only, server-enforced). */
@Injectable({ providedIn: 'root' })
export class UsersApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/users`;

  list(): Observable<UserAccount[]> {
    return this.http.get<UserAccount[]>(this.base);
  }

  register(body: RegisterUserRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }

  changeRole(id: string, body: ChangeUserRoleRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/role`, body);
  }

  /** Moves the user onto a configurable role. */
  assignRole(id: string, body: AssignUserRoleRequest): Observable<void> {
    return this.http.put<void>(`${this.base}/${id}/assigned-role`, body);
  }

  /** Sets the user's allowed branches/departments; empty lists mean unrestricted. */
  setScope(id: string, body: SetUserScopeRequest): Observable<void> {
    return this.http.put<void>(`${this.base}/${id}/scope`, body);
  }

  /** Sets the user's interface language; null inherits role, then tenant. */
  setLanguage(id: string, body: SetUserLanguageRequest): Observable<void> {
    return this.http.put<void>(`${this.base}/${id}/language`, body);
  }

  deactivate(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/deactivate`, {}); }
  reactivate(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/reactivate`, {}); }

  resetPassword(id: string, body: ResetUserPasswordRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/reset-password`, body);
  }

  /** Active-user directory for name pickers (readable by every tenant user). */
  directory(): Observable<UserDirectoryEntry[]> {
    return this.http.get<UserDirectoryEntry[]>(`${this.base}/directory`);
  }
}
