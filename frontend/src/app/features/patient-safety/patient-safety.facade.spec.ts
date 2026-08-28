import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PatientSafetyFacade } from './patient-safety.facade';
import { SafetyEventDetail, SafetyRates } from '../../core/models';
import { environment } from '../../../environments/environment';

/**
 * Patient Safety (HQMS M08): loading the register pulls the events and the live rates
 * (per 1,000 patient-days) together; backend problem titles surface as the user error.
 */
describe('PatientSafetyFacade', () => {
  let facade: PatientSafetyFacade;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/patient-safety`;

  const rates: SafetyRates = {
    fromUtc: '2026-08-01T00:00:00Z', toUtc: '2026-08-31T00:00:00Z', patientDays: 60,
    falls: { type: 'Fall', eventCount: 3, patientDays: 60, ratePer1000: 50 },
    pressureInjuries: { type: 'PressureInjury', eventCount: 2, patientDays: 60, ratePer1000: 33.33 },
    hospitalAcquiredPressureInjuries: 1, hapiRatePer1000: 16.67,
  };

  const detail: SafetyEventDetail = {
    id: 'e1', eventRef: 'PSE-2026-0001', type: 'Fall', patientRef: 'PT-1', unit: 'Ward A', departmentId: null,
    occurredAtUtc: '2026-08-10T09:00:00Z', harmLevel: 'Minor', origin: 'HospitalAcquired', stage: null,
    description: 'x', status: 'Reported', reviewedBy: null, reviewNotes: null, reviewedAtUtc: null,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    facade = TestBed.inject(PatientSafetyFacade);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the events register and the live rates together', async () => {
    const done = facade.loadList();

    http.expectOne(`${base}/events`).flush([]);
    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/rates?windowDays=30`).flush(rates);
    await done;

    expect(facade.rates()?.falls.ratePer1000).toBe(50);
    expect(facade.rates()?.hapiRatePer1000).toBe(16.67);
  });

  it('refreshes the loaded event after a review', async () => {
    const done = facade.review('e1', { notes: 'seen' });

    http.expectOne(`${base}/events/e1/review`).flush(null);
    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/events/e1`).flush({ ...detail, status: 'Reviewed', reviewNotes: 'seen' });
    await done;

    expect(facade.selected()?.status).toBe('Reviewed');
  });

  it('surfaces the backend problem title when a report fails', async () => {
    const done = facade.reportFall({
      patientRef: '', unit: 'Ward A', occurredAtUtc: '2026-08-10T09:00:00Z',
      harm: 'None', description: 'x', departmentId: null,
    });
    http.expectOne(`${base}/falls`).flush(
      { title: 'A patient reference is required.', code: 'PSE-001' },
      { status: 400, statusText: 'Bad Request' });
    await done;

    expect(facade.error()).toBe('A patient reference is required.');
  });
});
