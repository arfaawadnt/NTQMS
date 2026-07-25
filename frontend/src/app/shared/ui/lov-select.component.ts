import { ChangeDetectionStrategy, Component, OnInit, forwardRef, inject, input, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { I18nService } from '../../core/i18n.service';
import { OrgDataService } from '../../core/org-data.service';
import { LovEntry } from '../../core/models';

/**
 * List-of-values picker: when entries exist for the given LOV category
 * (managed under Reference Data → Lists of Values) it renders a localized
 * dropdown storing the entry's English name (the value the backend receives);
 * when the category has no entries yet it degrades to a free-text input with
 * a hint pointing at the LOV setup — never a dead end. ControlValueAccessor,
 * value is a plain string.
 */
@Component({
  selector: 'qams-lov-select',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{
    provide: NG_VALUE_ACCESSOR,
    useExisting: forwardRef(() => LovSelectComponent),
    multi: true,
  }],
  template: `
    @if (entries().length > 0) {
      <select [value]="value()" (change)="pick($event)" [disabled]="disabled()">
        <option value="">—</option>
        @for (e of entries(); track e.id) {
          <option [value]="e.nameEn">{{ org.lovName(e) }}</option>
        }
      </select>
    } @else {
      <input [value]="value()" (input)="type($event)" [disabled]="disabled()" [placeholder]="placeholder()" />
      <div class="hint">{{ i18n.t('lov.manageHint') }} ({{ category() }})</div>
    }
  `,
  styles: [`select, input { width: 100%; }`],
})
export class LovSelectComponent implements ControlValueAccessor, OnInit {
  readonly i18n = inject(I18nService);
  readonly org = inject(OrgDataService);

  /** LOV category key, e.g. RISK_CATEGORY. */
  readonly category = input.required<string>();
  readonly placeholder = input('');

  readonly entries = signal<LovEntry[]>([]);
  readonly value = signal('');
  readonly disabled = signal(false);

  private onChange: (value: string) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  async ngOnInit(): Promise<void> {
    this.entries.set(await this.org.lovEntries(this.category()));
  }

  pick(event: Event): void {
    this.value.set((event.target as HTMLSelectElement).value);
    this.onChange(this.value());
    this.onTouched();
  }

  type(event: Event): void {
    this.value.set((event.target as HTMLInputElement).value);
    this.onChange(this.value());
    this.onTouched();
  }

  writeValue(value: string | null): void { this.value.set(value ?? ''); }
  registerOnChange(fn: (value: string) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }
  setDisabledState(isDisabled: boolean): void { this.disabled.set(isDisabled); }
}
