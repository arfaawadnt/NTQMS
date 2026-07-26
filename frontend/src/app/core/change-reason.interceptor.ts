import { HttpInterceptorFn } from '@angular/common/http';
import { EMPTY } from 'rxjs';

const CHANGE_REASON_HEADER = 'X-Change-Reason';

/**
 * 21 CFR Part 11 §11.10(e) / ALCOA+: every DELETE in this system voids a piece
 * of analytical evidence, which may never happen without a recorded reason. If
 * the caller has not already attached one, the operator is prompted; cancelling
 * (or leaving it blank) aborts the request so nothing is voided unjustified. The
 * reason travels in the X-Change-Reason header, which the server requires and
 * stamps onto the void's field-change ledger row in the same transaction.
 */
export const changeReasonInterceptor: HttpInterceptorFn = (req, next) => {
  if (req.method !== 'DELETE' || req.headers.has(CHANGE_REASON_HEADER)) {
    return next(req);
  }

  const reason = window.prompt(
    'Removing this record is audited (21 CFR Part 11). Enter the reason for voiding it:',
  );

  // Cancelled or blank — abort silently; the record stays and nothing is sent.
  if (reason === null || reason.trim() === '') {
    return EMPTY;
  }

  return next(req.clone({ setHeaders: { [CHANGE_REASON_HEADER]: reason.trim() } }));
};
