import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * Semicircular percentage gauge, rendered as self-contained SVG (no chart
 * library, consistent with the rest of the app and with the locked CSP).
 *
 * A null value renders an em dash and an empty track, never a zero arc: "no
 * population to measure" and "measured zero" are different findings, and a
 * gauge sitting at the floor would report the second when it means the first.
 *
 * Colour is a secondary cue only — the figure is always printed, so the gauge
 * never carries its meaning by hue alone.
 */
@Component({
  selector: 'qams-gauge',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg viewBox="0 0 120 74" role="img" [attr.aria-label]="ariaLabel()">
      <!-- track -->
      <path [attr.d]="arc" fill="none" stroke="var(--nt-filter-grey)"
            [attr.stroke-width]="thickness" stroke-linecap="round" />
      <!-- value -->
      @if (fraction() !== null) {
        <path [attr.d]="arc" fill="none" [attr.stroke]="ink()"
              [attr.stroke-width]="thickness" stroke-linecap="round"
              [attr.stroke-dasharray]="length"
              [attr.stroke-dashoffset]="length * (1 - fraction()!)" />
      }
      <text x="60" y="58" text-anchor="middle" class="v" [attr.fill]="ink()">{{ display() }}</text>
      @if (caption()) {
        <text x="60" y="70" text-anchor="middle" class="c">{{ caption() }}</text>
      }
    </svg>
  `,
  styles: [`
    :host { display: block; }
    svg { width: 100%; max-width: 200px; height: auto; }
    .v { font-size: 22px; font-weight: 800; font-variant-numeric: tabular-nums; }
    .c { font-size: 8px; fill: var(--nt-grey-m); }
  `],
})
export class GaugeComponent {
  /** Percentage 0–100, or null when there is no population to measure. */
  readonly value = input.required<number | null>();
  /** Small label under the figure (already translated). */
  readonly caption = input('');
  /** Accessible description; the value is appended automatically. */
  readonly label = input('');
  /** Suffix printed after the number. */
  readonly suffix = input('%');

  /** Semicircle from (10,60) to (110,60), radius 50. */
  readonly arc = 'M 10 60 A 50 50 0 0 1 110 60';
  readonly length = Math.PI * 50;
  readonly thickness = 10;

  readonly fraction = computed(() => {
    const value = this.value();
    return value === null ? null : Math.max(0, Math.min(1, value / 100));
  });

  readonly display = computed(() => {
    const value = this.value();
    return value === null ? '—' : `${Math.round(value)}${this.suffix()}`;
  });

  /**
   * Bands use the readable ink ramp, not the saturated fill tones — this mark is
   * read as a value, and the fill tones fail contrast as a categorical set.
   */
  readonly ink = computed(() => {
    const value = this.value();
    if (value === null) { return 'var(--nt-ink-neutral)'; }
    if (value >= 90) { return 'var(--nt-ink-ok)'; }
    if (value >= 75) { return 'var(--nt-ink-teal)'; }
    if (value >= 50) { return 'var(--nt-ink-warn)'; }
    return 'var(--nt-ink-crit)';
  });

  readonly ariaLabel = computed(() =>
    `${this.label() || this.caption()}: ${this.display()}`);
}
