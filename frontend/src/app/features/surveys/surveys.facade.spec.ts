import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SurveysFacade } from './surveys.facade';
import { SurveyDetail, SurveyResults } from '../../core/models';
import { environment } from '../../../environments/environment';

/**
 * Patient satisfaction surveys (HQMS M11): loading a survey pulls its questions and scored
 * results; backend problem titles surface as the user-facing error.
 */
describe('SurveysFacade', () => {
  let facade: SurveysFacade;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/surveys`;

  const detail: SurveyDetail = {
    id: 's1', title: 'Inpatient experience', description: null, status: 'Open', questions: [],
  };
  const results: SurveyResults = {
    surveyId: 's1', title: 'Inpatient experience', status: 'Open', responseCount: 2, overallScore: 4.25,
    byQuestion: [], byDomain: [], byDepartment: [],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    facade = TestBed.inject(SurveysFacade);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the survey and its results together', async () => {
    const done = facade.loadDetail('s1');

    http.expectOne(`${base}/s1`).flush(detail);
    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/s1/results`).flush(results);
    await done;

    expect(facade.selected()?.title).toBe('Inpatient experience');
    expect(facade.results()?.overallScore).toBe(4.25);
  });

  it('surfaces the backend problem title when creation fails', async () => {
    const done = facade.create({ title: '', description: null });
    http.expectOne(base).flush(
      { title: 'A survey title is required.', code: 'SVY-001' },
      { status: 400, statusText: 'Bad Request' });
    await done;

    expect(facade.error()).toBe('A survey title is required.');
  });
});
