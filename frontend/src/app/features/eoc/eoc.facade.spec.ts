import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { EocFacade } from './eoc.facade';
import { DrillDetail, EocSummary } from '../../core/models';
import { environment } from '../../../environments/environment';

/**
 * Environment of Care (HQMS M15): loading pulls rounds, drills and the summary together; a drill
 * mutation refreshes the loaded drill and the lists; backend problem titles surface as the error.
 */
describe('EocFacade', () => {
  let facade: EocFacade;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/eoc`;

  const summary: EocSummary = {
    roundsScheduled: 1, roundsCompleted: 1, openFindings: 1, criticalOpenFindings: 1,
    drillsScheduled: 1, drillsEvaluated: 1, meanDrillScore: 80,
  };

  const drill: DrillDetail = {
    id: 'd1', drillRef: 'EOD-2026-0001', type: 'Fire', location: 'Tower A', scheduledDate: '2026-09-01',
    status: 'Executed', executedAtUtc: '2026-09-01T09:00:00Z', participantCount: 30,
    evaluationScore: null, effectiveness: null, improvementNotes: null,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    facade = TestBed.inject(EocFacade);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads rounds, drills and the summary together', async () => {
    const done = facade.loadAll();

    http.expectOne(`${base}/rounds?`).flush([]);
    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/drills?`).flush([]);
    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/summary`).flush(summary);
    await done;

    expect(facade.summary()?.criticalOpenFindings).toBe(1);
    expect(facade.summary()?.meanDrillScore).toBe(80);
  });

  it('refreshes the loaded drill and lists after evaluating', async () => {
    const done = facade.evaluateDrill('d1', { score: 90, improvementNotes: 'x' });

    http.expectOne(`${base}/drills/d1/evaluate`).flush(null);
    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/drills/d1`).flush({ ...drill, status: 'Evaluated', evaluationScore: 90, effectiveness: 'Effective' });
    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/rounds?`).flush([]);
    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/drills?`).flush([]);
    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/summary`).flush(summary);
    await done;

    expect(facade.drill()?.effectiveness).toBe('Effective');
  });

  it('surfaces the backend problem title when scheduling a round fails', async () => {
    const done = facade.scheduleRound({ area: '', type: 'FireSafety', scheduledDate: '2026-09-01' });
    http.expectOne(`${base}/rounds`).flush(
      { title: 'An area is required.', code: 'EOC-001' },
      { status: 400, statusText: 'Bad Request' });
    await done;

    expect(facade.error()).toBe('An area is required.');
  });
});
