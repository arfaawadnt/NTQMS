import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { UncertaintyFacade } from './uncertainty.facade';
import { UncertaintyBudgetDetail } from '../../core/models';
import { environment } from '../../../environments/environment';

/**
 * MU budgets take u(Rw) from a signed-off precision study of the same analyte
 * (ISO/TS 20914): candidates are the analyte's SignedOff studies, and an
 * import refetches the budget; a refused import shows the backend title.
 */
describe('UncertaintyFacade', () => {
  let facade: UncertaintyFacade;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/uncertainty-budgets`;
  const precisionBase = `${environment.apiBaseUrl}/precision-studies`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    facade = TestBed.inject(UncertaintyFacade);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads only signed-off precision studies of the analyte as candidates', async () => {
    const done = facade.loadPrecisionCandidates('Glucose');
    const get = http.expectOne((r) => r.url === precisionBase);
    expect(get.request.params.get('state')).toBe('SignedOff');
    expect(get.request.params.get('analyte')).toBe('Glucose');
    get.flush([{ id: 'p1', studyRef: 'PR-2026-0001', analyte: 'Glucose', level: 'L1', state: 'SignedOff', withinLabCvPct: 3.1 }]);
    await done;

    expect(facade.precisionCandidates().map((p) => p.id)).toEqual(['p1']);
  });

  it('imports the within-lab CV then refetches the budget', async () => {
    const done = facade.addPrecisionComponent('b1', 'p1');
    const post = http.expectOne(`${base}/b1/components/from-precision-study`);
    expect(post.request.body).toEqual({ precisionStudyId: 'p1' });
    post.flush({ componentId: 'c1' });

    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/b1`).flush({ id: 'b1', components: [] } as unknown as UncertaintyBudgetDetail);
    await done;

    expect(facade.selected()?.id).toBe('b1');
  });

  it('surfaces the backend title when the study is not signed off', async () => {
    const done = facade.addPrecisionComponent('b1', 'p2');
    http.expectOne(`${base}/b1/components/from-precision-study`).flush(
      { title: 'Only a signed-off precision study with a within-laboratory CV can feed an uncertainty budget.', code: 'MU-008' },
      { status: 422, statusText: 'Unprocessable Entity' });
    await done;

    expect(facade.error()).toContain('signed-off precision study');
  });
});
