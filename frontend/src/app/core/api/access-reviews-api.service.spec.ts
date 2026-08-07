import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';
import { AccessReviewsApiService } from './access-reviews-api.service';
import { environment } from '../../../environments/environment';

/** Periodic user-access review (F-11 / Part 11 §11.10(d)): open → recertify → complete. */
describe('AccessReviewsApiService', () => {
  let api: AccessReviewsApiService;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/access-reviews`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    api = TestBed.inject(AccessReviewsApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('opens a review', async () => {
    const done = firstValueFrom(api.open());
    const req = http.expectOne(base);
    expect(req.request.method).toBe('POST');
    req.flush({ id: 'uar1' });
    expect(await done).toEqual({ id: 'uar1' });
  });

  it('completes a review with the changes-required flag, conclusion and e-signature', async () => {
    const done = firstValueFrom(
      api.complete('uar1', true, 'Deactivated one dormant account.', { password: 'Sign-Pass-1', pin: '2468' }));
    const req = http.expectOne(`${base}/uar1/complete`);
    expect(req.request.body).toEqual({
      changesRequired: true, conclusion: 'Deactivated one dormant account.', password: 'Sign-Pass-1', pin: '2468',
    });
    req.flush(null);
    await done;
  });
});
