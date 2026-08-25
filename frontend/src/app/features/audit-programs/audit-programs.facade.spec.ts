import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuditProgramsFacade } from './audit-programs.facade';
import { AuditProgramDetail } from '../../core/models';
import { environment } from '../../../environments/environment';

/**
 * Annual audit programme (HQMS M05): adding a plan line refetches the programme so the
 * coverage figure stays live; backend problem titles surface as the user-facing error.
 */
describe('AuditProgramsFacade', () => {
  let facade: AuditProgramsFacade;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/audit-programs`;

  const detail: AuditProgramDetail = {
    id: 'p1', year: 2026, title: '2026 Programme', status: 'Active',
    coverage: { planned: 1, scheduled: 0, completed: 0, coveragePercent: 0, scheduledPercent: 0 },
    plan: [],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    facade = TestBed.inject(AuditProgramsFacade);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('adds a plan line then refetches the programme', async () => {
    const done = facade.addPlanned('p1', {
      scopeArea: 'Laboratory', departmentId: null, standardChapter: 'GAHAR-LAB', priority: 'High', plannedQuarter: 1,
    });

    const post = http.expectOne(`${base}/p1/plan`);
    expect(post.request.body.scopeArea).toBe('Laboratory');
    post.flush({ plannedId: 'l1' });

    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/p1`).flush(detail);
    await done;

    expect(facade.selected()?.title).toBe('2026 Programme');
  });

  it('surfaces the backend problem title when creation fails', async () => {
    const done = facade.create({ year: 2026, title: '' });
    http.expectOne(base).flush(
      { title: 'A programme title is required.', code: 'APG-002' },
      { status: 400, statusText: 'Bad Request' });
    await done;

    expect(facade.error()).toBe('A programme title is required.');
  });
});
