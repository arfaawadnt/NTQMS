import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreatedResource, ProvisionTenantRequest, Tenant } from '../models';

/** Typed client for the control-plane Tenants API (platform administrators only). */
@Injectable({ providedIn: 'root' })
export class TenantsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/tenants`;

  list(): Observable<Tenant[]> {
    return this.http.get<Tenant[]>(this.base);
  }

  provision(body: ProvisionTenantRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(this.base, body);
  }
}
