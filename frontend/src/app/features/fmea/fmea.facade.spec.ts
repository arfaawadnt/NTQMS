import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { FmeaFacade } from './fmea.facade';
import { FmeaDetail } from '../../core/models';
import { environment } from '../../../environments/environment';

/**
 * FMEA / HFMEA (HQMS M04): adding a failure mode refetches the worksheet so RPNs stay
 * current; backend problem titles surface as the user-facing error.
 */
describe('FmeaFacade', () => {
  let facade: FmeaFacade;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/fmea`;

  const detail: FmeaDetail = {
    id: 'f1', fmeaRef: 'FMEA-2026-0001', title: 'Med admin', processName: 'Medication', type: 'Hfmea',
    status: 'Active', branchId: null, departmentId: null, highRpnThreshold: 100, failureModes: [],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    facade = TestBed.inject(FmeaFacade);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('adds a failure mode then refetches the worksheet', async () => {
    const done = facade.addFailureMode('f1', {
      processStep: 'Dispensing', failureMode: 'Wrong drug', effect: 'Harm', cause: 'LASA',
      severity: 8, occurrence: 6, detection: 5,
    });

    const post = http.expectOne(`${base}/f1/failure-modes`);
    expect(post.request.body.severity).toBe(8);
    post.flush({ modeId: 'm1' });

    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/f1`).flush(detail);
    await done;

    expect(facade.selected()?.fmeaRef).toBe('FMEA-2026-0001');
  });

  it('surfaces the backend problem title when a rating is out of range', async () => {
    const done = facade.addFailureMode('f1', {
      processStep: 'x', failureMode: 'y', effect: '', cause: '', severity: 11, occurrence: 5, detection: 5,
    });
    http.expectOne(`${base}/f1/failure-modes`).flush(
      { title: 'Severity, occurrence and detection must each be explicitly rated 1–10.', code: 'FME-019' },
      { status: 400, statusText: 'Bad Request' });
    await done;

    expect(facade.error()).toContain('rated 1');
  });
});
