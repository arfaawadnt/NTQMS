import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter, map } from 'rxjs';
import { I18nService } from '../../core/i18n.service';
import { HelpService } from '../../core/help/help.service';
import { helpTopicForUrl } from '../../core/help/help-content';
import { navIcon } from '../../core/nav-icons';

/**
 * Page-title bar per the QAMS Design System: 18px/700 slate H1 with a teal
 * accent bar, optional helper subtitle, and a trailing action slot. When the
 * current route has a registered help topic, a ? icon appears that opens the
 * page's workflow user manual popup.
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
      <div class="actions">
        <ng-content />
        @if (topic(); as t) {
          <button type="button" class="help" (click)="help.open(t)"
                  [attr.aria-label]="i18n.t('help.title')" [title]="i18n.t('help.title')">
            <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor"
                 stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <path [attr.d]="helpIcon" />
            </svg>
          </button>
        }
      </div>
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
    .help {
      display: inline-flex; align-items: center; justify-content: center; width: 34px; height: 34px;
      padding: 0; border: 1px solid var(--nt-border); border-radius: 8px; background: var(--nt-surface);
      color: var(--nt-grey-m); cursor: pointer; transition: color .15s, border-color .15s, background .15s;
    }
    .help:hover { color: var(--nt-blue); border-color: var(--nt-blue); background: color-mix(in srgb, var(--nt-blue) 8%, transparent); }
  `],
})
export class PageHeaderComponent {
  readonly help = inject(HelpService);
  readonly i18n = inject(I18nService);
  private readonly router = inject(Router);

  /** Main heading text. */
  readonly title = input.required<string>();
  /** Optional secondary line under the heading. */
  readonly subtitle = input<string>('');

  readonly helpIcon = navIcon('help');

  private readonly url = toSignal(
    this.router.events.pipe(filter((e) => e instanceof NavigationEnd), map(() => this.router.url)),
    { initialValue: this.router.url },
  );

  /** The help topic for the current route, if one is registered. */
  readonly topic = computed(() => helpTopicForUrl(this.url()));
}
