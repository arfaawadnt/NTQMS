import { ChangeDetectionStrategy, Component, OnInit, forwardRef, inject, input, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { OrgDataService } from '../../core/org-data.service';

/**
 * User picker bound to the tenant directory (names, not GUIDs). Integrates
 * with reactive forms via ControlValueAccessor: single mode holds a user id
 * (string, '' when empty); multi mode holds string[]. Multi-select renders as
 * checkbox chips — friendlier than a ctrl-click listbox.
 */
@Component({
  selector: 'qams-user-select',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{
    provide: NG_VALUE_ACCESSOR,
    useExisting: forwardRef(() => UserSelectComponent),
    multi: true,
  }],
  template: `
    @if (!multiple()) {
      <select [value]="single()" (change)="pick($event)" [disabled]="disabled()">
        <option value="">—</option>
        @for (u of org.directory(); track u.id) {
          <option [value]="u.id">{{ u.displayName }} ({{ u.role }})</option>
        }
      </select>
    } @else {
      <div class="chips">
        @for (u of org.directory(); track u.id) {
          <label class="chip" [class.on]="selected().includes(u.id)">
            <input type="checkbox" [checked]="selected().includes(u.id)"
                   (change)="toggle(u.id)" [disabled]="disabled()" />
            {{ u.displayName }}
          </label>
        }
      </div>
    }
  `,
  styles: [`
    select { width: 100%; }
    .chips { display: flex; flex-wrap: wrap; gap: 6px; }
    .chip {
      display: inline-flex; align-items: center; gap: 5px; margin: 0;
      border: 1px solid var(--nt-border); border-radius: 999px; padding: 4px 11px;
      font-size: 12px; font-weight: 500; cursor: pointer; background: #fff;
    }
    .chip input { width: auto; margin: 0; }
    .chip.on { background: var(--nt-brand-soft); border-color: var(--nt-blue); color: var(--nt-blue); font-weight: 600; }
  `],
})
export class UserSelectComponent implements ControlValueAccessor, OnInit {
  readonly org = inject(OrgDataService);

  /** Multi-user mode: value is string[] instead of a single id string. */
  readonly multiple = input(false);

  readonly single = signal('');
  readonly selected = signal<string[]>([]);
  readonly disabled = signal(false);

  private onChange: (value: string | string[]) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  ngOnInit(): void { void this.org.ensureDirectory(); }

  pick(event: Event): void {
    this.single.set((event.target as HTMLSelectElement).value);
    this.onChange(this.single());
    this.onTouched();
  }

  toggle(id: string): void {
    const current = this.selected();
    this.selected.set(current.includes(id) ? current.filter((x) => x !== id) : [...current, id]);
    this.onChange(this.selected());
    this.onTouched();
  }

  writeValue(value: string | string[] | null): void {
    if (this.multiple()) {
      this.selected.set(Array.isArray(value) ? value : []);
    } else {
      this.single.set(typeof value === 'string' ? value : '');
    }
  }

  registerOnChange(fn: (value: string | string[]) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }
  setDisabledState(isDisabled: boolean): void { this.disabled.set(isDisabled); }
}
