import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Reusable page title + optional action slot, styled to the design system. */
@Component({
  selector: 'qams-page-header',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="header">
      <div>
        <h1>{{ title() }}</h1>
        @if (subtitle()) { <p class="muted">{{ subtitle() }}</p> }
      </div>
      <div class="actions"><ng-content /></div>
    </div>
  `,
  styles: [`
    .header { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; margin-bottom: 1rem; }
    .actions { display: flex; gap: .6rem; align-items: center; }
    p { margin: .1rem 0 0; }
  `],
})
export class PageHeaderComponent {
  /** Main heading text. */
  readonly title = input.required<string>();
  /** Optional secondary line under the heading. */
  readonly subtitle = input<string>('');
}
