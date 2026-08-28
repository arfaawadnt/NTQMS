import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { IncidentsFacade } from './incidents.facade';
import { IncidentDetail } from '../../core/models';
import { environment } from '../../../environments/environment';

/**
 * Incident & Occurrence Reporting (HQMS M02). Closing is a Part 11 signing ceremony;
 * the facade posts the signed close, refetches the record so the UI shows the terminal
 * Closed state, and refreshes the §11.50 signature manifest. Anonymous reporting returns
 * a one-time follow-up reference and never records a reporter.
 */
describe('IncidentsFacade', () => {
  let facade: IncidentsFacade;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/incidents`;

  const pending: IncidentDetail = {
    id: 'i1', incidentRef: 'INC-2026-0001', title: 'Patient fall', description: 'Unwitnessed fall',
    status: 'PendingReview', category: 'Fall', location: 'Ward B', harmGrade: 'Minor', channel: 'Web',
    isSentinel: false, sentinelDeclaredAtUtc: null, isAnonymous: false, reportedBy: 'u1', assignedTo: 'u2',
    investigatorId: 'u3', investigationSummary: 'Rails down.', rejectionReason: null, closureSummary: null,
    correctiveActionNcId: null,
    occurredAtUtc: '2026-08-20T09:30:00Z', createdAtUtc: '2026-08-20T10:00:00Z',
    contributingFactors: [], timeline: [],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    facade = TestBed.inject(IncidentsFacade);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reports an incident and returns the new id', async () => {
    const done = facade.report({
      title: 'Patient fall', description: 'Unwitnessed', category: 'Fall', harmGrade: 'Minor',
      channel: 'Web', occurredAtUtc: '2026-08-20T09:30:00Z', location: null, branchId: null, departmentId: null,
    });
    const post = http.expectOne(base);
    expect(post.request.method).toBe('POST');
    post.flush({ id: 'i1' });

    expect(await done).toBe('i1');
  });

  it('reports anonymously and returns the one-time follow-up reference', async () => {
    const done = facade.reportAnonymous({
      title: 'Med near miss', description: 'Wrong dose caught', category: 'Medication', harmGrade: 'NearMiss',
      channel: 'Kiosk', occurredAtUtc: '2026-08-20T09:30:00Z', location: null, branchId: null, departmentId: null,
    });
    http.expectOne(`${base}/anonymous`).flush({ id: 'i9', incidentRef: 'INC-2026-0009', followUpReference: 'IR-ABC123' });

    const receipt = await done;
    expect(receipt?.followUpReference).toBe('IR-ABC123');
  });

  it('signs the close, refetches the incident as Closed and refreshes the signature manifest', async () => {
    const done = facade.close('i1', { closureSummary: 'Actions raised.', password: 'p', pin: '1234' });

    const post = http.expectOne(`${base}/i1/close`);
    expect(post.request.body).toEqual({ closureSummary: 'Actions raised.', password: 'p', pin: '1234' });
    post.flush(null);

    // The detail refetch is issued a microtask after the POST resolves — yield once.
    await new Promise((resolve) => setTimeout(resolve));
    http.expectOne(`${base}/i1`).flush({ ...pending, status: 'Closed', closureSummary: 'Actions raised.' });

    // The §11.50 manifest refresh is issued only after the detail refetch resolves — yield again.
    await new Promise((resolve) => setTimeout(resolve));
    http.expectOne(`${base}/i1/signatures`).flush([]);
    await done;

    expect(facade.selected()?.status).toBe('Closed');
  });

  it('surfaces the backend problem title when a close is rejected', async () => {
    const done = facade.close('i1', { closureSummary: '', password: 'p', pin: '1234' });
    http.expectOne(`${base}/i1/close`).flush(
      { title: 'A closure summary is required.', code: 'INC-025' },
      { status: 400, statusText: 'Bad Request' });
    await done;

    expect(facade.error()).toBe('A closure summary is required.');
  });

  it('redeems an anonymous follow-up reference for the report status', async () => {
    const done = facade.track('IR-ABCDEFGHJK');
    const get = http.expectOne((r) => r.url.startsWith(`${base}/track`));
    expect(get.request.method).toBe('GET');
    get.flush({ incidentRef: 'INC-2026-0007', status: 'UnderInvestigation', isSentinel: false });

    const tracking = await done;
    expect(tracking?.incidentRef).toBe('INC-2026-0007');
    expect(tracking?.status).toBe('UnderInvestigation');
  });
});
