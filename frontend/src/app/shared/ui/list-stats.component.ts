import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** One statistic tile: a real computed value with a label and a semantic tone. */
export interface ListStat {
  label: string;
  value: number | string;
  tone: 'blue' | 'teal' | 'green' | 'gold' | 'orange' | 'red' | 'slate';
  /**
   * The whole this value is a part of, so the tile can show a proportion
   * ("5 of 17") instead of a bare count. Omit it when no honest denominator
   * exists — the tile then renders without a meter rather than inventing a
   * ratio. Overrides {@link ListStatsComponent.ratioFromFirst}.
   */
  of?: number;
}

/** A tile prepared for rendering: the meter is resolved, never guessed. */
interface RenderedStat extends ListStat {
  /** Percentage of the whole, or null when this tile has no meter. */
  percent: number | null;
  /** The resolved denominator, for the caption. */
  whole: number | null;
}

/**
 * Statistics strip for module registers, rendered as proportion tiles: a value
 * in text ink, a label, and — where a real denominator exists — a thin meter
 * whose fill carries the tone and whose track is a light step of the same hue.
 *
 * The tone tokens are deliberately NOT used as tile backgrounds. As a
 * categorical set they fail the palette checks (gold 1.80:1 and teal 2.58:1
 * against a white surface; red↔orange only ΔE 8.9 apart for normal vision), and
 * a saturated block also forces white text and carries severity by colour alone.
 * Here the tone appears as a meter fill and a value ink, both of which are
 * darkened steps that clear 4.5:1, and the proportion carries the magnitude.
 *
 * Values are computed by the page from the real loaded list — this component
 * only renders.
 */
@Component({
  selector: 'qams-list-stats',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="stats">
      @for (s of rendered(); track s.label) {
        <div class="stat" [class]="'stat ' + s.tone" [class.zero]="s.value === 0">
          <div class="v">{{ s.value }}</div>
          <div class="l">{{ s.label }}</div>
          @if (s.percent !== null) {
            <div class="meter" role="img"
                 [attr.aria-label]="s.label + ': ' + s.value + ' of ' + s.whole">
              <div class="track"><div class="fill" [style.width.%]="s.percent"></div></div>
            </div>
            <div class="cap">{{ s.value }} / {{ s.whole }}</div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(146px, 1fr)); gap: 10px; margin-bottom: 14px; }
    .stat {
      background: var(--nt-surface); border: 1px solid var(--nt-border);
      border-radius: var(--nt-radius-input); padding: 11px 14px 12px;
      box-shadow: var(--nt-shadow-xs);
    }
    /* Proportional figures on purpose: tabular-nums makes a display-size number
       look loose. Tabular is for columns that must align vertically. */
    .v { font-size: 21px; font-weight: 800; line-height: 1.1; color: var(--nt-navy); }
    .l { font-size: 11.5px; font-weight: 600; color: var(--nt-slate); margin-top: 4px; }

    .meter { margin-top: 9px; }
    .track { height: 5px; border-radius: 999px; background: color-mix(in srgb, currentColor 18%, transparent); }
    .fill { height: 100%; border-radius: 999px; background: currentColor; min-width: 2px; }
    .cap { font-size: 10.5px; font-weight: 600; color: var(--nt-slate); margin-top: 5px; font-variant-numeric: tabular-nums; }

    /* currentColor carries the tone, so the meter needs no per-tone rules. */
    .stat.blue   { color: var(--nt-ink-info); }
    .stat.teal   { color: var(--nt-ink-teal); }
    .stat.green  { color: var(--nt-ink-ok); }
    .stat.gold   { color: var(--nt-ink-warn); }
    .stat.orange { color: var(--nt-ink-serious); }
    .stat.red    { color: var(--nt-ink-crit); }
    .stat.slate  { color: var(--nt-ink-neutral); }
    /* The VALUE stays text ink — the meter fill is what carries the tone. A tile
       reading zero is de-emphasised so a screenful of zeros recedes. */
    .stat.zero .v { color: var(--nt-slate); font-weight: 700; }
    .stat.zero .track { background: color-mix(in srgb, var(--nt-slate) 14%, transparent); }
    .stat.zero .fill { background: var(--nt-slate); }
  `],
})
export class ListStatsComponent {
  /** The tiles to render, in order. */
  readonly stats = input.required<readonly ListStat[]>();

  /**
   * Opt in where the FIRST tile is the register's total, so every later tile is
   * a part of it. Off by default: a page must assert that its first tile really
   * is the whole, because a wrong denominator is worse than none.
   */
  readonly ratioFromFirst = input(false, { transform: (v: boolean | string) => v !== false && v !== 'false' });

  protected readonly rendered = computed<RenderedStat[]>(() => {
    const stats = this.stats();
    const first = stats[0];
    const fallbackWhole = this.ratioFromFirst() && typeof first?.value === 'number' ? first.value : null;

    return stats.map((s, index) => {
      // The total tile is the whole; it gets no meter of its own.
      const isWhole = this.ratioFromFirst() && index === 0;
      const whole = s.of ?? (isWhole ? null : fallbackWhole);

      const meterable =
        typeof s.value === 'number' &&
        whole !== null && Number.isFinite(whole) && whole > 0 &&
        s.value >= 0 && s.value <= whole;

      return {
        ...s,
        whole: meterable ? whole : null,
        percent: meterable ? Math.round(((s.value as number) / whole) * 100) : null,
      };
    });
  });
}
