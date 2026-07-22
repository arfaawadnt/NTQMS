import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ChangeUserRoleRequest, CreatedResource, RegisterUserRequest,
  ResetUserPasswordRequest, UserAccount,
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

  deactivate(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/deactivate`, {}); }
  reactivate(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/reactivate`, {}); }

  resetPassword(id: string, body: ResetUserPasswordRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/reset-password`, body);
  }
}
