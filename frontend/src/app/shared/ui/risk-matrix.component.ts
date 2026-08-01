import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RiskMatrixCell } from '../../core/models';

/** One rendered cell of the grid. */
interface Cell {
  likelihood: number;
  impact: number;
  count: number;
  score: number;
  band: 'low' | 'moderate' | 'high' | 'extreme';
}

/**
 * 5×5 likelihood × impact heat grid. The 1–5 scale is fixed by the domain
 * (RiskItem requires both to be assessed 1–5), so the grid is drawn at full
 * extent rather than sized to the data — a matrix that shrank to fit would
 * misrepresent where the plotted risks sit on the scale.
 *
 * Banding follows the domain's own residual threshold (>12 is high), so this
 * grid and the "high or extreme" KPI cannot disagree about what counts as high.
 * Every cell shows its count, so severity is never carried by colour alone.
 */
@Component({
  selector: 'qams-risk-matrix',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="matrix" role="img" [attr.aria-label]="ariaLabel()">
      <div class="yaxis">{{ impactLabel() }}</div>
      <div class="grid">
        @for (c of cells(); track c.likelihood + '-' + c.impact) {
          <div class="cell" [class]="c.band" [class.empty]="c.count === 0"
               [title]="impactLabel() + ' ' + c.impact + ' × ' + likelihoodLabel() + ' ' + c.likelihood
                        + ' — ' + c.count + ' (score ' + c.score + ')'">
            {{ c.count || '' }}
          </div>
        }
      </div>
      <div class="xaxis">{{ likelihoodLabel() }}</div>
    </div>
  `,
  styles: [`
    :host { display: block; }
    .matrix { display: grid; grid-template-columns: 18px 1fr; grid-template-rows: 1fr 18px;
              gap: 6px; max-width: 300px; }
    .yaxis { grid-column: 1; grid-row: 1; writing-mode: vertical-rl; transform: rotate(180deg);
             text-align: center; font-size: 10px; color: var(--nt-grey-m); }
    .grid { grid-column: 2; grid-row: 1; display: grid; grid-template-columns: repeat(5, 1fr);
            grid-auto-rows: 1fr; gap: 3px; }
    .xaxis { grid-column: 2; grid-row: 2; text-align: center; font-size: 10px; color: var(--nt-grey-m); }
    .cell { aspect-ratio: 1; display: grid; place-items: center; border-radius: 3px;
            font-size: 12px; font-weight: 700; font-variant-numeric: tabular-nums;
            color: #fff; }
    .cell.low { background: var(--nt-ink-ok); }
    .cell.moderate { background: var(--nt-ink-warn); }
    .cell.high { background: var(--nt-ink-serious); }
    .cell.extreme { background: var(--nt-ink-crit); }
    /* An unoccupied cell keeps its band as a wash, so the scale stays legible
       without implying a risk sits there. */
    .cell.empty { opacity: 0.16; }
  `],
})
export class RiskMatrixComponent {
  readonly data = input.required<readonly RiskMatrixCell[]>();
  readonly likelihoodLabel = input('Likelihood');
  readonly impactLabel = input('Impact');

  /** The domain's residual high-risk threshold. */
  private readonly highThreshold = 12;

  readonly cells = computed<Cell[]>(() => {
    const counts = new Map<string, number>();
    for (const cell of this.data()) {
      counts.set(`${cell.likelihood}-${cell.impact}`, cell.count);
    }

    const cells: Cell[] = [];
    // Impact descends so the most severe row sits at the top, as a risk matrix
    // is conventionally read.
    for (let impact = 5; impact >= 1; impact--) {
      for (let likelihood = 1; likelihood <= 5; likelihood++) {
        const score = likelihood * impact;
        cells.push({
          likelihood,
          impact,
          count: counts.get(`${likelihood}-${impact}`) ?? 0,
          score,
          band: score > this.highThreshold ? (score >= 20 ? 'extreme' : 'high')
            : score >= 6 ? 'moderate' : 'low',
        });
      }
    }

    return cells;
  });

  readonly ariaLabel = computed(() => {
    const occupied = this.cells().filter((c) => c.count > 0);
    if (occupied.length === 0) { return 'Risk matrix: no risks plotted'; }
    return 'Risk matrix: ' + occupied
      .map((c) => `${c.count} at likelihood ${c.likelihood} impact ${c.impact}`)
      .join(', ');
  });
}
