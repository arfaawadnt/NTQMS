import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CredentialingFacade } from './credentialing.facade';
import { PrivilegeCheckResult } from '../../core/models';
import { environment } from '../../../environments/environment';

/**
 * Credentialing & Privileging (HQMS M13): loading pulls the roster and the expiry register
 * together; the point-of-care check surfaces its result; backend problem titles surface as the
 * user-facing error (e.g. the evidence gate on credentialing).
 */
describe('CredentialingFacade', () => {
  let facade: CredentialingFacade;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/credentialing`;

  const check: PrivilegeCheckResult = {
    practitionerId: 'p1', practitionerRef: 'PRC-2026-0001', fullName: 'Dr Alice Roe',
    privilegeName: 'Coronary angiography', holds: true, practitionerStatus: 'Credentialed', detail: null,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    facade = TestBed.inject(CredentialingFacade);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the roster and the expiry register together', async () => {
    const done = facade.loadAll();

    http.expectOne(`${base}/practitioners?`).flush([]);
    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/expiring?withinDays=90`).flush([]);
    await done;

    expect(facade.practitioners().length).toBe(0);
  });

  it('surfaces the point-of-care check result', async () => {
    const done = facade.verifyPrivilege('p1', 'Coronary angiography');
    http.expectOne(`${base}/practitioners/p1/verify-privilege?privilege=Coronary%20angiography`).flush(check);
    await done;

    expect(facade.check()?.holds).toBeTrue();
  });

  it('surfaces the evidence gate when credentialing is rejected', async () => {
    const done = facade.credential('p1', { appointedUntil: '2028-09-01' });

    http.expectOne(`${base}/practitioners/p1/credential`).flush(
      { title: 'At least one primary-source-verified licence is required.', code: 'CRD-032' },
      { status: 400, statusText: 'Bad Request' });
    await new Promise((r) => setTimeout(r));
    // The mutate refresh (getById) is skipped because the write failed.
    http.expectNone(`${base}/practitioners/p1`);
    await done;

    expect(facade.error()).toContain('primary-source-verified');
  });
});
