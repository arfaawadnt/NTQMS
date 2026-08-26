import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { InfectionControlFacade } from './infection-control.facade';
import { HaiCaseDetail, HaiRates } from '../../core/models';
import { environment } from '../../../environments/environment';

/**
 * Infection Prevention & Control (HQMS M09): loading the register pulls HAI cases, device
 * exposures and the live device-associated rates together; backend problem titles surface as
 * the user-facing error.
 */
describe('InfectionControlFacade', () => {
  let facade: InfectionControlFacade;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/infection-control`;

  const rates: HaiRates = {
    fromUtc: '2026-08-01T00:00:00Z', toUtc: '2026-08-31T00:00:00Z', patientDays: 60,
    clabsi: { haiType: 'Clabsi', deviceType: 'CentralLine', deviceDays: 40, caseCount: 2, ratePer1000: 50, utilizationRatio: 0.67 },
    cauti: { haiType: 'Cauti', deviceType: 'UrinaryCatheter', deviceDays: 0, caseCount: 0, ratePer1000: 0, utilizationRatio: 0 },
    vap: { haiType: 'Vap', deviceType: 'Ventilator', deviceDays: 10, caseCount: 1, ratePer1000: 100, utilizationRatio: 0.17 },
    ssiCount: 1,
  };

  const detail: HaiCaseDetail = {
    id: 'c1', caseRef: 'HAI-2026-0001', type: 'Clabsi', patientRef: 'PT-1', unit: 'ICU', departmentId: null,
    onsetDateUtc: '2026-08-10T09:00:00Z', organism: 'E. coli', description: 'x', status: 'Reported',
    reviewedBy: null, reviewNotes: null, reviewedAtUtc: null,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    facade = TestBed.inject(InfectionControlFacade);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads cases, device exposures and the live rates together', async () => {
    const done = facade.loadAll();

    http.expectOne(`${base}/cases?`).flush([]);
    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/devices?`).flush([]);
    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/rates?windowDays=30`).flush(rates);
    await done;

    expect(facade.rates()?.clabsi.ratePer1000).toBe(50);
    expect(facade.rates()?.vap.ratePer1000).toBe(100);
  });

  it('refreshes the loaded case after a review', async () => {
    const done = facade.review('c1', { notes: 'bundle reinforced' });

    http.expectOne(`${base}/cases/c1/review`).flush(null);
    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/cases/c1`).flush({ ...detail, status: 'Reviewed', reviewNotes: 'bundle reinforced' });
    await done;

    expect(facade.selected()?.status).toBe('Reviewed');
  });

  it('surfaces the backend problem title when a case report fails', async () => {
    const done = facade.reportCase({
      type: 'Clabsi', patientRef: '', unit: 'ICU', onsetDateUtc: '2026-08-10T09:00:00Z',
      organism: null, description: 'x', departmentId: null,
    });
    http.expectOne(`${base}/cases`).flush(
      { title: 'A patient reference is required.', code: 'HAI-001' },
      { status: 400, statusText: 'Bad Request' });
    await done;

    expect(facade.error()).toBe('A patient reference is required.');
  });
});
