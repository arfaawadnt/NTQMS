import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** One statistic tile: a real computed value with a label and a semantic tone. */
export interface ListStat {
  label: string;
  value: number | string;
  tone: 'blue' | 'teal' | 'green' | 'gold' | 'orange' | 'red' | 'slate';
}

/**
 * Compact statistics strip for module registers, styled as the design
 * system's KPI tiles. Values are computed by the page from the real loaded
 * list — this component only renders.
 */
@Component({
  selector: 'qams-list-stats',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="stats">
      @for (s of stats(); track s.label) {
        <div class="stat" [class]="'stat ' + s.tone">
          <div class="v">{{ s.value }}</div>
          <div class="l">{{ s.label }}</div>
        </div>
      }
    </div>
  `,
  styles: [`
    .stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(130px, 1fr)); gap: 10px; margin-bottom: 14px; }
    .stat { border-radius: var(--nt-radius-card); padding: 10px 14px; color: #fff; box-shadow: var(--nt-shadow-xs); }
    .v { font-size: 22px; font-weight: 800; line-height: 1; font-variant-numeric: tabular-nums; }
    .l { font-size: 11px; font-weight: 600; margin-top: 5px; opacity: .95; }
    .blue { background: var(--nt-blue); } .teal { background: var(--nt-teal); }
    .green { background: var(--nt-green); } .gold { background: var(--nt-gold); color: #3a2c00; }
    .orange { background: var(--nt-orange); } .red { background: var(--nt-red); }
    .slate { background: var(--nt-slate); }
  `],
})
export class ListStatsComponent {
  /** The tiles to render, in order. */
  readonly stats = input.required<readonly ListStat[]>();
}
