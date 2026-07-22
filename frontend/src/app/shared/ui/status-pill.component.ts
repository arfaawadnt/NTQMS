import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * Renders a workflow status as a coloured pill. Terminal-positive states
 * (closed/published/authorized/approved/active/satisfactory) read green;
 * terminal-negative (rejected/revoked/suspended/out…/unsatisfactory) read red;
 * everything else is treated as in-progress (amber).
 */
@Component({
  selector: 'qams-status-pill',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="pill" [class.ok]="tone() === 'ok'" [class.warn]="tone() === 'warn'" [class.danger]="tone() === 'danger'">{{ status() }}</span>`,
})
export class StatusPillComponent {
  /** The raw backend status string (e.g. "Closed", "ActionPlan"). */
  readonly status = input.required<string>();

  private static readonly POSITIVE = ['closed', 'published', 'authorized', 'approved', 'active', 'satisfactory', 'signedoff', 'sent'];
  private static readonly NEGATIVE = ['rejected', 'revoked', 'suspended', 'outofservice', 'unsatisfactory', 'failed', 'disposed', 'obsolete'];

  /** Resolved colour tone for the current status. */
  readonly tone = computed<'ok' | 'warn' | 'danger'>(() => {
    const s = this.status().toLowerCase();
    if (StatusPillComponent.POSITIVE.includes(s)) { return 'ok'; }
    if (StatusPillComponent.NEGATIVE.includes(s)) { return 'danger'; }
    return 'warn';
  });
}
