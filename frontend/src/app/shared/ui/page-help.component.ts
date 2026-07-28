import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { I18nService } from '../../core/i18n.service';
import { HelpService } from '../../core/help/help.service';
import { DrawerComponent } from './drawer.component';
import { HelpBodyComponent } from './help-body.component';

/**
 * Global help popup, rendered once in the shell. It slides in the workflow user
 * manual for whichever page opened it (via the page-header ? icon → HelpService),
 * and links through to the full User Manual module.
 */
@Component({
    selector: 'qams-page-help',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [RouterLink, DrawerComponent, HelpBodyComponent],
    template: `
    <qams-drawer
      [open]="!!help.topic()"
      [title]="title()"
      width="560px"
      (closed)="help.close()">
      @if (help.topic(); as t) {
        <qams-help-body [topic]="t" />
        <a class="all" routerLink="/manual" (click)="help.close()">{{ i18n.t('help.openManual') }} →</a>
      }
    </qams-drawer>
  `,
    styles: [`
    .all {
      display: inline-block; margin-top: 1.25rem; font-size: 12.5px; font-weight: 600;
      color: var(--nt-blue); text-decoration: none;
    }
    .all:hover { text-decoration: underline; }
  `]
})
export class PageHelpComponent {
  readonly help = inject(HelpService);
  readonly i18n = inject(I18nService);

  title(): string {
    const t = this.help.topic();
    return t ? `${this.i18n.t(t.titleKey)} — ${this.i18n.t('help.title')}` : this.i18n.t('help.title');
  }
}
