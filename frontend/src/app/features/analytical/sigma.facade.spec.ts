import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SigmaFacade } from './sigma.facade';
import { SigmaAssessmentDetail } from '../../core/models';
import { environment } from '../../../environments/environment';

/**
 * Sigma takes its CV input from a signed-off precision study's within-lab CV;
 * the refetched assessment reports that study as the source.
 */
describe('SigmaFacade', () => {
  let facade: SigmaFacade;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/sigma-assessments`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    facade = TestBed.inject(SigmaFacade);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('uses a precision study then shows it as the CV source', async () => {
    const done = facade.usePrecisionStudy('a1', 'p1');
    const post = http.expectOne(`${base}/a1/use-precision-study`);
    expect(post.request.body).toEqual({ precisionStudyId: 'p1' });
    post.flush(null);

    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/a1`).flush(
      { id: 'a1', cvPct: 3.1, sourcePrecisionStudyId: 'p1', sourcePrecisionStudyRef: 'PR-2026-0001' } as unknown as SigmaAssessmentDetail);
    await done;

    expect(facade.selected()?.sourcePrecisionStudyRef).toBe('PR-2026-0001');
  });

  it('surfaces the backend title for a study of another analyte', async () => {
    const done = facade.usePrecisionStudy('a1', 'p9');
    http.expectOne(`${base}/a1/use-precision-study`).flush(
      { title: 'The precision study measures Urea, not this assessment analyte Glucose.', code: 'SIG-007' },
      { status: 422, statusText: 'Unprocessable Entity' });
    await done;

    expect(facade.error()).toContain('Urea');
  });
});
