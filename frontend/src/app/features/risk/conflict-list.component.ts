import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterOutlet } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { GovernanceApiService } from '../../core/api/governance-api.service';
import { I18nService } from '../../core/i18n.service';
import { OrgDataService } from '../../core/org-data.service';
import { ConflictListItem } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { UserSelectComponent } from '../../shared/ui/user-select.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';

/** Impartiality / conflict-of-interest register (ISO 17025 §4.1). */
@Component({
    selector: 'qams-conflict-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, UserSelectComponent, ListStatsComponent],
    template: `
    <qams-page-header [title]="i18n.t('coi.title')" [subtitle]="i18n.t('coi.subtitle')">
      <button (click)="showForm.set(!showForm())">{{ i18n.t('coi.new') }}</button>
    </qams-page-header>

    <qams-list-stats [stats]="stats()" ratioFromFirst />

    <div class="filterbar card">
      <input class="search" [value]="search()" (input)="search.set($any($event.target).value)" [placeholder]="i18n.t('common.search')" />
      <select [value]="statusFilter()" (change)="onFilter($event)" aria-label="Status filter">
        <option value="">{{ i18n.t('nc.allStatuses') }}</option>
        @for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
      </select>
    </div>

    <qams-drawer [open]="showForm()" [title]="i18n.t('coi.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="declare()">
        <label>{{ i18n.t('coi.declarant') }}</label>
        <qams-user-select formControlName="declarantId" />
        <label>{{ i18n.t('coi.relatedParty') }}</label>
        <input formControlName="relatedParty" [placeholder]="i18n.t('coi.relatedPartyHint')" />
        <label>{{ i18n.t('nc.description') }}</label>
        <textarea rows="3" formControlName="description" [placeholder]="i18n.t('coi.descriptionHint')"></textarea>
        <label>{{ i18n.t('coi.declaredOn') }}</label>
        <input type="date" formControlName="declaredOn" />
        <div class="row">
          <button type="submit" [disabled]="form.invalid || loading()">{{ i18n.t('coi.declare') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (error()) { <div class="error">{{ error() }}</div> }
      </form>
    </qams-drawer>

    @if (loading() && list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (filtered().length === 0) {
      <p class="muted">{{ i18n.t('coi.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('mu.ref') }}</th><th>{{ i18n.t('coi.declarant') }}</th><th>{{ i18n.t('coi.relatedParty') }}</th>
            <th>{{ i18n.t('coi.declaredOn') }}</th><th>{{ i18n.t('coi.risk') }}</th><th>{{ i18n.t('nc.status') }}</th>
          </tr></thead>
          <tbody>
            @for (c of filtered(); track c.id) {
              <tr class="clickable" (click)="open(c.id)">
                <td class="code">{{ c.conflictRef }}</td>
                <td>{{ org.userName(c.declarantId) || '—' }}</td>
                <td>{{ c.relatedParty }}</td>
                <td>{{ c.declaredOn | date:'mediumDate' }}</td>
                <td>
                  @if (c.riskLevel) { <span [class]="'risk ' + c.riskLevel.toLowerCase()">{{ c.riskLevel }}</span> }
                  @else { — }
                </td>
                <td><qams-status-pill [status]="c.status" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <!-- Record workspace: the routed detail renders in a wide drawer over the list. -->
    <qams-drawer [open]="detailOpen()" [title]="i18n.t('coi.title')" width="920px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .filterbar { display: flex; gap: 10px; align-items: center; padding: 10px 14px; margin-bottom: 14px; flex-wrap: wrap; }
    .search { max-width: 280px; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    button, select { width: auto; }
    .risk { font-weight: 700; }
    .risk.low { color: var(--nt-green); }
    .risk.medium { color: var(--nt-orange, #ef6c00); }
    .risk.high { color: var(--nt-red); }
  `]
})
export class ConflictListComponent implements OnInit {
  readonly i18n = inject(I18nService);
  readonly org = inject(OrgDataService);
  private readonly api = inject(GovernanceApiService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly statuses = ['Declared', 'Assessed', 'Closed'];
  readonly showForm = signal(false);
  /** Whether the record-workspace drawer (child route) is active. */
  readonly detailOpen = signal(false);
  readonly statusFilter = signal('');
  readonly search = signal('');
  readonly list = signal<ConflictListItem[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');

  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    return this.list().filter((c) =>
      !q || `${c.conflictRef} ${this.org.userName(c.declarantId)} ${c.relatedParty} ${c.status}`.toLowerCase().includes(q));
  });

  readonly stats = computed<ListStat[]>(() => {
    const all = this.list();
    return [
      { label: this.i18n.t('stat.total'), value: all.length, tone: 'slate' },
      { label: this.i18n.t('coi.pending'), value: all.filter((c) => c.status === 'Declared').length, tone: 'orange' },
      { label: this.i18n.t('coi.highRisk'), value: all.filter((c) => c.riskLevel === 'High' && c.status !== 'Closed').length, tone: 'red' },
      { label: this.i18n.t('coi.closed'), value: all.filter((c) => c.status === 'Closed').length, tone: 'green' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    declarantId: ['', [Validators.required]],
    relatedParty: ['', [Validators.required, Validators.maxLength(300)]],
    description: ['', [Validators.required, Validators.maxLength(2000)]],
    declaredOn: ['', [Validators.required]],
  });

  ngOnInit(): void {
    void this.load();
    void this.org.ensureDirectory();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      this.list.set(await firstValueFrom(this.api.listConflicts(this.statusFilter() || undefined)));
    } catch (err) {
      this.error.set(err instanceof HttpErrorResponse
        ? ((err.error as { title?: string } | null)?.title ?? `Request failed (${err.status}).`)
        : 'Unexpected error.');
    } finally {
      this.loading.set(false);
    }
  }

  onFilter(event: Event): void {
    this.statusFilter.set((event.target as HTMLSelectElement).value);
    void this.load();
  }

  async declare(): Promise<void> {
    if (this.form.invalid) { return; }
    this.error.set('');
    try {
      const created = await firstValueFrom(this.api.declareConflict(this.form.getRawValue()));
      this.cancel();
      await this.load();
      void this.router.navigate(['/conflicts', created.id]);
    } catch (err) {
      this.error.set(err instanceof HttpErrorResponse
        ? ((err.error as { title?: string } | null)?.title ?? `Request failed (${err.status}).`)
        : 'Unexpected error.');
    }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset();
  }

  open(id: string): void { void this.router.navigate(['/conflicts', id]); }

  /** Dismissing the workspace drawer returns to the plain list route. */
  closeDetail(): void {
    void this.router.navigate(['/conflicts']);
    void this.load();
  }
}
