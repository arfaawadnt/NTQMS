import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { EMPTY, from, switchMap } from 'rxjs';
import { ChangeReasonService } from './change-reason.service';

const CHANGE_REASON_HEADER = 'X-Change-Reason';

/**
 * 21 CFR Part 11 §11.10(e) / ALCOA+: every DELETE in this system voids a piece
 * of analytical evidence, which may never happen without a recorded reason. If
 * the caller has not already attached one, the accessible reason modal
 * (ChangeReasonService, EA finding UI-014) is opened; cancelling aborts the
 * request silently so nothing is voided unjustified. The reason travels in the
 * X-Change-Reason header, which the server requires and stamps onto the void's
 * field-change ledger row in the same transaction.
 */
export const changeReasonInterceptor: HttpInterceptorFn = (req, next) => {
  if (req.method !== 'DELETE' || req.headers.has(CHANGE_REASON_HEADER)) {
    return next(req);
  }

  const reasons = inject(ChangeReasonService);

  return from(reasons.request()).pipe(
    switchMap((reason) => {
      // Cancelled — complete without emitting; the record stays, nothing is sent.
      if (reason === null) {
        return EMPTY;
      }
      return next(req.clone({ setHeaders: { [CHANGE_REASON_HEADER]: reason } }));
    }),
  );
};
