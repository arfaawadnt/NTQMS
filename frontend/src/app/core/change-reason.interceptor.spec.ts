import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { changeReasonInterceptor } from './change-reason.interceptor';
import { ChangeReasonService } from './change-reason.service';

/**
 * F-06 / UI-014: every DELETE must carry a reason (21 CFR Part 11 §11.10(e)).
 * The interceptor opens the accessible reason modal via ChangeReasonService,
 * attaches X-Change-Reason on confirm, and silently aborts the request when
 * the operator cancels — so nothing is voided without a recorded justification.
 */
describe('changeReasonInterceptor', () => {
  let http: HttpClient;
  let ctrl: HttpTestingController;
  let reasons: ChangeReasonService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withXhr(), withInterceptors([changeReasonInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    ctrl = TestBed.inject(HttpTestingController);
    reasons = TestBed.inject(ChangeReasonService);
  });

  afterEach(() => ctrl.verify());

  it('opens the reason dialog on DELETE and attaches the reason as X-Change-Reason', fakeAsync(() => {
    http.delete('/api/precision-studies/1/measurements/2').subscribe();
    expect(reasons.open()).toBeTrue();

    reasons.confirm('Transcription error');
    tick();

    const req = ctrl.expectOne('/api/precision-studies/1/measurements/2');
    expect(req.request.headers.get('X-Change-Reason')).toBe('Transcription error');
    req.flush(null);
  }));

  it('aborts the DELETE (no request sent) when the operator cancels', fakeAsync(() => {
    let completed = false;
    http.delete('/api/x/1').subscribe({ complete: () => (completed = true) });

    reasons.cancel();
    tick();

    ctrl.expectNone('/api/x/1');
    expect(completed).toBeTrue();
  }));

  it('aborts when the confirmed reason is blank', fakeAsync(() => {
    let completed = false;
    http.delete('/api/x/1').subscribe({ complete: () => (completed = true) });

    reasons.confirm('   ');
    tick();

    ctrl.expectNone('/api/x/1');
    expect(completed).toBeTrue();
  }));

  it('trims the reason before sending it', fakeAsync(() => {
    http.delete('/api/x/1').subscribe();

    reasons.confirm('  corrected entry  ');
    tick();

    const req = ctrl.expectOne('/api/x/1');
    expect(req.request.headers.get('X-Change-Reason')).toBe('corrected entry');
    req.flush(null);
  }));

  it('does not open the dialog for non-DELETE requests', () => {
    http.get('/api/x').subscribe();
    ctrl.expectOne('/api/x').flush([]);
    expect(reasons.open()).toBeFalse();
  });

  it('respects a reason already supplied by the caller', () => {
    http.delete('/api/x/1', { headers: { 'X-Change-Reason': 'preset' } }).subscribe();

    const req = ctrl.expectOne('/api/x/1');
    expect(req.request.headers.get('X-Change-Reason')).toBe('preset');
    expect(reasons.open()).toBeFalse();
    req.flush(null);
  });
});
