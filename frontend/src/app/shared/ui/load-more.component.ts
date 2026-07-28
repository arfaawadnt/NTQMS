import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { I18nService } from '../../core/i18n.service';

/**
 * Shared list-pager footer over the API-004 pagination envelope (backlog R-3):
 * a polite live "showing X of Y" count plus a Load-more button that appears
 * only while further pages exist and is disabled during an in-flight fetch.
 * Purely presentational — the owning facade appends the next page.
 */
@Component({
  selector: 'qams-load-more',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pager">
      <p class="count" aria-live="polite">{{ countText() }}</p>
      @if (hasMore()) {
        <button type="button" class="secondary" [disabled]="loading()" (click)="more.emit()">
          {{ i18n.t('common.loadMore') }}
        </button>
      }
    </div>
  `,
  styles: [`
    .pager { display: flex; flex-direction: column; align-items: center; gap: 8px; padding: 12px 0 4px; }
    .count { margin: 0; font-size: 12px; color: var(--nt-grey-m); font-variant-numeric: tabular-nums; }
    button { width: auto; }
  `],
})
export class LoadMoreComponent {
  readonly i18n = inject(I18nService);

  /** How many records are loaded in the list right now. */
  readonly shown = input.required<number>();
  /** Total matching records on the server. */
  readonly total = input.required<number>();
  /** True while more pages exist beyond the loaded slice. */
  readonly hasMore = input.required<boolean>();
  /** True while a page fetch is in flight (disables the button). */
  readonly loading = input(false);
  /** Emitted when the operator asks for the next page. */
  readonly more = output<void>();

  /** Localized "showing X of Y" line with the placeholders interpolated. */
  readonly countText = computed(() => this.i18n.t('common.showingOf')
    .replace('{shown}', String(this.shown()))
    .replace('{total}', String(this.total())));
}
