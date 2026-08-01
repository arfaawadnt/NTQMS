import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { CategoryCount } from '../../core/models';

/** One pre-projected donut arc. */
interface Segment {
  label: string;
  count: number;
  percent: number;
  offset: number;
  ink: string;
}

/**
 * Category-split donut, self-contained SVG. Segments are drawn as dash offsets on
 * a single circle rather than as arc paths — fewer trig edge cases, and a single
 * category correctly renders a full ring instead of a degenerate arc.
 *
 * Every segment is also listed in the legend with its count, so the chart never
 * relies on colour alone to convey which slice is which.
 */
@Component({
  selector: 'qams-donut-chart',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (total() === 0) {
      <p class="muted empty">{{ emptyText() }}</p>
    } @else {
      <div class="wrap">
        <svg viewBox="0 0 42 42" role="img" [attr.aria-label]="ariaLabel()">
          <circle cx="21" cy="21" [attr.r]="radius" fill="none"
                  stroke="var(--nt-filter-grey)" stroke-width="5" />
          @for (s of segments(); track s.label) {
            <circle cx="21" cy="21" [attr.r]="radius" fill="none"
                    [attr.stroke]="s.ink" stroke-width="5"
                    [attr.stroke-dasharray]="s.percent + ' ' + (circumference - s.percent)"
                    [attr.stroke-dashoffset]="-s.offset"
                    transform="rotate(-90 21 21)">
              <title>{{ s.label }}: {{ s.count }}</title>
            </circle>
          }
          <text x="21" y="22.5" text-anchor="middle" class="tot">{{ total() }}</text>
        </svg>
        <ul class="legend">
          @for (s of segments(); track s.label) {
            <li>
              <i [style.background]="s.ink" aria-hidden="true"></i>
              <span class="l">{{ s.label }}</span>
              <b>{{ s.count }}</b>
            </li>
          }
        </ul>
      </div>
    }
  `,
  styles: [`
    :host { display: block; }
    .wrap { display: flex; gap: 16px; align-items: center; flex-wrap: wrap; }
    svg { width: 132px; height: 132px; flex: none; }
    .tot { font-size: 8px; font-weight: 800; fill: var(--nt-navy); font-variant-numeric: tabular-nums; }
    .legend { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column;
              gap: 5px; font-size: 12px; min-width: 150px; }
    .legend li { display: grid; grid-template-columns: 10px 1fr auto; gap: 8px; align-items: center; }
    .legend i { width: 10px; height: 10px; border-radius: 2px; }
    .legend .l { color: var(--nt-slate); overflow-wrap: anywhere; }
    .legend b { font-variant-numeric: tabular-nums; }
    .empty { margin: 0; }
  `],
})
export class DonutChartComponent {
  readonly data = input.required<readonly CategoryCount[]>();
  readonly label = input('');
  /** Shown when there is nothing to plot (already translated). */
  readonly emptyText = input('No data');

  readonly radius = 15.915;
  readonly circumference = 100;

  /** The readable ink ramp, cycled — these arcs are read against their legend. */
  private readonly inks = [
    'var(--nt-ink-info)', 'var(--nt-ink-teal)', 'var(--nt-ink-ok)',
    'var(--nt-ink-warn)', 'var(--nt-ink-serious)', 'var(--nt-ink-crit)',
    'var(--nt-ink-neutral)',
  ];

  readonly total = computed(() => this.data().reduce((sum, d) => sum + d.count, 0));

  readonly segments = computed<Segment[]>(() => {
    const total = this.total();
    if (total === 0) { return []; }
    let offset = 0;
    return this.data().map((d, i) => {
      const percent = (d.count / total) * 100;
      const segment: Segment = {
        label: d.label,
        count: d.count,
        percent,
        offset,
        ink: this.inks[i % this.inks.length],
      };
      offset += percent;
      return segment;
    });
  });

  readonly ariaLabel = computed(() =>
    `${this.label()}: ${this.segments().map((s) => `${s.label} ${s.count}`).join(', ')}`);
}
