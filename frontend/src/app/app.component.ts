import { Component, effect, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { I18nService } from './core/i18n.service';

@Component({
    selector: 'qams-root',
    imports: [RouterOutlet],
    template: '<router-outlet />'
})
export class AppComponent {
  private readonly i18n = inject(I18nService);

  constructor() {
    // Keep the document direction/lang in sync with the chosen language (RTL for Arabic).
    effect(() => {
      const lang = this.i18n.lang();
      document.documentElement.lang = lang;
      document.documentElement.dir = this.i18n.isRtl() ? 'rtl' : 'ltr';
    });
  }
}
