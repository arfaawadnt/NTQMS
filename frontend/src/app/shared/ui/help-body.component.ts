import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { I18nService } from '../../core/i18n.service';
import { HelpTopic, tr } from '../../core/help/help-content';

/**
 * Renders one help topic: a plain-language description, the page's workflow as
 * a segmented progress bar plus a numbered step diagram, and step-by-step usage
 * guidance. Shared by the in-page help popup and the standalone User Manual so
 * both always show identical, single-sourced content.
 */
@Component({
  selector: 'qams-help-body',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (topic(); as t) {
      <p class="desc">{{ text(t.summary) }}</p>

      @if (t.steps.length > 0) {
        <h4 class="sec">{{ i18n.t('help.workflow') }}</h4>

        <!-- Progress bar: every stage as an equal, numbered segment. -->
        <div class="progress" role="img" [attr.aria-label]="i18n.t('help.workflow')">
          @for (s of t.steps; track $index) {
            <div class="seg" [style.width.%]="segWidth()">
              <span class="segnum">{{ $index + 1 }}</span>
              <span class="seglbl">{{ text(s.label) }}</span>
            </div>
          }
        </div>

        <!-- Workflow diagram: numbered nodes with connectors and detail. -->
        <ol class="flow">
          @for (s of t.steps; track $index; let last = $last) {
            <li class="node">
              <span class="num">{{ $index + 1 }}</span>
              <div class="body">
                <div class="lbl">{{ text(s.label) }}</div>
                <div class="dt">{{ text(s.detail) }}</div>
              </div>
              @if (!last) { <span class="conn" aria-hidden="true"></span> }
            </li>
          }
        </ol>
      }

      <h4 class="sec">{{ i18n.t('help.howto') }}</h4>
      <ol class="usage">
        @for (u of t.usage; track $index) { <li>{{ text(u) }}</li> }
      </ol>
    }
  `,
  styles: [`
    .desc { margin: 0 0 1rem; font-size: 13px; line-height: 1.6; color: var(--nt-slate); }
    .sec {
      margin: 1.25rem 0 .6rem; font-size: 12px; font-weight: 700; text-transform: uppercase;
      letter-spacing: .04em; color: var(--nt-grey-m);
    }
    .progress {
      display: flex; gap: 3px; margin-bottom: 1rem; width: 100%;
    }
    .seg {
      background: color-mix(in srgb, var(--nt-teal) 16%, transparent);
      border-bottom: 3px solid var(--nt-teal); border-radius: 5px 5px 0 0;
      padding: 7px 8px; min-width: 0; display: flex; flex-direction: column; gap: 3px;
    }
    .segnum {
      width: 18px; height: 18px; border-radius: 50%; background: var(--nt-teal); color: #fff;
      font-size: 10px; font-weight: 700; display: inline-flex; align-items: center; justify-content: center;
    }
    .seglbl { font-size: 10.5px; font-weight: 600; color: var(--nt-slate); line-height: 1.25; }
    .flow { list-style: none; margin: 0; padding: 0; }
    .node { position: relative; display: flex; gap: 12px; padding-bottom: 14px; }
    .num {
      flex-shrink: 0; width: 26px; height: 26px; border-radius: 50%; z-index: 1;
      background: var(--nt-blue); color: #fff; font-size: 12px; font-weight: 700;
      display: inline-flex; align-items: center; justify-content: center;
    }
    .conn {
      position: absolute; inset-inline-start: 12px; top: 26px; bottom: 0; width: 2px;
      background: var(--nt-border);
    }
    .body { padding-top: 3px; }
    .lbl { font-size: 13px; font-weight: 700; color: var(--nt-slate); }
    .dt { font-size: 12px; line-height: 1.5; color: var(--nt-grey-m); margin-top: 1px; }
    .usage { margin: 0; padding-inline-start: 1.15rem; }
    .usage li { font-size: 12.5px; line-height: 1.55; color: var(--nt-slate); margin-bottom: .5rem; }
    @media (max-width: 520px) { .seglbl { display: none; } }
  `],
})
export class HelpBodyComponent {
  readonly i18n = inject(I18nService);

  /** The topic to render. */
  readonly topic = input.required<HelpTopic>();

  readonly segWidth = computed(() => {
    const n = this.topic().steps.length;
    return n > 0 ? 100 / n : 100;
  });

  /** Localize a piece of help text to the active language. */
  text(value: Parameters<typeof tr>[0]): string {
    return tr(value, this.i18n.lang());
  }
}
