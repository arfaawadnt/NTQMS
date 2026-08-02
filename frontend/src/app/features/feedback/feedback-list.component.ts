import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { FeedbackFacade } from './feedback.facade';
import { I18nService } from '../../core/i18n.service';
import { FEEDBACK_TYPES, FeedbackListItem } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { AllocationPickerComponent } from '../../shared/ui/allocation-picker.component';
import { LovSelectComponent } from '../../shared/ui/lov-select.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';
import { ExportColumn, ExportMenuComponent } from '../../shared/ui/export-menu.component';

/** General feedback register: compliments, suggestions, dissatisfaction + satisfaction trend (§8.6.2). */
@Component({
    selector: 'qams-feedback-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, AllocationPickerComponent, LovSelectComponent, ListStatsComponent, ExportMenuComponent],
    template: `
    <qams-page-header [title]="i18n.t('fbk.title')" [subtitle]="i18n.t('fbk.subtitle')">
      <qams-export-menu [title]="i18n.t('fbk.title')" [stats]="stats()" [columns]="exportColumns"
                        [rows]="filtered()" [filtersSummary]="filtersSummary()" />
      <button (click)="showForm.set(!showForm())">{{ i18n.t('fbk.new') }}</button>
    </qams-page-header>

    <qams-list-stats [stats]="stats()" />

    <div class="filterbar card">
      <input class="search" [value]="search()" (input)="search.set($any($event.target).value)" [placeholder]="i18n.t('common.search')" />
      <select [value]="typeFilter()" (change)="onType($event)" aria-label="Type filter">
        <option value="">{{ i18n.t('fbk.allTypes') }}</option>
        @for (t of types; track t) { <option [value]="t">{{ i18n.t('fbk.type' + t) }}</option> }
      </select>
      <select [value]="statusFilter()" (change)="onStatus($event)" aria-label="Status filter">
        <option value="">{{ i18n.t('nc.allStatuses') }}</option>
        @for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
      </select>
    </div>

    <qams-drawer [open]="showForm()" [title]="i18n.t('fbk.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="log()">
        <label>{{ i18n.t('fbk.source') }}</label>
        <qams-lov-select formControlName="source" category="FEEDBACK_SOURCE" [placeholder]="i18n.t('fbk.sourceHint')" />
        <label>{{ i18n.t('fbk.channel') }}</label>
        <qams-lov-select formControlName="channel" category="FEEDBACK_CHANNEL" [placeholder]="i18n.t('fbk.channelHint')" />
        <label>{{ i18n.t('fbk.type') }}</label>
        <select formControlName="type">
          @for (t of types; track t) { <option [value]="t">{{ i18n.t('fbk.type' + t) }}</option> }
        </select>
        <label>{{ i18n.t('cmpl.subject') }}</label>
        <input formControlName="subject" />
        <label>{{ i18n.t('nc.description') }}</label>
        <textarea rows="3" formControlName="details"></textarea>
        <label>{{ i18n.t('fbk.score') }}</label>
        <select formControlName="satisfactionScore">
          <option value="">{{ i18n.t('common.optional') }}</option>
          @for (n of [1,2,3,4,5]; track n) { <option [value]="n">{{ n }} / 5</option> }
        </select>
        <label>{{ i18n.t('fbk.receivedOn') }}</label>
        <input type="date" formControlName="receivedOn" />
        <qams-allocation-picker [branchCtrl]="form.controls.branchId" [departmentCtrl]="form.controls.departmentId" />
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('fbk.log') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (filtered().length === 0) {
      <p class="muted">{{ i18n.t('fbk.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('mu.ref') }}</th><th>{{ i18n.t('fbk.type') }}</th><th>{{ i18n.t('cmpl.subject') }}</th>
            <th>{{ i18n.t('fbk.source') }}</th><th>{{ i18n.t('fbk.score') }}</th>
            <th>{{ i18n.t('fbk.receivedOn') }}</th><th>{{ i18n.t('nc.status') }}</th>
          </tr></thead>
          <tbody>
            @for (f of filtered(); track f.id) {
              <tr class="clickable" (click)="open(f.id)">
                <td class="code">{{ f.feedbackRef }}</td>
                <td>
                  <span [class.good]="f.type === 'Compliment'" [class.bad]="f.type === 'Dissatisfaction'">
                    {{ i18n.t('fbk.type' + f.type) }}
                  </span>
                </td>
                <td>{{ f.subject }}</td>
                <td class="muted">{{ f.source }} · {{ f.channel }}</td>
                <td>{{ f.satisfactionScore !== null ? f.satisfactionScore + '/5' : '—' }}</td>
                <td>{{ f.receivedOn | date:'mediumDate' }}</td>
                <td><qams-status-pill [status]="f.status" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <!-- Record workspace: the routed detail renders in a wide drawer over the list. -->
    <qams-drawer [open]="detailOpen()" [title]="i18n.t('fbk.title')" width="920px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .filterbar { display: flex; gap: 10px; align-items: center; padding: 10px 14px; margin-bottom: 14px; flex-wrap: wrap; }
    .search { max-width: 280px; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    button, select { width: auto; }
    .good { color: var(--nt-green); font-weight: 600; }
    .bad { color: var(--nt-red); font-weight: 600; }
  `]
})
export class FeedbackListComponent implements OnInit {
  readonly facade = inject(FeedbackFacade);
  readonly i18n = inject(I18nService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly statuses = ['Logged', 'Reviewed', 'Closed', 'Escalated'];
  readonly types = FEEDBACK_TYPES;
  readonly showForm = signal(false);
  /** Whether the record-workspace drawer (child route) is active. */
  readonly detailOpen = signal(false);
  readonly statusFilter = signal('');
  readonly typeFilter = signal('');
  readonly search = signal('');

  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    return this.facade.list().filter((f) =>
      !q || `${f.feedbackRef} ${f.subject} ${f.source} ${f.channel} ${f.type} ${f.status}`.toLowerCase().includes(q));
  });

  /** Export columns — the printed grid mirrors the on-screen table. */
  readonly exportColumns: ExportColumn<FeedbackListItem>[] = [
    { header: this.i18n.t('mu.ref'), cell: (f) => f.feedbackRef },
    { header: this.i18n.t('fbk.type'), cell: (f) => this.i18n.t('fbk.type' + f.type) },
    { header: this.i18n.t('cmpl.subject'), cell: (f) => f.subject },
    { header: this.i18n.t('fbk.source'), cell: (f) => `${f.source} · ${f.channel}` },
    { header: this.i18n.t('fbk.score'), cell: (f) => f.satisfactionScore !== null ? `${f.satisfactionScore}/5` : '—' },
    { header: this.i18n.t('fbk.receivedOn'), cell: (f) => f.receivedOn },
    { header: this.i18n.t('nc.status'), cell: (f) => f.status },
  ];

  /** The filter line printed on the document, mirroring the filter bar. */
  readonly filtersSummary = computed(() => {
    const parts: string[] = [];
    if (this.typeFilter()) { parts.push(this.i18n.t('fbk.type' + this.typeFilter())); }
    if (this.statusFilter()) { parts.push(this.statusFilter()); }
    if (this.search().trim()) { parts.push(`"${this.search().trim()}"`); }
    return parts.length ? parts.join(' · ') : this.i18n.t('exp.allRecords');
  });

  readonly stats = computed<ListStat[]>(() => {
    const all = this.facade.list();
    const scored = all.filter((f) => f.satisfactionScore !== null);
    const avg = scored.length === 0
      ? null
      : Math.round((scored.reduce((s, f) => s + (f.satisfactionScore ?? 0), 0) / scored.length) * 10) / 10;
    return [
      { label: this.i18n.t('stat.total'), value: all.length, tone: 'slate' },
      { label: this.i18n.t('fbk.typeCompliment'), value: all.filter((f) => f.type === 'Compliment').length, tone: 'green' },
      { label: this.i18n.t('fbk.typeDissatisfaction'), value: all.filter((f) => f.type === 'Dissatisfaction').length, tone: 'red' },
      { label: this.i18n.t('fbk.avgScore'), value: avg ?? '—', tone: 'teal' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    source: ['', [Validators.required, Validators.maxLength(100)]],
    channel: ['', [Validators.required, Validators.maxLength(100)]],
    type: ['Suggestion' as (typeof FEEDBACK_TYPES)[number], [Validators.required]],
    subject: ['', [Validators.required, Validators.maxLength(300)]],
    details: ['', [Validators.required, Validators.maxLength(4000)]],
    satisfactionScore: [''],
    receivedOn: ['', [Validators.required]],
    branchId: [''],
    departmentId: [''],
  });

  ngOnInit(): void { void this.facade.loadList(); }

  onType(event: Event): void {
    this.typeFilter.set((event.target as HTMLSelectElement).value);
    void this.facade.loadList(this.statusFilter() || undefined, this.typeFilter() || undefined);
  }

  onStatus(event: Event): void {
    this.statusFilter.set((event.target as HTMLSelectElement).value);
    void this.facade.loadList(this.statusFilter() || undefined, this.typeFilter() || undefined);
  }

  async log(): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    const id = await this.facade.log({
      ...raw,
      satisfactionScore: raw.satisfactionScore ? Number(raw.satisfactionScore) : null,
      branchId: raw.branchId || null,
      departmentId: raw.departmentId || null,
    });
    if (id) { this.cancel(); void this.router.navigate(['/feedback', id]); }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset({ type: 'Suggestion' });
  }

  open(id: string): void { void this.router.navigate(['/feedback', id]); }

  /** Dismissing the workspace drawer returns to the plain list route. */
  closeDetail(): void { void this.router.navigate(['/feedback']); }
}
