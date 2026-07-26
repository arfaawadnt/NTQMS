import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeFacade } from './change.facade';
import { ChangeDetail } from '../../core/models';
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
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
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
});
