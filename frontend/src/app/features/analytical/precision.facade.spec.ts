import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PrecisionFacade } from './precision.facade';
import { PrecisionDetail } from '../../core/models';
import { environment } from '../../../environments/environment';

/**
 * Imprecision studies (CLSI EP05 / EP15): a multi-level series is created in
 * one call and opens its first level; a replicate carries its day label and
 * measurement conditions verbatim; backend problem titles surface as errors.
 */
describe('PrecisionFacade', () => {
  let facade: PrecisionFacade;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/precision-studies`;

  const detail = {
    id: 's1', studyRef: 'PR-2026-0001', analyte: 'Glucose', unit: 'mmol/L', level: 'Low', state: 'DataEntry',
    measurements: [], runs: [], seriesMembers: [], ptContext: [], seriesRef: 'PRS-2026-0001', seriesLevelCount: 2,
  } as unknown as PrecisionDetail;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    facade = TestBed.inject(PrecisionFacade);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('creates a series and returns the first level to open, then reloads the register', async () => {
    const done = facade.createSeries({
      analyte: 'Glucose', unit: 'mmol/L',
      levels: [
        { level: 'Low', claimedRepeatabilityCvPct: 2.5, claimedWithinLabCvPct: 4 },
        { level: 'High', claimedRepeatabilityCvPct: null, claimedWithinLabCvPct: null },
      ],
    });

    const post = http.expectOne(`${base}/series`);
    expect(post.request.body.levels.length).toBe(2);
    post.flush({ seriesRef: 'PRS-2026-0001', studyIds: ['s1', 's2'] });

    await new Promise((r) => setTimeout(r));
    http.expectOne(base).flush([]);
    expect(await done).toBe('s1');
  });

  it('sends the day label and measurement conditions with a replicate, then refetches', async () => {
    const done = facade.addMeasurement('s1', {
      runLabel: 'R1', value: 5.42, dayLabel: 'D03', operator: 'A. Hassan', instrument: 'Cobas-1',
      reagentLot: 'LOT-77', calibratorLot: 'CAL-9', measuredOn: '2026-10-03',
    });

    const post = http.expectOne(`${base}/s1/measurements`);
    expect(post.request.body).toEqual(jasmine.objectContaining({ dayLabel: 'D03', reagentLot: 'LOT-77', measuredOn: '2026-10-03' }));
    post.flush(null);

    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/s1`).flush(detail);
    await done;

    expect(facade.selected()?.seriesRef).toBe('PRS-2026-0001');
  });

  it('surfaces the backend problem title when a design is partly day-labelled', async () => {
    const done = facade.calculate('s1');
    http.expectOne(`${base}/s1/calculate`).flush(
      { title: 'Either every replicate carries a day label or none does — a design cannot be partly nested.', code: 'PR-015' },
      { status: 422, statusText: 'Unprocessable Entity' });
    await done;

    expect(facade.error()).toContain('day label');
  });
});
