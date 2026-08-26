import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MortalityReviewFacade } from './mortality-review.facade';
import { MortalityDetail, MortalityRates } from '../../core/models';
import { environment } from '../../../environments/environment';

/**
 * Mortality, Morbidity & Peer Review (HQMS M10): loading pulls reviews, complications and the live
 * rates together; a classify refreshes the loaded review; backend problem titles surface as the
 * user-facing error (e.g. the SoD guard on the second review).
 */
describe('MortalityReviewFacade', () => {
  let facade: MortalityReviewFacade;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/mortality-review`;

  const rates: MortalityRates = {
    fromUtc: '2026-08-01T00:00:00Z', toUtc: '2026-08-31T00:00:00Z', patientDays: 60,
    deaths: 3, mortalityRatePer1000: 50, expected: 1, unexpected: 1, potentiallyPreventable: 1, preventable: 0,
    complications: 2, preventableComplications: 1,
  };

  const detail: MortalityDetail = {
    id: 'm1', reviewRef: 'MRT-2026-0001', patientRef: 'PT-1', unit: 'ICU', departmentId: null,
    deathDateUtc: '2026-08-10T03:00:00Z', primaryDiagnosis: 'Sepsis', status: 'Reported', classification: null,
    requiresSecondReview: false, firstReviewerId: null, classificationFindings: null,
    secondReviewerId: null, secondReviewNotes: null, secondReviewerConcurs: null, committeeLearnings: null,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    facade = TestBed.inject(MortalityReviewFacade);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads reviews, complications and the live rates together', async () => {
    const done = facade.loadAll();

    http.expectOne(`${base}/reviews?`).flush([]);
    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/complications?`).flush([]);
    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/rates?windowDays=30`).flush(rates);
    await done;

    expect(facade.rates()?.mortalityRatePer1000).toBe(50);
    expect(facade.rates()?.potentiallyPreventable).toBe(1);
  });

  it('refreshes the loaded review after a classify', async () => {
    const done = facade.classify('m1', { classification: 'Unexpected', findings: 'Sudden deterioration.' });

    http.expectOne(`${base}/reviews/m1/classify`).flush(null);
    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/reviews/m1`).flush({ ...detail, status: 'Classified', classification: 'Unexpected', requiresSecondReview: true });
    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/reviews?`).flush([]);
    await done;

    expect(facade.selected()?.requiresSecondReview).toBeTrue();
  });

  it('surfaces the SoD guard when the second review is rejected', async () => {
    const done = facade.secondReview('m1', { notes: 'Concur.', concurs: true });

    http.expectOne(`${base}/reviews/m1/second-review`).flush(
      { title: 'The second review must be performed by a different reviewer (SoD-MRT-001).', code: 'MRT-014' },
      { status: 400, statusText: 'Bad Request' });
    await new Promise((r) => setTimeout(r));
    await done;

    expect(facade.error()).toContain('different reviewer');
  });
});
