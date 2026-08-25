import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { IntegrationFacade } from './integration.facade';
import { EndpointListItem, PatientCensus } from '../../core/models';
import { environment } from '../../../environments/environment';

/**
 * Integration monitoring (HQMS M24): the dashboard loads endpoint health and the census
 * together; backend problem titles surface as the user-facing error.
 */
describe('IntegrationFacade', () => {
  let facade: IntegrationFacade;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/integration`;

  const endpoints: EndpointListItem[] = [{
    id: 'e1', name: 'HIS ADT', system: 'His', protocol: 'Hl7V2', status: 'Active', healthy: true,
    lastMessageAtUtc: null, lastErrorAtUtc: null, consecutiveFailures: 0, received: 3, processed: 3, failed: 0,
  }];
  const census: PatientCensus = { activeStays: 12, patientDaysWindow: 240, asOfUtc: '2026-09-01T00:00:00Z', fromUtc: '2026-08-02T00:00:00Z' };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    facade = TestBed.inject(IntegrationFacade);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads endpoints and census together', async () => {
    const done = facade.load();

    http.expectOne(`${base}/endpoints`).flush(endpoints);
    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/census?windowDays=30`).flush(census);
    await done;

    expect(facade.endpoints().length).toBe(1);
    expect(facade.census()?.activeStays).toBe(12);
  });

  it('surfaces the backend problem title when registration fails', async () => {
    const done = facade.register({ name: '', system: 'His', protocol: 'Hl7V2' });
    http.expectOne(`${base}/endpoints`).flush(
      { title: 'An endpoint name is required.', code: 'INT-001' },
      { status: 400, statusText: 'Bad Request' });
    await done;

    expect(facade.error()).toBe('An endpoint name is required.');
  });
});
