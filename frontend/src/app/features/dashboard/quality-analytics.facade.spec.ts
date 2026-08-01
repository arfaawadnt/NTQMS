import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '../../../environments/environment';
import { QualityAnalytics } from '../../core/models';
import { QualityAnalyticsFacade } from './quality-analytics.facade';

/**
 * The facade must preserve two properties the page depends on: a section the
 * server withheld stays absent (it is a permission decision, not a rendering
 * one), and a filter is applied server-side rather than by narrowing a cached
 * payload in the browser.
 */
describe('QualityAnalyticsFacade', () => {
  let facade: QualityAnalyticsFacade;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/reports`;

  const payload = (overrides: Partial<QualityAnalytics> = {}): QualityAnalytics => ({
    health: {
      score: 64, contributingCategories: 7, totalCategories: 9,
      components: [
        { category: 'Risk', weight: 10, achievedScore: null, contributed: false, excludedReason: 'noData' },
      ],
    },
    documentControl: null,
    ncCapa: null,
    complaints: null,
    audits: null,
    equipment: null,
    competency: null,
    proficiencyTesting: null,
    suppliers: null,
    risk: null,
    scope: {
      branchId: null, departmentId: null, filterApplied: false,
      unscopedSections: [], hiddenSections: [],
    },
    computedAtUtc: '2026-08-01T13:00:00Z',
    ...overrides,
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    facade = TestBed.inject(QualityAnalyticsFacade);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the analytics without scope parameters when unfiltered', async () => {
    const done = facade.load();
    const request = http.expectOne((r) => r.url === `${base}/quality-analytics`);
    expect(request.request.params.get('branchId')).toBeNull();
    expect(request.request.params.get('departmentId')).toBeNull();
    request.flush(payload());
    await done;

    expect(facade.analytics()?.health.score).toBe(64);
    expect(facade.filtered()).toBeFalse();
  });

  it('sends the branch and department to the server rather than filtering locally', async () => {
    const done = facade.applyFilter('branch-1', 'dept-2');
    const request = http.expectOne((r) =>
      r.url === `${base}/quality-analytics`
      && r.params.get('branchId') === 'branch-1'
      && r.params.get('departmentId') === 'dept-2');
    request.flush(payload({
      scope: {
        branchId: 'branch-1', departmentId: 'dept-2', filterApplied: true,
        unscopedSections: ['documentControl'], hiddenSections: [],
      },
    }));
    await done;

    expect(facade.filtered()).toBeTrue();
    expect(facade.analytics()?.scope.unscopedSections).toEqual(['documentControl']);
  });

  it('keeps a withheld section absent instead of substituting an empty one', async () => {
    const done = facade.load();
    http.expectOne((r) => r.url === `${base}/quality-analytics`).flush(payload({
      scope: {
        branchId: null, departmentId: null, filterApplied: false,
        unscopedSections: [], hiddenSections: ['ncCapa'],
      },
    }));
    await done;

    // Absent means "you may not see this"; an empty object would read as
    // "there is nothing here", which is a different and false statement.
    expect(facade.analytics()?.ncCapa).toBeNull();
    expect(facade.analytics()?.scope.hiddenSections).toEqual(['ncCapa']);
  });

  it('refetches the analytics after the weighting changes', async () => {
    const done = facade.saveWeights([{ category: 'Risk', weight: 30 }], 'Risk raised for this cycle.');

    const put = http.expectOne(`${base}/quality-health-profile`);
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({
      weights: [{ category: 'Risk', weight: 30 }],
      reason: 'Risk raised for this cycle.',
    });
    put.flush(null);

    // The refetches are issued in microtasks after the PUT resolves — yield once.
    await new Promise((resolve) => setTimeout(resolve));
    http.expectOne(`${base}/quality-health-profile`).flush({ weights: [{ category: 'Risk', weight: 30 }] });
    await new Promise((resolve) => setTimeout(resolve));
    http.expectOne((r) => r.url === `${base}/quality-analytics`).flush(payload({
      health: { score: 70, contributingCategories: 7, totalCategories: 9, components: [] },
    }));

    await done;
    // The score on screen must reflect the new weighting, not the one it was
    // computed under a moment ago.
    expect(facade.analytics()?.health.score).toBe(70);
    expect(facade.weights()).toEqual([{ category: 'Risk', weight: 30 }]);
  });

  it('surfaces the backend problem title when the weighting is rejected', async () => {
    const done = facade.saveWeights([{ category: 'Risk', weight: 30 }], '');
    http.expectOne(`${base}/quality-health-profile`).flush(
      { title: 'A reason is required when changing how the quality health score is calculated.', code: 'QHP-001' },
      { status: 422, statusText: 'Unprocessable Content' });

    const saved = await done;
    expect(saved).toBeFalse();
    expect(facade.error()).toContain('A reason is required');
  });
});
