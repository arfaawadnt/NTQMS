import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterOutlet } from '@angular/router';
import { TrainingFacade } from './training.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { COURSE_STATUSES, TRAINING_CATEGORIES, TrainingCategory } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';

/**
 * Training catalogue (HQMS M12): the compliance summary at the top; the course catalogue with a
 * define drawer and drawer-detail; a course drills into its effectiveness and sessions.
 */
@Component({
    selector: 'qams-training-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, ListStatsComponent],
    template: `
    <qams-page-header [title]="i18n.t('trn.title')">
      @if (perms.can('training.create')) {
        <button (click)="form.reset(defaults); showForm.set(true)">{{ i18n.t('trn.defineCourse') }}</button>
      }
    </qams-page-header>

    <qams-list-stats [stats]="stats()" />

    <div class="filterbar card">
      <select [value]="categoryFilter()" (change)="onFilter('category', $event)" aria-label="Category filter">
        <option value="">{{ i18n.t('trn.allCategories') }}</option>
        @for (c of categories; track c) { <option [value]="c">{{ i18n.t('trn.cat.' + c) }}</option> }
      </select>
      <select [value]="statusFilter()" (change)="onFilter('status', $event)" aria-label="Status filter">
        <option value="">{{ i18n.t('trn.allStatuses') }}</option>
        @for (s of statuses; track s) { <option [value]="s">{{ i18n.t('trn.cs.' + s) }}</option> }
      </select>
    </div>

    <qams-drawer [open]="showForm()" [title]="i18n.t('trn.defineCourse')" (closed)="showForm.set(false)">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="create()">
        <label>{{ i18n.t('trn.courseTitle') }}</label>
        <input formControlName="title" />
        <div class="grid">
          <div><label>{{ i18n.t('trn.category') }}</label><select formControlName="category">@for (c of categories; track c) { <option [value]="c">{{ i18n.t('trn.cat.' + c) }}</option> }</select></div>
          <div><label>{{ i18n.t('trn.durationHours') }}</label><input type="number" min="0.25" step="0.25" formControlName="durationHours" /></div>
          <div><label>{{ i18n.t('trn.validityMonths') }}</label><input type="number" min="1" formControlName="validityMonths" [placeholder]="i18n.t('trn.noExpiry')" /></div>
          <div><label>{{ i18n.t('trn.passMark') }}</label><input type="number" min="0" max="100" formControlName="passMark" /></div>
        </div>
        <label>{{ i18n.t('trn.description') }}</label>
        <textarea rows="2" formControlName="description"></textarea>
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('trn.save') }}</button>
          <button type="button" class="secondary" (click)="showForm.set(false)">{{ i18n.t('common.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.courses().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.courses().length === 0) {
      <p class="muted">{{ i18n.t('trn.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('trn.ref') }}</th><th>{{ i18n.t('trn.courseTitle') }}</th><th>{{ i18n.t('trn.category') }}</th>
            <th>{{ i18n.t('trn.durationHours') }}</th><th>{{ i18n.t('trn.passMark') }}</th><th>{{ i18n.t('trn.sessions') }}</th><th>{{ i18n.t('trn.status') }}</th>
          </tr></thead>
          <tbody>
            @for (c of facade.courses(); track c.id) {
              <tr class="clickable" (click)="open(c.id)">
                <td class="code">{{ c.courseRef }}</td>
                <td>{{ c.title }}</td>
                <td>{{ i18n.t('trn.cat.' + c.category) }}</td>
                <td>{{ c.durationHours }}h</td>
                <td>{{ c.passMark }}%</td>
                <td>{{ c.sessionCount }}</td>
                <td><qams-status-pill [status]="c.status" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <qams-drawer [open]="detailOpen()" [title]="i18n.t('trn.title')" width="860px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .filterbar { display: flex; gap: 10px; padding: 10px 14px; margin-bottom: 14px; flex-wrap: wrap; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: .5rem 1rem; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    select, button { width: auto; }
  `]
})
export class TrainingListComponent implements OnInit {
  readonly facade = inject(TrainingFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly categories = TRAINING_CATEGORIES;
  readonly statuses = COURSE_STATUSES;

  readonly showForm = signal(false);
  readonly detailOpen = signal(false);
  readonly categoryFilter = signal('');
  readonly statusFilter = signal('');

  readonly stats = computed<ListStat[]>(() => [
    { label: this.i18n.t('trn.stat.courses'), value: this.facade.courses().length, tone: 'slate' },
    { label: this.i18n.t('trn.stat.active'), value: this.facade.activeCourses(), tone: 'teal' },
    { label: this.i18n.t('trn.stat.passRate'), value: this.facade.meanPassRate(), tone: 'green' },
  ]);

  readonly defaults = { title: '', category: 'Mandatory' as TrainingCategory, durationHours: 1, validityMonths: null as number | null, passMark: 80, description: '' };
  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    category: ['Mandatory' as TrainingCategory, [Validators.required]],
    durationHours: [1, [Validators.required, Validators.min(0.25)]],
    validityMonths: this.fb.control<number | null>(null),
    passMark: [80, [Validators.required, Validators.min(0), Validators.max(100)]],
    description: ['', [Validators.maxLength(4000)]],
  });

  ngOnInit(): void {
    void this.facade.loadList();
  }

  onFilter(which: 'category' | 'status', event: Event): void {
    const val = (event.target as HTMLSelectElement).value;
    if (which === 'category') { this.categoryFilter.set(val); } else { this.statusFilter.set(val); }
    void this.facade.loadList(this.categoryFilter() || undefined, this.statusFilter() || undefined);
  }

  async create(): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    const id = await this.facade.defineCourse({
      title: raw.title, category: raw.category, description: raw.description,
      durationHours: Number(raw.durationHours),
      validityMonths: raw.validityMonths ? Number(raw.validityMonths) : null,
      passMark: Number(raw.passMark),
    });
    if (id) {
      this.showForm.set(false);
      void this.facade.loadList(this.categoryFilter() || undefined, this.statusFilter() || undefined);
      void this.router.navigate(['/training-catalogue', id]);
    }
  }

  open(id: string): void { void this.router.navigate(['/training-catalogue', id]); }
  closeDetail(): void { void this.router.navigate(['/training-catalogue']); }
}
