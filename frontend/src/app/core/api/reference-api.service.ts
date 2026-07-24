import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Branch, CreateBranchRequest, CreateDepartmentRequest, CreateTestRequest, CreatedResource,
  Department, LovEntry, TestCatalogItem, UpsertLovRequest,
} from '../models';

/**
 * Typed client for Organization & Reference Data: branches, departments,
 * the test catalog, and trilingual lists of values (one method per endpoint).
 */
@Injectable({ providedIn: 'root' })
export class ReferenceApiService {
  private readonly http = inject(HttpClient);
  private readonly api = environment.apiBaseUrl;

  branches(): Observable<Branch[]> {
    return this.http.get<Branch[]>(`${this.api}/branches`);
  }

  createBranch(body: CreateBranchRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(`${this.api}/branches`, body);
  }

  deactivateBranch(id: string): Observable<void> {
    return this.http.post<void>(`${this.api}/branches/${id}/deactivate`, {});
  }

  departments(branchId?: string): Observable<Department[]> {
    let params = new HttpParams();
    if (branchId) { params = params.set('branchId', branchId); }
    return this.http.get<Department[]>(`${this.api}/departments`, { params });
  }

  createDepartment(body: CreateDepartmentRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(`${this.api}/departments`, body);
  }

  deactivateDepartment(id: string): Observable<void> {
    return this.http.post<void>(`${this.api}/departments/${id}/deactivate`, {});
  }

  testCatalog(): Observable<TestCatalogItem[]> {
    return this.http.get<TestCatalogItem[]>(`${this.api}/test-catalog`);
  }

  createTest(body: CreateTestRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(`${this.api}/test-catalog`, body);
  }

  lovs(category?: string): Observable<LovEntry[]> {
    let params = new HttpParams();
    if (category) { params = params.set('category', category); }
    return this.http.get<LovEntry[]>(`${this.api}/lovs`, { params });
  }

  upsertLov(body: UpsertLovRequest): Observable<CreatedResource> {
    return this.http.post<CreatedResource>(`${this.api}/lovs`, body);
  }
}
