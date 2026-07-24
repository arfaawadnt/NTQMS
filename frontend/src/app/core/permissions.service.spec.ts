import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { PermissionsService } from './permissions.service';
import { AuthService } from './auth.service';

describe('PermissionsService', () => {
  const role = signal('');

  beforeEach(() => {
    role.set('');
    TestBed.configureTestingModule({
      providers: [{ provide: AuthService, useValue: { role: role.asReadonly() } }],
    });
  });

  function withRole(r: string): PermissionsService {
    role.set(r);
    return TestBed.inject(PermissionsService);
  }

  it('grants approval to Quality Managers and Tenant Admins only', () => {
    expect(withRole('QualityManager').canApprove()).toBeTrue();
    expect(withRole('TenantAdmin').canApprove()).toBeTrue();
    expect(withRole('Analyst').canApprove()).toBeFalse();
    expect(withRole('DepartmentHead').canApprove()).toBeFalse();
  });

  it('lets department heads assign training but not approve', () => {
    const perms = withRole('DepartmentHead');
    expect(perms.canAssignTraining()).toBeTrue();
    expect(perms.canApprove()).toBeFalse();
  });

  it('grants compliance-ledger access to external auditors (read-only role)', () => {
    expect(withRole('ExternalAuditor').canViewCompliance()).toBeTrue();
    expect(withRole('Analyst').canViewCompliance()).toBeFalse();
  });

  it('keeps platform administration separate from tenant roles', () => {
    const perms = withRole('PlatformAdmin');
    expect(perms.isPlatformAdmin()).toBeTrue();
    expect(perms.canApprove()).toBeFalse();
    expect(perms.isTenantAdmin()).toBeFalse();
  });
});
