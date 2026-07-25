import {
  ChangeDetectionStrategy, Component, ElementRef, HostListener, OnInit,
  computed, forwardRef, inject, input, signal,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { I18nService } from '../../core/i18n.service';
import { OrgDataService } from '../../core/org-data.service';

/**
 * User picker built for large directories (100+ users): a combobox with
 * type-to-search filtering, a scrollable option list, and — in multi mode —
 * removable selection tags plus per-row checkboxes. Single mode picks one id
 * and closes. Integrates with reactive forms via ControlValueAccessor
 * (single: string id or ''; multi: string[]). Closes on outside click or Esc.
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
    <div class="box" [class.disabled]="disabled()">
      <button type="button" class="trigger" (click)="toggleOpen()" [disabled]="disabled()"
              [attr.aria-expanded]="open()" aria-haspopup="listbox">
        <span class="content">
          @if (multiple()) {
            @if (selected().length === 0) {
              <span class="ph">{{ i18n.t('usel.placeholder') }}</span>
            } @else {
              @for (id of visibleTags(); track id) {
                <span class="tag">
                  {{ org.userName(id) }}
                  <span class="x" role="button" tabindex="0" (click)="remove(id, $event)" aria-label="Remove">✕</span>
                </span>
              }
              @if (selected().length > maxTags) {
                <span class="tag more">+{{ selected().length - maxTags }}</span>
              }
            }
          } @else {
            @if (single()) {
              <span>{{ org.userName(single()) }}</span>
            } @else {
              <span class="ph">{{ i18n.t('usel.placeholder') }}</span>
            }
          }
        </span>
        <span class="caret">▾</span>
      </button>

      @if (open()) {
        <div class="panel" role="listbox">
          <input class="query" type="text" [value]="query()" (input)="query.set($any($event.target).value)"
                 [placeholder]="i18n.t('usel.search')" autocomplete="off" />
          @if (multiple() && selected().length > 0) {
            <div class="meta">
              {{ selected().length }} {{ i18n.t('usel.selected') }}
              <button type="button" class="clear" (click)="clearAll()">{{ i18n.t('usel.clear') }}</button>
            </div>
          }
          <div class="options">
            @for (u of filtered(); track u.id) {
              <div class="opt" [class.on]="isSelected(u.id)" (click)="pick(u.id)" role="option"
                   [attr.aria-selected]="isSelected(u.id)">
                @if (multiple()) {
                  <span class="cb" [class.checked]="isSelected(u.id)">@if (isSelected(u.id)) { ✓ }</span>
                }
                <span class="nm">{{ u.displayName }}</span>
                <span class="rl">{{ u.role }}</span>
              </div>
            } @empty {
              <div class="none">{{ i18n.t('usel.noMatch') }}</div>
            }
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .box { position: relative; }
    .trigger {
      width: 100%; min-height: 38px; display: flex; align-items: center; gap: 8px;
      background: #fff; color: var(--nt-slate); font-weight: 400; font-size: 13px;
      border: 1px solid var(--nt-border); border-radius: var(--nt-radius-input);
      padding: 5px 11px; text-align: start; cursor: pointer;
    }
    .trigger:hover { background: #fff; }
    .trigger:focus { outline: none; border-color: var(--nt-blue); box-shadow: 0 0 0 2px rgba(0, 119, 194, .15); }
    .content { flex: 1; display: flex; flex-wrap: wrap; gap: 4px; align-items: center; min-width: 0; }
    .ph { color: var(--nt-grey); }
    .caret { color: var(--nt-grey-m); font-size: 11px; flex-shrink: 0; }
    .tag {
      display: inline-flex; align-items: center; gap: 5px;
      background: var(--nt-brand-soft); color: var(--nt-blue); border: 1px solid rgba(0, 119, 194, .25);
      border-radius: 999px; padding: 2px 8px; font-size: 11.5px; font-weight: 600; white-space: nowrap;
    }
    .tag .x { cursor: pointer; opacity: .7; font-size: 10px; }
    .tag .x:hover { opacity: 1; }
    .tag.more { background: var(--nt-filter-grey); color: var(--nt-grey-d); border-color: var(--nt-border); }
    .panel {
      position: absolute; z-index: 250; inset-inline-start: 0; inset-inline-end: 0; top: calc(100% + 4px);
      background: #fff; border: 1px solid var(--nt-border); border-radius: 8px;
      box-shadow: var(--nt-shadow-pop); padding: 8px; display: flex; flex-direction: column; gap: 6px;
    }
    .query { width: 100%; font-size: 13px; }
    .meta {
      display: flex; align-items: center; justify-content: space-between;
      font-size: 11px; color: var(--nt-grey-d); padding: 0 2px;
    }
    .clear { background: none; color: var(--nt-blue); font-size: 11px; padding: 2px 6px; border-radius: 4px; }
    .clear:hover { background: var(--nt-brand-soft); }
    .options { max-height: 240px; overflow-y: auto; }
    .opt {
      display: flex; align-items: center; gap: 9px; padding: 7px 9px;
      border-radius: 5px; cursor: pointer; font-size: 13px;
    }
    .opt:hover { background: var(--nt-bg-grey); }
    .opt.on { background: var(--nt-brand-soft); }
    .cb {
      width: 16px; height: 16px; flex-shrink: 0; border: 1.5px solid var(--nt-border); border-radius: 4px;
      display: inline-flex; align-items: center; justify-content: center;
      font-size: 11px; color: #fff; background: #fff;
    }
    .cb.checked { background: var(--nt-blue); border-color: var(--nt-blue); }
    .nm { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .rl { font-size: 10.5px; color: var(--nt-grey-m); flex-shrink: 0; }
    .none { padding: 10px; font-size: 12px; color: var(--nt-grey-m); text-align: center; }
    .disabled .trigger { background: var(--nt-bg-grey); color: var(--nt-grey-m); cursor: not-allowed; }
  `],
})
export class UserSelectComponent implements ControlValueAccessor, OnInit {
  readonly i18n = inject(I18nService);
  readonly org = inject(OrgDataService);
  private readonly host = inject(ElementRef<HTMLElement>);

  /** Multi-user mode: value is string[] instead of a single id string. */
  readonly multiple = input(false);

  /** Selection tags shown before collapsing into a "+N" counter. */
  readonly maxTags = 4;

  readonly open = signal(false);
  readonly query = signal('');
  readonly single = signal('');
  readonly selected = signal<string[]>([]);
  readonly disabled = signal(false);

  /** Directory filtered by the search query (name or role, case-insensitive). */
  readonly filtered = computed(() => {
    const q = this.query().trim().toLowerCase();
    const all = this.org.directory();
    return q ? all.filter((u) => `${u.displayName} ${u.role}`.toLowerCase().includes(q)) : all;
  });

  readonly visibleTags = computed(() => this.selected().slice(0, this.maxTags));

  private onChange: (value: string | string[]) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  ngOnInit(): void { void this.org.ensureDirectory(); }

  @HostListener('document:click', ['$event'])
  onOutsideClick(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) {
      this.close();
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open()) { this.close(); }
  }

  toggleOpen(): void {
    if (this.disabled()) { return; }
    this.open.update((o) => !o);
    if (!this.open()) { this.onTouched(); }
  }

  /** Selects (single) or toggles (multi) the given user id. */
  pick(id: string): void {
    if (this.multiple()) {
      this.toggle(id);
      return;
    }
    this.single.set(id);
    this.onChange(id);
    this.close();
  }

  /** Toggles a user id in the multi selection. */
  toggle(id: string): void {
    const current = this.selected();
    this.selected.set(current.includes(id) ? current.filter((x) => x !== id) : [...current, id]);
    this.onChange(this.selected());
  }

  remove(id: string, event: Event): void {
    event.stopPropagation();
    this.selected.set(this.selected().filter((x) => x !== id));
    this.onChange(this.selected());
  }

  clearAll(): void {
    this.selected.set([]);
    this.onChange([]);
  }

  isSelected(id: string): boolean {
    return this.multiple() ? this.selected().includes(id) : this.single() === id;
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

  private close(): void {
    this.open.set(false);
    this.query.set('');
    this.onTouched();
  }
}
