import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * Workflow stepper per the QAMS Design System: the record's canonical path as
 * dot-connected steps — completed steps teal, the current step highlighted
 * blue, upcoming steps grey. A status outside the canonical path (Rejected,
 * Revoked, Suspended, OutOfService…) renders as a terminal red badge after the
 * last completed step instead of pretending the flow continued.
 */
@Component({
  selector: 'qams-workflow-stepper',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="stepper card">
      @for (step of steps(); track step; let i = $index; let last = $last) {
        <div class="step"
             [class.done]="i < activeIndex()"
             [class.current]="i === activeIndex() && !offPath()">
          <span class="dot">@if (i < activeIndex()) { ✓ }</span>
          <span class="lbl">{{ pretty(step) }}</span>
        </div>
        @if (!last) { <span class="conn" [class.done]="i < activeIndex()"></span> }
      }
      @if (offPath()) {
        <span class="conn"></span>
        <div class="step off"><span class="dot">✕</span><span class="lbl">{{ pretty(current()) }}</span></div>
      }
    </div>
  `,
  styles: [`
    .stepper {
      display: flex; align-items: center; gap: 6px; flex-wrap: wrap;
      padding: 12px 16px; margin-bottom: 1rem; overflow-x: auto;
    }
    .step { display: flex; align-items: center; gap: 7px; white-space: nowrap; }
    .dot {
      width: 20px; height: 20px; border-radius: 50%; flex-shrink: 0;
      border: 2px solid var(--nt-border); background: #fff; color: #fff;
      display: inline-flex; align-items: center; justify-content: center;
      font-size: 11px; font-weight: 700;
    }
    .lbl { font-size: 12px; font-weight: 500; color: var(--nt-grey-m); }
    .step.done .dot { background: var(--nt-teal); border-color: var(--nt-teal); }
    .step.done .lbl { color: var(--nt-slate); }
    .step.current .dot { border-color: var(--nt-blue); box-shadow: 0 0 0 3px rgba(0, 119, 194, .18); background: var(--nt-blue); }
    .step.current .lbl { color: var(--nt-blue); font-weight: 700; }
    .step.off .dot { background: var(--nt-red); border-color: var(--nt-red); }
    .step.off .lbl { color: var(--nt-red); font-weight: 700; }
    .conn { flex: 0 0 22px; height: 2px; background: var(--nt-border); border-radius: 2px; }
    .conn.done { background: var(--nt-teal); }
  `],
})
export class WorkflowStepperComponent {
  /** The canonical (happy-path) status sequence, in backend enum spelling. */
  readonly steps = input.required<readonly string[]>();
  /** The record's current status (may be outside the canonical path). */
  readonly current = input.required<string>();

  /**
   * Index of the current status on the path. Off-path states render the whole
   * path neutral (the status alone cannot tell how far the record got before
   * being e.g. rejected — claiming progress would be dishonest).
   */
  readonly activeIndex = computed(() => Math.max(this.steps().indexOf(this.current()), 0));

  /** True when the current status is not on the canonical path (terminal-negative). */
  readonly offPath = computed(() => !this.steps().includes(this.current()));

  /** "PendingVerification" → "Pending Verification", "Rca" → "RCA". */
  pretty(status: string): string {
    if (status.length <= 3) { return status.toUpperCase(); }
    return status.replace(/([a-z])([A-Z])/g, '$1 $2');
  }
}
