import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { IncidentsFacade } from './incidents.facade';
import { I18nService } from '../../core/i18n.service';
import { OrgDataService } from '../../core/org-data.service';
import {
  HARM_GRADES, HarmGrade, INCIDENT_CATEGORIES, INCIDENT_STATUSES, INTAKE_CHANNELS,
  IncidentCategory, IncidentTracking, IntakeChannel,
} from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { AllocationPickerComponent } from '../../shared/ui/allocation-picker.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';
import { LoadMoreComponent } from '../../shared/ui/load-more.component';

/**
 * Incident & Occurrence register (HQMS M02). Reporting is fast and open to every
 * user — volume is a safety-culture signal — with an explicit anonymous path that
 * returns a one-time follow-up reference and never records the reporter. Sentinel
 * events are flagged in the grid; the record workspace opens in a wide drawer.
 */
@Component({
    selector: 'qams-incidents-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, AllocationPickerComponent, ListStatsComponent, LoadMoreComponent],
    template: `
    <qams-page-header [title]="i18n.t('inc.title')">
      <button (click)="openForm(false)">{{ i18n.t('inc.new') }}</button>
      <button class="secondary" (click)="openForm(true)">{{ i18n.t('inc.newAnonymous') }}</button>
      <button class="secondary" (click)="openTrack()">{{ i18n.t('inc.track') }}</button>
    </qams-page-header>

    <qams-list-stats [stats]="stats()" ratioFromFirst />

    <div class="filterbar card">
      <input class="search" [value]="search()" (input)="search.set($any($event.target).value)" [placeholder]="i18n.t('common.search')" />
      <select [value]="statusFilter()" (change)="onStatusFilter($event)" aria-label="Status filter">
        <option value="">{{ i18n.t('inc.allStatuses') }}</option>
        @for (s of statuses; track s) { <option [value]="s">{{ i18n.t('inc.status.' + s) }}</option> }
      </select>
      <select [value]="categoryFilter()" (change)="onCategoryFilter($event)" aria-label="Category filter">
        <option value="">{{ i18n.t('inc.allCategories') }}</option>
        @for (c of categories; track c) { <option [value]="c">{{ i18n.t('inc.cat.' + c) }}</option> }
      </select>
      <label class="check"><input type="checkbox" [checked]="sentinelOnly()" (change)="onSentinelFilter($event)" /> {{ i18n.t('inc.sentinelOnly') }}</label>
    </div>

    <!-- Anonymous follow-up: redeem the one-time reference the receipt promised. -->
    <qams-drawer [open]="showTrack()" [title]="i18n.t('inc.track')" (closed)="showTrack.set(false)">
      <form class="drawer-form" (ngSubmit)="track()">
        <label>{{ i18n.t('inc.anonRef') }}</label>
        <input [value]="trackRef()" (input)="trackRef.set($any($event.target).value)" [placeholder]="i18n.t('inc.trackPlaceholder')" />
        <div class="row">
          <button type="submit" [disabled]="!trackRef().trim() || facade.loading()">{{ i18n.t('inc.track') }}</button>
          <button type="button" class="secondary" (click)="showTrack.set(false)">{{ i18n.t('common.close') }}</button>
        </div>
        @if (trackResult(); as t) {
          <div class="receipt">
            <p class="code">{{ t.incidentRef }}</p>
            <p><qams-status-pill [status]="t.status" /> @if (t.isSentinel) { <span class="pill sentinel">{{ i18n.t('inc.sentinel') }}</span> }</p>
          </div>
        }
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    <qams-drawer [open]="showForm()" [title]="anonymous() ? i18n.t('inc.newAnonymous') : i18n.t('inc.new')" (closed)="showForm.set(false)">
      @if (receipt(); as r) {
        <div class="receipt">
          <h3>{{ i18n.t('inc.anonSubmitted') }}</h3>
          <p>{{ i18n.t('inc.anonRefHint') }}</p>
          <p class="code refbox">{{ r.followUpReference }}</p>
          <button (click)="showForm.set(false)">{{ i18n.t('common.close') }}</button>
        </div>
      } @else {
        <form class="drawer-form" [formGroup]="form" (ngSubmit)="create()">
          @if (anonymous()) { <p class="muted">{{ i18n.t('inc.anonNotice') }}</p> }
          <label>{{ i18n.t('inc.subject') }}</label>
          <input formControlName="title" />
          <label>{{ i18n.t('inc.description') }}</label>
          <textarea rows="3" formControlName="description"></textarea>
          <div class="grid">
            <div>
              <label>{{ i18n.t('inc.category') }}</label>
              <select formControlName="category">@for (c of categories; track c) { <option [value]="c">{{ i18n.t('inc.cat.' + c) }}</option> }</select>
            </div>
            <div>
              <label>{{ i18n.t('inc.harmGrade') }}</label>
              <select formControlName="harmGrade">@for (h of harmGrades; track h) { <option [value]="h">{{ i18n.t('inc.harm.' + h) }}</option> }</select>
            </div>
            <div>
              <label>{{ i18n.t('inc.channel') }}</label>
              <select formControlName="channel">@for (ch of channels; track ch) { <option [value]="ch">{{ i18n.t('inc.ch.' + ch) }}</option> }</select>
            </div>
            <div>
              <label>{{ i18n.t('inc.occurredAt') }}</label>
              <input type="datetime-local" formControlName="occurredAt" />
            </div>
            <div class="col-2">
              <label>{{ i18n.t('inc.location') }}</label>
              <input formControlName="location" />
            </div>
          </div>
          <qams-allocation-picker [branchCtrl]="form.controls.branchId" [departmentCtrl]="form.controls.departmentId" />
          <div class="row">
            <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('inc.submit') }}</button>
            <button type="button" class="secondary" (click)="showForm.set(false)">{{ i18n.t('common.cancel') }}</button>
          </div>
          @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
        </form>
      }
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (filtered().length === 0) {
      <p class="muted">{{ i18n.t('inc.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead>
            <tr>
              <th>{{ i18n.t('inc.ref') }}</th><th>{{ i18n.t('inc.subject') }}</th>
              <th>{{ i18n.t('inc.status') }}</th><th>{{ i18n.t('inc.category') }}</th>
              <th>{{ i18n.t('inc.harmGrade') }}</th><th>{{ i18n.t('inc.occurredAt') }}</th>
              <th>{{ i18n.t('alloc.branch') }}</th>
            </tr>
          </thead>
          <tbody>
            @for (inc of filtered(); track inc.id) {
              <tr class="clickable" (click)="open(inc.id)">
                <td>{{ inc.incidentRef }}</td>
                <td>
                  {{ inc.title }}
                  @if (inc.isSentinel) { <span class="pill sentinel">{{ i18n.t('inc.sentinel') }}</span> }
                  @if (inc.isAnonymous) { <span class="pill anon">{{ i18n.t('inc.anon') }}</span> }
                </td>
                <td><qams-status-pill [status]="inc.status" /></td>
                <td>{{ i18n.t('inc.cat.' + inc.category) }}</td>
                <td [class.danger-text]="inc.harmGrade === 'Severe' || inc.harmGrade === 'Death'">{{ i18n.t('inc.harm.' + inc.harmGrade) }}</td>
                <td>{{ inc.occurredAtUtc | date:'short' }}</td>
                <td class="code">{{ org.branchName(inc.branchId) || '—' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      <qams-load-more [shown]="facade.list().length" [total]="facade.total()" [hasMore]="facade.hasMore()"
                      [loading]="facade.loading()" (more)="facade.loadMore()" />
    }

    <!-- Record workspace: the routed detail renders in a wide drawer over the list. -->
    <qams-drawer [open]="detailOpen()" [title]="i18n.t('inc.title')" width="920px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .filterbar { display: flex; gap: 10px; align-items: center; padding: 10px 14px; margin-bottom: 14px; flex-wrap: wrap; }
    .search { max-width: 280px; }
    .check { display: inline-flex; align-items: center; gap: .4rem; }
    .check input { width: auto; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: .5rem 1rem; }
    .col-2 { grid-column: 1 / -1; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    .danger-text { color: var(--nt-ink-crit); font-weight: 700; }
    .pill.sentinel { background: var(--nt-ink-crit); color: #fff; margin-inline-start: .4rem; }
    .pill.anon { background: var(--nt-ink-neutral); color: #fff; margin-inline-start: .4rem; }
    .receipt { padding: .5rem 0; }
    .refbox { font-size: 1.25rem; letter-spacing: .08em; padding: .6rem .8rem; background: var(--nt-surface-alt, #f4f7fa); border-radius: 6px; display: inline-block; }
    select, button { width: auto; }
  `]
})
export class IncidentsListComponent implements OnInit {
  readonly facade = inject(IncidentsFacade);
  readonly i18n = inject(I18nService);
  readonly org = inject(OrgDataService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly categories = INCIDENT_CATEGORIES;
  readonly harmGrades = HARM_GRADES;
  readonly channels = INTAKE_CHANNELS;
  readonly statuses = INCIDENT_STATUSES;

  readonly showForm = signal(false);
  /** Whether the open report form is the anonymous variant. */
  readonly anonymous = signal(false);
  /** The one-time anonymous receipt to display after a successful anonymous report. */
  readonly receipt = signal<import('../../core/models').AnonymousIncidentReceipt | null>(null);
  /** Whether the record-workspace drawer (child route) is active. */
  readonly detailOpen = signal(false);
  readonly statusFilter = signal('');
  readonly categoryFilter = signal('');
  readonly sentinelOnly = signal(false);
  readonly search = signal('');
  /** Anonymous follow-up tracking (the receipt's promise). */
  readonly showTrack = signal(false);
  readonly trackRef = signal('');
  readonly trackResult = signal<IncidentTracking | null>(null);

  /**
   * Status, category and sentinel filter SERVER-side (a reload per change);
   * only the free-text search narrows the already-loaded pages client-side.
   */
  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    return this.facade.list().filter((inc) =>
      !q || `${inc.incidentRef} ${inc.title} ${inc.status}`.toLowerCase().includes(q));
  });

  /** Live statistics computed from the real register. */
  readonly stats = computed<ListStat[]>(() => {
    const all = this.facade.list();
    return [
      { label: this.i18n.t('stat.total'), value: all.length, tone: 'slate' },
      { label: this.i18n.t('inc.stat.open'), value: all.filter((i) => i.status !== 'Closed' && i.status !== 'Rejected').length, tone: 'blue' },
      { label: this.i18n.t('inc.stat.sentinel'), value: all.filter((i) => i.isSentinel).length, tone: 'red' },
      { label: this.i18n.t('inc.stat.severe'), value: all.filter((i) => i.harmGrade === 'Severe' || i.harmGrade === 'Death').length, tone: 'orange' },
      { label: this.i18n.t('stat.closed'), value: all.filter((i) => i.status === 'Closed').length, tone: 'green' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(300)]],
    description: ['', [Validators.maxLength(8000)]],
    category: ['Fall' as IncidentCategory, [Validators.required]],
    harmGrade: ['NoHarm' as HarmGrade, [Validators.required]],
    channel: ['Web' as IntakeChannel, [Validators.required]],
    occurredAt: ['', [Validators.required]],
    location: ['', [Validators.maxLength(200)]],
    branchId: [''],
    departmentId: [''],
  });

  ngOnInit(): void {
    void this.facade.loadList();
    void this.org.ensureOrg();
  }

  openForm(anonymous: boolean): void {
    this.anonymous.set(anonymous);
    this.receipt.set(null);
    this.showForm.set(true);
  }

  onStatusFilter(event: Event): void {
    this.statusFilter.set((event.target as HTMLSelectElement).value);
    this.reload();
  }

  onCategoryFilter(event: Event): void {
    this.categoryFilter.set((event.target as HTMLSelectElement).value);
    this.reload();
  }

  onSentinelFilter(event: Event): void {
    this.sentinelOnly.set((event.target as HTMLInputElement).checked);
    this.reload();
  }

  private reload(): void {
    void this.facade.loadList(
      this.statusFilter() || undefined, undefined, this.categoryFilter() || undefined, this.sentinelOnly());
  }

  openTrack(): void {
    this.trackResult.set(null);
    this.trackRef.set('');
    this.showTrack.set(true);
  }

  async track(): Promise<void> {
    const reference = this.trackRef().trim();
    if (!reference) { return; }
    this.trackResult.set(await this.facade.track(reference));
  }

  async create(): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    const request = {
      title: raw.title,
      description: raw.description,
      category: raw.category,
      harmGrade: raw.harmGrade,
      channel: raw.channel,
      occurredAtUtc: new Date(raw.occurredAt).toISOString(),
      location: raw.location || null,
      branchId: raw.branchId || null,
      departmentId: raw.departmentId || null,
    };

    if (this.anonymous()) {
      const receipt = await this.facade.reportAnonymous(request);
      if (receipt) {
        this.receipt.set(receipt);
        this.form.reset({ category: 'Fall', harmGrade: 'NoHarm', channel: 'Web' });
        this.reload();
      }
      return;
    }

    const id = await this.facade.report(request);
    if (id) {
      this.showForm.set(false);
      this.form.reset({ category: 'Fall', harmGrade: 'NoHarm', channel: 'Web' });
      void this.router.navigate(['/incidents', id]);
    }
  }

  open(id: string): void {
    void this.router.navigate(['/incidents', id]);
  }

  /** Dismissing the workspace drawer returns to the plain list route. */
  closeDetail(): void { void this.router.navigate(['/incidents']); }
}
