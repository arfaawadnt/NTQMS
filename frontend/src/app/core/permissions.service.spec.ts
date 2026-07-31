import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PermissionsService } from './permissions.service';
import { AuthService } from './auth.service';
import { I18nService } from './i18n.service';
import { MyPrivileges } from './models';

describe('PermissionsService', () => {
  const authenticated = signal(false);
  const role = signal('');

  beforeEach(() => {
    authenticated.set(false);
    role.set('');
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: AuthService,
          useValue: { role: role.asReadonly(), isAuthenticated: authenticated.asReadonly() },
        },
      ],
    });
  });

  function privileges(overrides: Partial<MyPrivileges>): MyPrivileges {
    return {
      roleId: 'r1',
      roleName: 'Custom Role',
      isPlatformAdmin: false,
      permissions: [],
      branchIds: [],
      departmentIds: [],
      preferredLanguage: null,
      ...overrides,
    };
  }

  function signInWith(p: MyPrivileges): PermissionsService {
    const service = TestBed.inject(PermissionsService);
    authenticated.set(true);
    TestBed.tick();
    TestBed.inject(HttpTestingController)
      .expectOne((r) => r.url.endsWith('/auth/me/privileges'))
      .flush(p);
    return service;
  }

  it('denies everything before privileges arrive', () => {
    const service = TestBed.inject(PermissionsService);
    expect(service.can('nc.view')).toBeFalse();
  });

  it('answers can() from the granted permission keys', () => {
    const service = signInWith(privileges({ permissions: ['nc.view', 'documents.approve'] }));

    expect(service.can('nc.view')).toBeTrue();
    expect(service.can('documents.approve')).toBeTrue();
    expect(service.can('documents.sign')).toBeFalse();
    expect(service.canAny('documents.sign', 'nc.view')).toBeTrue();
  });

  it('exposes the configured role name and working scope', () => {
    const service = signInWith(privileges({ roleName: 'NC Reader', branchIds: ['b1'] }));

    expect(service.roleName()).toBe('NC Reader');
    expect(service.branchIds()).toEqual(['b1']);
  });

  it('applies the profile language on load', () => {
    const i18n = TestBed.inject(I18nService);
    signInWith(privileges({ preferredLanguage: 'fr' }));

    expect(i18n.lang()).toBe('fr');
    i18n.setLang('en');
  });

  it('clears privileges on sign-out and denies again', () => {
    const service = signInWith(privileges({ permissions: ['nc.view'] }));
    expect(service.can('nc.view')).toBeTrue();

    authenticated.set(false);
    TestBed.tick();
    expect(service.can('nc.view')).toBeFalse();
  });

  it('keeps platform administration on the session tier, without a privileges fetch', () => {
    const service = TestBed.inject(PermissionsService);
    role.set('PlatformAdmin');
    authenticated.set(true);
    TestBed.tick();

    TestBed.inject(HttpTestingController).expectNone((r) => r.url.endsWith('/auth/me/privileges'));
    expect(service.isPlatformAdmin()).toBeTrue();
    // The platform tier passes any tenant gate it can reach.
    expect(service.can('roles.manage')).toBeTrue();
  });
});
