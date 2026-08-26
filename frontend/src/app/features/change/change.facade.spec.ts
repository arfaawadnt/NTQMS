import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeFacade } from './change.facade';
import { ChangeDetail, ChangeListItem, Paged } from '../../core/models';
import { environment } from '../../../environments/environment';

/**
 * Regulated change-control workflow (F-11): the post-implementation review (PIR)
 * verifies a closed change was effective, then the facade refetches so the UI shows
 * the terminal Reviewed state.
 */
describe('ChangeFacade — post-implementation review', () => {
  let facade: ChangeFacade;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/changes`;

  const closed: ChangeDetail = {
    id: 'ch1', changeRef: 'CHG-2026-0001', title: 'New LIS interface', impactAnalysis: 'x',
    status: 'Closed', proposedBy: 'u1', riskItemId: 'r1', approvedBy: 'u2', approvedAtUtc: '2026-07-01T00:00:00Z',
    rejectionReason: null, implementationNotes: 'Deployed', changeEffective: null,
    postImplementationReviewNotes: null, postImplementationReviewedBy: null, postImplementationReviewedAtUtc: null,
    impactLevel: 'Medium', isEmergency: false, retrospectiveDeadline: null, ratifiedBy: null, ratifiedAtUtc: null,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    facade = TestBed.inject(ChangeFacade);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('posts the review and refetches the change as Reviewed', async () => {
    const done = facade.review('ch1', true, 'KPIs confirm objective met.');

    const post = http.expectOne(`${base}/ch1/review`);
    expect(post.request.body).toEqual({ effective: true, notes: 'KPIs confirm objective met.' });
    post.flush(null);

    // The refetch is issued in a microtask after the POST resolves — yield once.
    await new Promise((resolve) => setTimeout(resolve));
    http.expectOne(`${base}/ch1`).flush({ ...closed, status: 'Reviewed', changeEffective: true });
    await done;

    expect(facade.selected()?.status).toBe('Reviewed');
    expect(facade.selected()?.changeEffective).toBeTrue();
  });

  it('surfaces the backend problem title when the review is rejected', async () => {
    const done = facade.review('ch1', true, '');
    http.expectOne(`${base}/ch1/review`).flush(
      { title: 'Post-implementation review notes are required.', code: 'CHG-021' },
      { status: 400, statusText: 'Bad Request' });
    await done;

    expect(facade.error()).toBe('Post-implementation review notes are required.');
  });

  it('ratifies an emergency change and refetches it as Closed (HQMS M18)', async () => {
    const done = facade.ratify('ch1', 'Documented and confirmed.', { password: 'pw', pin: '1234' });

    const post = http.expectOne(`${base}/ch1/ratify`);
    expect(post.request.body).toEqual({ implementationNotes: 'Documented and confirmed.', password: 'pw', pin: '1234' });
    post.flush(null);

    await new Promise((resolve) => setTimeout(resolve));
    http.expectOne(`${base}/ch1`).flush({ ...closed, status: 'Closed', isEmergency: true, ratifiedBy: 'u2' });
    await done;

    expect(facade.selected()?.status).toBe('Closed');
    expect(facade.selected()?.ratifiedBy).toBe('u2');
  });

  // ── R-3 load-more pager over the API-004 envelope ─────────────────────────

  function item(id: string): ChangeListItem {
    return {
      id, changeRef: `CHG-2026-${id}`, title: `Change ${id}`, status: 'Proposed',
      riskItemId: null, impactLevel: 'Medium', isEmergency: false, branchId: null, departmentId: null,
    };
  }

  function envelope(items: ChangeListItem[], page: number, total: number, hasMore: boolean): Paged<ChangeListItem> {
    return { items, total, page, pageSize: 50, hasMore };
  }

  it('loadMore fetches the next page with the same filter and appends it', async () => {
    const first = facade.loadList('Proposed');
    http.expectOne((r) => r.url === base && r.params.get('page') === '1' && r.params.get('status') === 'Proposed')
      .flush(envelope([item('a'), item('b')], 1, 3, true));
    await first;

    const more = facade.loadMore();
    http.expectOne((r) => r.url === base && r.params.get('page') === '2' && r.params.get('status') === 'Proposed')
      .flush(envelope([item('c')], 2, 3, false));
    await more;

    expect(facade.list().map((c) => c.id)).toEqual(['a', 'b', 'c']);
    expect(facade.total()).toBe(3);
    expect(facade.hasMore()).toBeFalse();
  });

  it('a reload resets to page 1 and replaces the accumulated list', async () => {
    const first = facade.loadList();
    http.expectOne((r) => r.url === base && r.params.get('page') === '1')
      .flush(envelope([item('a')], 1, 2, true));
    await first;

    const more = facade.loadMore();
    http.expectOne((r) => r.url === base && r.params.get('page') === '2')
      .flush(envelope([item('b')], 2, 2, false));
    await more;
    expect(facade.list().length).toBe(2);

    // Filter change → loadList again: back to page 1, list replaced not appended.
    const reload = facade.loadList('Closed');
    http.expectOne((r) => r.url === base && r.params.get('page') === '1' && r.params.get('status') === 'Closed')
      .flush(envelope([item('z')], 1, 1, false));
    await reload;

    expect(facade.list().map((c) => c.id)).toEqual(['z']);

    // And a subsequent loadMore is a no-op because no more pages exist.
    await facade.loadMore();
    http.expectNone((r) => r.url === base && r.params.get('page') === '2');
  });
});
