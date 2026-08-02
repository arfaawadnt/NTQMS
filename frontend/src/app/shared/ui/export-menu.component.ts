import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ExportsApiService } from '../../core/api/exports-api.service';
import { I18nService } from '../../core/i18n.service';
import { ListStat } from './list-stats.component';

/**
 * One column of a page export: the printed header and how to read the cell
 * from a row object. Everything is stringified client-side so the document
 * shows exactly what the screen shows — same locale, same names, same order.
 */
export interface ExportColumn<T> {
  header: string;
  cell: (row: T) => string;
}

/**
 * PDF / Excel export buttons for a register page. The document is the caller's
 * own current view: the page passes its title, its statistic tiles, its
 * filtered rows (or a fetchAll callback that pulls every page of the filtered
 * server query) and a human-readable filter line — so the export respects both
 * the filtration and the caller's privileges by construction.
 */
@Component({
  selector: 'qams-export-menu',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="wrap">
      <button type="button" class="secondary" [disabled]="busy()" (click)="run('pdf')">
        {{ busy() === 'pdf' ? i18n.t('exp.working') : i18n.t('exp.pdf') }}
      </button>
      <button type="button" class="secondary" [disabled]="busy()" (click)="run('xlsx')">
        {{ busy() === 'xlsx' ? i18n.t('exp.working') : i18n.t('exp.excel') }}
      </button>
      @if (error()) { <span class="err" role="alert">{{ error() }}</span> }
    </span>
  `,
  styles: [`
    .wrap { display: inline-flex; gap: 8px; align-items: center; }
    button { width: auto; }
    .err { color: var(--nt-ink-crit); font-size: 12px; }
  `],
})
export class ExportMenuComponent<T> {
  readonly i18n = inject(I18nService);
  private readonly exports = inject(ExportsApiService);

  /** Localized page title — becomes the document title and filename. */
  readonly title = input.required<string>();
  /** The stat tiles as shown (value + optional denominator are printed together). */
  readonly stats = input<readonly ListStat[]>([]);
  /** Column definitions in display order. */
  readonly columns = input.required<readonly ExportColumn<T>[]>();
  /** The page's current (client-filtered) rows. */
  readonly rows = input.required<readonly T[]>();
  /**
   * For paged registers: pulls EVERY page of the current server-side filter, so
   * the document holds the whole filtered dataset, not just the rows loaded so
   * far. Unpaged pages omit it and `rows` is already complete.
   */
  readonly fetchAll = input<(() => Promise<readonly T[]>) | null>(null);
  /** Human-readable description of the filters in force (already localized). */
  readonly filtersSummary = input<string>('');

  readonly busy = signal<'pdf' | 'xlsx' | null>(null);
  readonly error = signal('');

  async run(format: 'pdf' | 'xlsx'): Promise<void> {
    if (this.busy()) { return; }
    this.busy.set(format);
    this.error.set('');
    try {
      const source = this.fetchAll();
      const data = source ? await source() : this.rows();
      await this.exports.exportPage(format, {
        title: this.title(),
        filtersSummary: this.filtersSummary().trim() || null,
        stats: this.stats().map((s) => ({
          label: s.label,
          value: s.of !== undefined ? `${s.value} / ${s.of}` : `${s.value}`,
          tone: s.tone,
        })),
        columns: this.columns().map((c) => c.header),
        rows: data.map((row) => this.columns().map((c) => c.cell(row) ?? '—')),
      });
    } catch (err) {
      // Surfacing failures matters more than styling them: exports are evidence.
      this.error.set(err instanceof HttpErrorResponse
        ? ((err.error as { title?: string } | null)?.title ?? `Request failed (${err.status}).`)
        : 'Export failed.');
    } finally {
      this.busy.set(null);
    }
  }
}
