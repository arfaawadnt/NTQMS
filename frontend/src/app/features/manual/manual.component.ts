import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { I18nService } from '../../core/i18n.service';
import { HELP_TOPICS, HelpTopic, helpGroupsInOrder, tr } from '../../core/help/help-content';
import { navIcon } from '../../core/nav-icons';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { HelpBodyComponent } from '../../shared/ui/help-body.component';

/** One rendered manual section: a sidebar group and the topics beneath it. */
interface ManualSection {
  groupKey: string;
  topics: HelpTopic[];
}

/**
 * The standalone User Manual: every page's workflow help in one searchable
 * reference, grouped exactly like the sidebar. Each entry expands to the same
 * description / workflow diagram / progress bar / usage shown in the in-page
 * help popup, so the manual and the popups never drift apart.
 */
@Component({
  selector: 'qams-manual',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, PageHeaderComponent, HelpBodyComponent],
  template: `
    <qams-page-header [title]="i18n.t('nav.manual')" [subtitle]="i18n.t('manual.subtitle')" />

    <div class="toolbar card">
      <input class="search" [value]="search()" (input)="search.set($any($event.target).value)"
             [placeholder]="i18n.t('manual.search')" />
      <span class="count">{{ matchCount() }} / {{ total }}</span>
    </div>

    @if (matchCount() === 0) {
      <p class="muted">{{ i18n.t('manual.noMatch') }}</p>
    }

    @for (section of sections(); track section.groupKey) {
      <section class="group">
        <h2 class="ghead">{{ i18n.t(section.groupKey) }}</h2>
        <div class="grid">
          @for (t of section.topics; track t.route) {
            <article class="card topic" [class.open]="expanded() === t.route">
              <button type="button" class="thead" (click)="toggle(t.route)"
                      [attr.aria-expanded]="expanded() === t.route">
                <span class="tico">
                  <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor"
                       stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                    <path [attr.d]="icon(t.icon)" />
                  </svg>
                </span>
                <span class="ttl">
                  <span class="tname">{{ i18n.t(t.titleKey) }}</span>
                  <span class="tsum">{{ text(t.summary) }}</span>
                </span>
                <span class="chev" [class.up]="expanded() === t.route" aria-hidden="true">⌄</span>
              </button>
              @if (expanded() === t.route) {
                <div class="tbody">
                  <qams-help-body [topic]="t" />
                  <a class="goto" [routerLink]="t.route">{{ i18n.t('manual.goToPage') }} →</a>
                </div>
              }
            </article>
          }
        </div>
      </section>
    }
  `,
  styles: [`
    .toolbar { display: flex; align-items: center; gap: 12px; padding: 10px 14px; margin-bottom: 16px; }
    .search { flex: 1; max-width: 360px; }
    .count { font-size: 12px; color: var(--nt-grey-m); font-variant-numeric: tabular-nums; }
    .group { margin-bottom: 1.5rem; }
    .ghead {
      font-size: 12px; font-weight: 700; text-transform: uppercase; letter-spacing: .05em;
      color: var(--nt-grey-m); margin: 0 0 .6rem; padding-inline-start: 2px;
    }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(340px, 1fr)); gap: 12px; align-items: start; }
    .topic { padding: 0; overflow: hidden; }
    .topic.open { grid-column: 1 / -1; }
    .thead {
      display: flex; align-items: center; gap: 12px; width: 100%; text-align: start;
      padding: 14px 16px; background: transparent; border: none; cursor: pointer; color: inherit;
    }
    .thead:hover { background: var(--nt-bg-grey); }
    .tico {
      flex-shrink: 0; width: 34px; height: 34px; border-radius: 8px; color: var(--nt-blue);
      background: color-mix(in srgb, var(--nt-blue) 10%, transparent);
      display: inline-flex; align-items: center; justify-content: center;
    }
    .ttl { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 2px; }
    .tname { font-size: 14px; font-weight: 700; color: var(--nt-slate); }
    .tsum {
      font-size: 12px; color: var(--nt-grey-m); line-height: 1.4;
      display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden;
    }
    .topic.open .tsum { -webkit-line-clamp: unset; }
    .chev { flex-shrink: 0; font-size: 16px; color: var(--nt-grey-m); transition: transform .2s; }
    .chev.up { transform: rotate(180deg); }
    .tbody { padding: 4px 18px 18px; border-top: 1px solid var(--nt-border); }
    .goto {
      display: inline-block; margin-top: 1rem; font-size: 12.5px; font-weight: 600;
      color: var(--nt-blue); text-decoration: none;
    }
    .goto:hover { text-decoration: underline; }
  `],
})
export class ManualComponent {
  readonly i18n = inject(I18nService);

  readonly total = HELP_TOPICS.length;
  readonly search = signal('');
  readonly expanded = signal<string>('');

  private readonly matches = computed(() => {
    const q = this.search().trim().toLowerCase();
    if (!q) { return HELP_TOPICS; }
    const lang = this.i18n.lang();
    return HELP_TOPICS.filter((t) =>
      `${this.i18n.t(t.titleKey)} ${tr(t.summary, lang)}`.toLowerCase().includes(q));
  });

  readonly matchCount = computed(() => this.matches().length);

  readonly sections = computed<ManualSection[]>(() => {
    const found = this.matches();
    return helpGroupsInOrder()
      .map((groupKey) => ({ groupKey, topics: found.filter((t) => t.groupKey === groupKey) }))
      .filter((s) => s.topics.length > 0);
  });

  toggle(route: string): void {
    this.expanded.set(this.expanded() === route ? '' : route);
  }

  icon(name: string): string { return navIcon(name); }

  text(value: Parameters<typeof tr>[0]): string { return tr(value, this.i18n.lang()); }
}
