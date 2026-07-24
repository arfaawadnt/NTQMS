import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { QcRun } from '../../core/models';

/** One plotted point of the Levey-Jennings chart (pre-projected to SVG space). */
interface LjPoint {
  x: number;
  y: number;
  outcome: string;
  title: string;
}

/**
 * Levey-Jennings control chart rendered as a self-contained SVG (no chart
 * library): ±1/2/3 SD guide lines around the target mean, run points joined
 * chronologically and coloured by their stored Westgard outcome (in-control /
 * warning / out-of-control). Values beyond ±4 SD are clamped to the frame.
 */
@Component({
  selector: 'qams-levey-jennings',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg [attr.viewBox]="'0 0 ' + width + ' ' + height" preserveAspectRatio="none" role="img" aria-label="Levey-Jennings chart">
      <!-- SD guide lines and labels -->
      @for (g of guides; track g.sd) {
        <line [attr.x1]="padX" [attr.x2]="width - 8" [attr.y1]="yFor(g.sd)" [attr.y2]="yFor(g.sd)"
              [attr.stroke]="g.sd === 0 ? '#3B4658' : g.colour" [attr.stroke-dasharray]="g.sd === 0 ? null : '4 4'" stroke-width="1" />
        <text [attr.x]="4" [attr.y]="yFor(g.sd) + 4" font-size="10" fill="#797979">{{ g.label }}</text>
      }
      <!-- run polyline -->
      @if (points().length > 1) {
        <polyline [attr.points]="polyline()" fill="none" stroke="#0077C2" stroke-width="1.5" />
      }
      <!-- run points -->
      @for (p of points(); track p.x) {
        <circle [attr.cx]="p.x" [attr.cy]="p.y" r="4" [attr.fill]="colourFor(p.outcome)" stroke="#fff" stroke-width="1">
          <title>{{ p.title }}</title>
        </circle>
      }
    </svg>
  `,
  styles: [`
    :host { display: block; }
    svg { width: 100%; height: 240px; background: #fff; border: 1px solid var(--nt-border); border-radius: var(--nt-radius-card); }
  `],
})
export class LeveyJenningsChartComponent {
  /** Runs in chronological order (oldest first). */
  readonly runs = input.required<QcRun[]>();
  /** Profile target mean. */
  readonly mean = input.required<number>();
  /** Profile target SD (positive). */
  readonly sd = input.required<number>();

  readonly width = 760;
  readonly height = 240;
  readonly padX = 46;
  readonly padY = 14;

  /** Guide lines at the mean and ±1/2/3 SD. */
  readonly guides = [
    { sd: 3, label: '+3 SD', colour: '#DC3545' },
    { sd: 2, label: '+2 SD', colour: '#ECB71E' },
    { sd: 1, label: '+1 SD', colour: '#C1C1C6' },
    { sd: 0, label: 'Mean', colour: '#3B4658' },
    { sd: -1, label: '-1 SD', colour: '#C1C1C6' },
    { sd: -2, label: '-2 SD', colour: '#ECB71E' },
    { sd: -3, label: '-3 SD', colour: '#DC3545' },
  ];

  readonly points = computed<LjPoint[]>(() => {
    const runs = this.runs();
    if (runs.length === 0) { return []; }
    const step = (this.width - this.padX - 16) / Math.max(runs.length - 1, 1);
    return runs.map((r, i) => ({
      x: this.padX + i * step,
      y: this.yFor(this.clamp(r.zScore)),
      outcome: r.outcome,
      title: `${r.value} (z=${r.zScore}) — ${r.outcome}${r.violatedRules ? ' · ' + r.violatedRules : ''}`,
    }));
  });

  readonly polyline = computed(() => this.points().map((p) => `${p.x},${p.y}`).join(' '));

  /** Maps an SD multiple (z) to a vertical pixel position; frame spans ±4 SD. */
  yFor(z: number): number {
    const usable = this.height - 2 * this.padY;
    return this.padY + ((4 - z) / 8) * usable;
  }

  colourFor(outcome: string): string {
    switch (outcome) {
      case 'OutOfControl': return '#DC3545';
      case 'Warning': return '#ECB71E';
      default: return '#188038';
    }
  }

  private clamp(z: number): number { return Math.max(-4, Math.min(4, z)); }
}
