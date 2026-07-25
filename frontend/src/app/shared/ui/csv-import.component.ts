import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { I18nService } from '../../core/i18n.service';
import { BulkImportResult } from '../../core/models';

/** A parsed CSV column definition: header label and whether the field is numeric. */
export interface CsvColumn {
  label: string;
  numeric: boolean;
  optional?: boolean;
}

/** One previewed row: the raw cells plus a validity verdict. */
interface PreviewRow {
  cells: string[];
  valid: boolean;
  reason: string;
}

/**
 * Reusable analyzer/LIS CSV importer: the user pastes or uploads delimited text
 * (comma, tab or semicolon), the component maps it to the caller's columns,
 * validates every row client-side (numeric fields, required fields), shows a
 * preview with per-row verdicts, and emits the accepted rows as string[][].
 * The backend re-validates and returns the authoritative per-row result, which
 * the caller passes back via `result` for display.
 */
@Component({
  selector: 'qams-csv-import',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="importer">
      <p class="muted hint">{{ i18n.t('csv.columns') }}: <b>{{ columnHint() }}</b>. {{ i18n.t('csv.delimHint') }}</p>
      <div class="controls">
        <input type="file" accept=".csv,.tsv,.txt" (change)="onFile($event)" />
        <button type="button" class="secondary" (click)="clear()" [disabled]="!raw()">{{ i18n.t('csv.clear') }}</button>
      </div>
      <textarea rows="5" [value]="raw()" (input)="onPaste($event)" [placeholder]="i18n.t('csv.paste')"></textarea>

      @if (rows().length > 0) {
        <div class="summary">
          <span class="ok">{{ validCount() }} {{ i18n.t('csv.valid') }}</span>
          @if (invalidCount() > 0) { <span class="bad">{{ invalidCount() }} {{ i18n.t('csv.invalid') }}</span> }
          <label class="chk"><input type="checkbox" [checked]="skipHeader()" (change)="toggleHeader($event)" /> {{ i18n.t('csv.skipHeader') }}</label>
        </div>
        <div class="preview">
          <table>
            <thead><tr><th>#</th>@for (c of columns(); track c.label) { <th>{{ c.label }}</th> }<th></th></tr></thead>
            <tbody>
              @for (r of rows(); track $index) {
                <tr [class.bad-row]="!r.valid">
                  <td class="muted">{{ $index + 1 }}</td>
                  @for (c of columns(); track c.label; let ci = $index) { <td>{{ r.cells[ci] ?? '' }}</td> }
                  <td>@if (!r.valid) { <span class="bad" [title]="r.reason">✕</span> } @else { <span class="ok">✓</span> }</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <button type="button" (click)="emitImport()" [disabled]="validCount() === 0 || busy()">
          {{ i18n.t('csv.import') }} ({{ validCount() }})
        </button>
      }

      @if (result(); as r) {
        <div class="result" [class.clean]="r.rejected.length === 0">
          {{ r.imported }} {{ i18n.t('csv.imported') }}@if (r.rejected.length) { , {{ r.rejected.length }} {{ i18n.t('csv.rejected') }} }
          @for (rej of r.rejected; track rej.row) { <div class="muted small">{{ i18n.t('prc.run') === 'Run' ? 'Row' : '#' }} {{ rej.row }}: {{ rej.reason }}</div> }
        </div>
      }
    </div>
  `,
  styles: [`
    .importer { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    .hint { font-size: .75rem; margin-bottom: 8px; }
    .controls { display: flex; gap: 10px; align-items: center; margin-bottom: 8px; }
    .controls button { width: auto; }
    textarea { width: 100%; font-family: var(--nt-mono); font-size: 12px; }
    .summary { display: flex; gap: 16px; align-items: center; margin: 8px 0; font-size: 12.5px; }
    .ok { color: var(--nt-green); font-weight: 600; }
    .bad { color: var(--nt-red); font-weight: 600; }
    .chk { display: flex; gap: 6px; align-items: center; margin-inline-start: auto; font-weight: 400; }
    .chk input { width: auto; }
    .preview { max-height: 240px; overflow-y: auto; border: 1px solid var(--nt-border); border-radius: 6px; }
    .preview table { font-size: 12px; }
    .bad-row { background: rgba(220,53,69,.06); }
    button { width: auto; margin-top: .5rem; }
    .result { margin-top: 10px; padding: 8px 12px; border-radius: 6px; background: var(--nt-bg-grey);
              border-inline-start: 4px solid var(--nt-red); font-size: 13px; }
    .result.clean { border-inline-start-color: var(--nt-green); }
    .small { font-size: 11px; margin-top: 2px; }
  `],
})
export class CsvImportComponent {
  readonly i18n = inject(I18nService);

  /** Column schema the pasted data maps to, left-to-right. */
  readonly columns = input.required<CsvColumn[]>();
  /** Latest backend result, passed back by the caller for display. */
  readonly result = input<BulkImportResult | null>(null);
  readonly busy = input(false);

  /** Emits the accepted rows (cells as strings) for the caller to POST. */
  readonly import = output<string[][]>();

  readonly raw = signal('');
  readonly skipHeader = signal(true);

  readonly columnHint = computed(() => this.columns().map((c) => c.label + (c.optional ? '?' : '')).join(', '));

  /** Parsed + validated preview rows (header skipped when the toggle is on). */
  readonly rows = computed<PreviewRow[]>(() => {
    const text = this.raw().trim();
    if (!text) { return []; }
    const lines = text.split(/\r?\n/).filter((l) => l.trim().length > 0);
    const body = this.skipHeader() && lines.length > 1 ? lines.slice(1) : lines;
    return body.map((line) => this.validate(this.splitCells(line)));
  });

  readonly validCount = computed(() => this.rows().filter((r) => r.valid).length);
  readonly invalidCount = computed(() => this.rows().filter((r) => !r.valid).length);

  onPaste(event: Event): void { this.raw.set((event.target as HTMLTextAreaElement).value); }

  onFile(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) { return; }
    const reader = new FileReader();
    reader.onload = () => this.raw.set(String(reader.result ?? ''));
    reader.readAsText(file);
  }

  toggleHeader(event: Event): void { this.skipHeader.set((event.target as HTMLInputElement).checked); }

  clear(): void { this.raw.set(''); }

  emitImport(): void {
    this.import.emit(this.rows().filter((r) => r.valid).map((r) => r.cells));
  }

  private splitCells(line: string): string[] {
    const delim = line.includes('\t') ? '\t' : line.includes(';') ? ';' : ',';
    return line.split(delim).map((c) => c.trim());
  }

  private validate(cells: string[]): PreviewRow {
    const cols = this.columns();
    for (let i = 0; i < cols.length; i++) {
      const col = cols[i];
      const value = cells[i] ?? '';
      if (!value) {
        if (col.optional) { continue; }
        return { cells, valid: false, reason: `${col.label} ${this.i18n.t('csv.required')}` };
      }

      if (col.numeric && Number.isNaN(Number(value))) {
        return { cells, valid: false, reason: `${col.label} ${this.i18n.t('csv.notNumeric')} "${value}"` };
      }
    }

    return { cells, valid: true, reason: '' };
  }
}
