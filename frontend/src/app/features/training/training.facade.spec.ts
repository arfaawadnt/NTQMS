import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TrainingFacade } from './training.facade';
import { CourseDetail } from '../../core/models';
import { environment } from '../../../environments/environment';

/**
 * Training management (HQMS M12): loading the catalogue pulls courses and the compliance
 * dashboard together; loading a course pulls its detail and its sessions; backend problem titles
 * surface as the user-facing error.
 */
describe('TrainingFacade', () => {
  let facade: TrainingFacade;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/training`;

  const detail: CourseDetail = {
    id: 'c1', courseRef: 'CRS-2026-0001', title: 'Fire Safety', category: 'Safety', description: 'd',
    durationHours: 2, validityMonths: 12, passMark: 80, status: 'Active',
    effectiveness: { sessionsHeld: 1, attendedCount: 2, passedCount: 1, passRate: 50, meanPreScore: 55, meanPostScore: 80, meanGain: 25 },
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    facade = TestBed.inject(TrainingFacade);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the catalogue and the compliance dashboard together', async () => {
    const done = facade.loadList();

    http.expectOne(`${base}/courses`).flush([]);
    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/compliance`).flush([]);
    await done;

    expect(facade.courses().length).toBe(0);
  });

  it('loads a course and its sessions together', async () => {
    const done = facade.loadCourse('c1');

    http.expectOne(`${base}/courses/c1`).flush(detail);
    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/sessions?courseId=c1`).flush([]);
    await done;

    expect(facade.course()?.effectiveness.passRate).toBe(50);
  });

  it('surfaces the backend problem title when defining a course fails', async () => {
    const done = facade.defineCourse({
      title: '', category: 'Mandatory', description: 'x', durationHours: 1, validityMonths: null, passMark: 80,
    });
    http.expectOne(`${base}/courses`).flush(
      { title: 'A course title is required.', code: 'CRS-001' },
      { status: 400, statusText: 'Bad Request' });
    await done;

    expect(facade.error()).toBe('A course title is required.');
  });
});
