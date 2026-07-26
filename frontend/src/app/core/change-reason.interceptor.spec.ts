import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { changeReasonInterceptor } from './change-reason.interceptor';

/**
 * F-06: every DELETE must carry a reason (21 CFR Part 11 §11.10(e)). The interceptor
 * prompts for it, attaches X-Change-Reason, and aborts the request if the operator
 * declines — so nothing is voided without a recorded justification.
 */
describe('changeReasonInterceptor', () => {
  let http: HttpClient;
  let ctrl: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([changeReasonInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    ctrl = TestBed.inject(HttpTestingController);
  });

  afterEach(() => ctrl.verify());

  it('prompts on DELETE and attaches the reason as X-Change-Reason', () => {
    spyOn(window, 'prompt').and.returnValue('Transcription error');
    http.delete('/api/precision-studies/1/measurements/2').subscribe();

    const req = ctrl.expectOne('/api/precision-studies/1/measurements/2');
    expect(req.request.headers.get('X-Change-Reason')).toBe('Transcription error');
    req.flush(null);
  });

  it('aborts the DELETE (no request sent) when the operator cancels', () => {
    spyOn(window, 'prompt').and.returnValue(null);
    let completed = false;
    http.delete('/api/x/1').subscribe({ complete: () => (completed = true) });

    ctrl.expectNone('/api/x/1');
    expect(completed).toBeTrue();
  });

  it('aborts when the reason is blank', () => {
    spyOn(window, 'prompt').and.returnValue('   ');
    http.delete('/api/x/1').subscribe();
    ctrl.expectNone('/api/x/1');
  });

  it('does not prompt for non-DELETE requests', () => {
    const promptSpy = spyOn(window, 'prompt');
    http.get('/api/x').subscribe();
    ctrl.expectOne('/api/x').flush([]);
    expect(promptSpy).not.toHaveBeenCalled();
  });

  it('respects a reason already supplied by the caller', () => {
    const promptSpy = spyOn(window, 'prompt');
    http.delete('/api/x/1', { headers: { 'X-Change-Reason': 'preset' } }).subscribe();

    const req = ctrl.expectOne('/api/x/1');
    expect(req.request.headers.get('X-Change-Reason')).toBe('preset');
    expect(promptSpy).not.toHaveBeenCalled();
    req.flush(null);
  });
});
