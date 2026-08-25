import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { StandardsFacade } from './standards.facade';
import { GapItem, ReadinessDashboard, StandardSetDetail } from '../../core/models';
import { environment } from '../../../environments/environment';

/**
 * Accreditation (HQMS M07): loading a set pulls its elements, readiness dashboard and gap
 * analysis together so the compliance figure on screen is always live and consistent.
 */
describe('StandardsFacade', () => {
  let facade: StandardsFacade;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/standards`;

  const detail: StandardSetDetail = {
    id: 's1', framework: 'GAHAR', name: 'GAHAR Hospital', version: '2024', status: 'Active', elements: [],
  };
  const readiness: ReadinessDashboard = {
    standardSetId: 's1', framework: 'GAHAR', name: 'GAHAR Hospital', version: '2024', status: 'Active',
    overall: {
      chapterCode: '*', chapterTitle: 'Overall', elementCount: 2, applicableCount: 2, compliantCount: 1,
      partialCount: 0, nonCompliantCount: 1, notAssessedCount: 0, notApplicableCount: 0, compliancePercent: 50,
    },
    chapters: [],
  };
  const gaps: GapItem[] = [];

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    facade = TestBed.inject(StandardsFacade);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the set, readiness and gap analysis together', async () => {
    const done = facade.loadDetail('s1');

    http.expectOne(`${base}/s1`).flush(detail);
    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/s1/readiness`).flush(readiness);
    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/s1/gap-analysis`).flush(gaps);
    await done;

    expect(facade.selected()?.name).toBe('GAHAR Hospital');
    expect(facade.readiness()?.overall.compliancePercent).toBe(50);
  });

  it('surfaces the backend problem title when defining a set fails', async () => {
    const done = facade.define({ framework: 'GAHAR', name: '', version: '2024' });
    http.expectOne(base).flush(
      { title: 'A standard-set name is required.', code: 'STD-001' },
      { status: 400, statusText: 'Bad Request' });
    await done;

    expect(facade.error()).toBe('A standard-set name is required.');
  });
});
