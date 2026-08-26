import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AddLicenceRequest, CredentialRequest, CreatedResource, DenyPrivilegeRequest, ExpiringCredential,
  GrantPrivilegeRequest, PractitionerDetail, PractitionerListItem, PrivilegeCheckResult,
  RegisterPractitionerRequest, RequestPrivilegeRequest, SuspendPractitionerRequest, VerifyLicenceRequest,
} from '../models';

/** Typed client for the Credentialing & Privileging API (HQMS M13). */
@Injectable({ providedIn: 'root' })
export class CredentialingApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/credentialing`;

  list(specialty?: string, status?: string): Observable<PractitionerListItem[]> {
    const params = new URLSearchParams();
    if (specialty) { params.set('specialty', specialty); }
    if (status) { params.set('status', status); }
    return this.http.get<PractitionerListItem[]>(`${this.base}/practitioners?${params.toString()}`);
  }

  getById(id: string): Observable<PractitionerDetail> { return this.http.get<PractitionerDetail>(`${this.base}/practitioners/${id}`); }
  expiring(withinDays = 90): Observable<ExpiringCredential[]> { return this.http.get<ExpiringCredential[]>(`${this.base}/expiring?withinDays=${withinDays}`); }
  verifyPrivilege(id: string, privilege: string): Observable<PrivilegeCheckResult> {
    return this.http.get<PrivilegeCheckResult>(`${this.base}/practitioners/${id}/verify-privilege?privilege=${encodeURIComponent(privilege)}`);
  }

  register(body: RegisterPractitionerRequest): Observable<CreatedResource> { return this.http.post<CreatedResource>(`${this.base}/practitioners`, body); }
  addLicence(id: string, body: AddLicenceRequest): Observable<CreatedResource> { return this.http.post<CreatedResource>(`${this.base}/practitioners/${id}/licences`, body); }
  verifyLicence(id: string, licenceId: string, body: VerifyLicenceRequest): Observable<void> { return this.http.post<void>(`${this.base}/practitioners/${id}/licences/${licenceId}/verify`, body); }
  requestPrivilege(id: string, body: RequestPrivilegeRequest): Observable<CreatedResource> { return this.http.post<CreatedResource>(`${this.base}/practitioners/${id}/privileges`, body); }
  grantPrivilege(id: string, privilegeId: string, body: GrantPrivilegeRequest): Observable<void> { return this.http.post<void>(`${this.base}/practitioners/${id}/privileges/${privilegeId}/grant`, body); }
  denyPrivilege(id: string, privilegeId: string, body: DenyPrivilegeRequest): Observable<void> { return this.http.post<void>(`${this.base}/practitioners/${id}/privileges/${privilegeId}/deny`, body); }
  credential(id: string, body: CredentialRequest): Observable<void> { return this.http.post<void>(`${this.base}/practitioners/${id}/credential`, body); }
  reappoint(id: string, body: CredentialRequest): Observable<void> { return this.http.post<void>(`${this.base}/practitioners/${id}/reappoint`, body); }
  suspend(id: string, body: SuspendPractitionerRequest): Observable<void> { return this.http.post<void>(`${this.base}/practitioners/${id}/suspend`, body); }
  reinstate(id: string): Observable<void> { return this.http.post<void>(`${this.base}/practitioners/${id}/reinstate`, {}); }
}
