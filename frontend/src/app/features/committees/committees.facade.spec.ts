import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CommitteesFacade } from './committees.facade';
import { CommitteeDetail } from '../../core/models';
import { environment } from '../../../environments/environment';

/**
 * Committees & Governance (HQMS M17): loading a committee pulls its detail, meetings and
 * open actions together; backend problem titles surface as the user-facing error.
 */
describe('CommitteesFacade', () => {
  let facade: CommitteesFacade;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/committees`;

  const detail: CommitteeDetail = {
    id: 'c1', name: 'Quality & Safety', termsOfReference: 'ToR', frequency: 'Monthly', quorumSize: 3,
    status: 'Active', members: [],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    facade = TestBed.inject(CommitteesFacade);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the committee, its meetings and open actions together', async () => {
    const done = facade.loadCommittee('c1');

    http.expectOne(`${base}/c1`).flush(detail);
    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/c1/meetings`).flush([]);
    await new Promise((r) => setTimeout(r));
    http.expectOne(`${base}/c1/open-actions`).flush([]);
    await done;

    expect(facade.committee()?.name).toBe('Quality & Safety');
  });

  it('surfaces the backend problem title when creation fails', async () => {
    const done = facade.create({ name: '', termsOfReference: 'x', frequency: 'Monthly', quorumSize: 3 });
    http.expectOne(base).flush(
      { title: 'A committee name is required.', code: 'CMT-001' },
      { status: 400, statusText: 'Bad Request' });
    await done;

    expect(facade.error()).toBe('A committee name is required.');
  });
});
