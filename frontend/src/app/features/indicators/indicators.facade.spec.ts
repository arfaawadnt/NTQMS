import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { IndicatorsFacade } from './indicators.facade';
import { IndicatorControlChart, IndicatorDetail } from '../../core/models';
import { environment } from '../../../environments/environment';

/**
 * Quality Indicators (HQMS M06): recording a measurement refetches the record and the
 * SPC control chart, and backend problem titles surface as the user-facing error.
 */
describe('IndicatorsFacade', () => {
  let facade: IndicatorsFacade;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/indicators`;

  const detail: IndicatorDetail = {
    id: 'i1', indicatorRef: 'IND-2026-0001', code: 'HH-1', name: 'Hand hygiene', description: null,
    numerator: 'Compliant moments', denominator: 'Observed moments', inclusions: null, exclusions: null,
    dataSource: null, unit: '%', rateFactor: 100, frequency: 'Monthly', direction: 'HigherIsBetter',
    status: 'Active', target: 90, warningThreshold: 80, actionThreshold: 70, measurements: [],
  };

  const chart: IndicatorControlChart = {
    indicatorId: 'i1', code: 'HH-1', unit: '%', hasLimits: false,
    mean: 0, stdDev: 0, ucl: 0, lcl: 0, upper2Sigma: 0, lower2Sigma: 0, upper1Sigma: 0, lower1Sigma: 0,
    target: 90, warningThreshold: 80, actionThreshold: 70, points: [],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    facade = TestBed.inject(IndicatorsFacade);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('records a measurement then refetches the indicator and the control chart', async () => {
    const done = facade.recordMeasurement('i1', { period: '2026-08-01', numerator: 90, denominator: 100, note: null });

    const post = http.expectOne(`${base}/i1/measurements`);
    expect(post.request.body).toEqual({ period: '2026-08-01', numerator: 90, denominator: 100, note: null });
    post.flush({ measurementId: 'm1' });

    // mutate() refetches the detail a microtask later…
    await new Promise((resolve) => setTimeout(resolve));
    http.expectOne(`${base}/i1`).flush(detail);
    // …then the facade refreshes the SPC chart.
    await new Promise((resolve) => setTimeout(resolve));
    http.expectOne(`${base}/i1/control-chart`).flush(chart);
    await done;

    expect(facade.selected()?.code).toBe('HH-1');
    expect(facade.chart()?.indicatorId).toBe('i1');
  });

  it('surfaces the backend problem title when a measurement is rejected', async () => {
    const done = facade.recordMeasurement('i1', { period: '2026-08-01', numerator: 5, denominator: 0, note: null });
    http.expectOne(`${base}/i1/measurements`).flush(
      { title: 'The denominator must be greater than zero.', code: 'IND-014' },
      { status: 400, statusText: 'Bad Request' });
    await done;

    expect(facade.error()).toBe('The denominator must be greater than zero.');
  });
});
