import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { CategoryCount } from '../../core/models';

/**
 * Horizontal category bars, scaled to the largest value in the set. CSS rather
 * than SVG: the bars are simple rectangles that must wrap and reflow with their
 * labels, which flexbox does better than a fixed viewBox.
 *
 * Bars are sorted as supplied — the caller decides whether the order is
 * frequency (Pareto) or a fixed domain order, because re-sorting a fixed domain
 * would scramble a sequence the reader expects.
 */
@Component({
  selector: 'qams-bar-chart',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (data().length === 0) {
      <p class="muted empty">{{ emptyText() }}</p>
    } @else {
      <div class="bars" role="img" [attr.aria-label]="ariaLabel()">
        @for (d of data(); track d.label) {
          <div class="row">
            <span class="lbl" [title]="d.label">{{ d.label }}</span>
            <div class="track">
              <div class="fill" [style.width.%]="width(d)"></div>
            </div>
            <span class="val">{{ d.count }}</span>
          </div>
        }
      </div>
    }
  `,
  styles: [`
    :host { display: block; }
    .bars { display: flex; flex-direction: column; gap: 7px; }
    .row { display: grid; grid-template-columns: minmax(80px, 130px) 1fr 36px;
           gap: 10px; align-items: center; font-size: 12px; }
    .lbl { color: var(--nt-slate); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .track { background: var(--nt-filter-grey); border-radius: 3px; height: 16px; }
    .fill { background: var(--nt-ink-info); height: 100%; border-radius: 3px; min-width: 2px; }
    .val { text-align: end; font-weight: 700; font-variant-numeric: tabular-nums; color: var(--nt-navy); }
    .empty { margin: 0; }
  `],
})
export class BarChartComponent {
  readonly data = input.required<readonly CategoryCount[]>();
  readonly label = input('');
  readonly emptyText = input('No data');

  private readonly max = computed(() =>
    Math.max(...this.data().map((d) => d.count), 1));

  width(bucket: CategoryCount): number {
    return (bucket.count / this.max()) * 100;
  }

  readonly ariaLabel = computed(() =>
    `${this.label()}: ${this.data().map((d) => `${d.label} ${d.count}`).join(', ')}`);
}
