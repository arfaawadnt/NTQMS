import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Page-title bar per the QAMS Design System: 18px/700 slate H1 with a teal
 * accent bar, optional helper subtitle, and a trailing action slot.
 */
@Component({
  selector: 'qams-page-header',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="header">
      <div class="titleblock">
        <h1>{{ title() }}</h1>
        @if (subtitle()) { <p class="muted">{{ subtitle() }}</p> }
      </div>
      <div class="actions"><ng-content /></div>
    </div>
  `,
  styles: [`
    .header {
      display: flex; align-items: center; justify-content: space-between; gap: 16px;
      background: var(--nt-surface); border: 1px solid var(--nt-border);
      border-radius: var(--nt-radius-card); box-shadow: var(--nt-shadow-xs);
      padding: 12px 16px; margin-bottom: 16px;
    }
    .titleblock { border-inline-start: 3px solid var(--nt-teal); padding-inline-start: 12px; }
    h1 { margin: 0; font-size: 18px; font-weight: 700; color: var(--nt-slate); line-height: 1.3; }
    p { margin: 2px 0 0; font-size: 12px; }
    .actions { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
  `],
})
export class PageHeaderComponent {
  /** Main heading text. */
  readonly title = input.required<string>();
  /** Optional secondary line under the heading. */
  readonly subtitle = input<string>('');
}
