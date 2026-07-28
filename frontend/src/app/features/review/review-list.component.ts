import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { ReviewFacade } from './review.facade';
import { I18nService } from '../../core/i18n.service';
import { OrgDataService } from '../../core/org-data.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { UserSelectComponent } from '../../shared/ui/user-select.component';
import { AllocationPickerComponent } from '../../shared/ui/allocation-picker.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';
import { LoadMoreComponent } from '../../shared/ui/load-more.component';

/** Management review list: live statistics, filterable list + a schedule form (QM-gated). */
@Component({
    selector: 'qams-review-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, AllocationPickerComponent, ListStatsComponent, UserSelectComponent, LoadMoreComponent],
    template: `
    <qams-page-header [title]="i18n.t('mrv.title')">
      @if (perms.canApprove()) {
        <button (click)="showForm.set(!showForm())">{{ i18n.t('mrv.new') }}</button>
      }
    </qams-page-header>

    <qams-list-stats [stats]="stats()" />

    <div class="filterbar card">
      <input class="search" [value]="search()" (input)="search.set($any($event.target).value)" [placeholder]="i18n.t('common.search')" />
      <select [value]="branchFilter()" (change)="branchFilter.set($any($event.target).value)" aria-label="Branch filter">
        <option value="">{{ i18n.t('alloc.allBranches') }}</option>
        @for (b of org.branches(); track b.id) { <option [value]="b.id">{{ b.code }} — {{ b.name }}</option> }
      </select>
    </div>

    <qams-drawer [open]="showForm()" [title]="i18n.t('mrv.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="schedule()">
        <div class="grid">
          <div class="col-2"><label>{{ i18n.t('mrv.reviewTitle') }}</label><input formControlName="title" /></div>
          <div><label>{{ i18n.t('mrv.reviewDate') }}</label><input type="date" formControlName="reviewDate" /></div>
        </div>
        <label>{{ i18n.t('mrv.participants') }}</label>
        <qams-user-select formControlName="participantIds" [multiple]="true" />
        <div class="hint">{{ i18n.t('mrv.participantsPick') }}</div>
        <qams-allocation-picker [branchCtrl]="form.controls.branchId" [departmentCtrl]="form.controls.departmentId" />
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('mrv.schedule') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (filtered().length === 0) {
      <p class="muted">{{ i18n.t('mrv.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('mrv.ref') }}</th><th>{{ i18n.t('mrv.reviewTitle') }}</th><th>{{ i18n.t('mrv.reviewDate') }}</th>
            <th>{{ i18n.t('nc.status') }}</th><th>{{ i18n.t('mrv.decisions') }}</th>
            <th>{{ i18n.t('alloc.branch') }}</th>
          </tr></thead>
          <tbody>
            @for (r of filtered(); track r.id) {
              <tr class="clickable" (click)="open(r.id)">
                <td>{{ r.reviewRef }}</td><td>{{ r.title }}</td><td>{{ r.reviewDate | date:'mediumDate' }}</td>
                <td><qams-status-pill [status]="r.status" /></td>
                <td>{{ r.decisionCount }}</td>
                <td class="code">{{ org.branchName(r.branchId) || '—' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      <qams-load-more [shown]="facade.list().length" [total]="facade.total()" [hasMore]="facade.hasMore()"
                      [loading]="facade.loading()" (more)="facade.loadMore()" />
    }

    <!-- Record workspace: the routed detail renders in a wide drawer over the list. -->
    <qams-drawer [open]="detailOpen()" [title]="i18n.t('mrv.title')" width="920px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .filterbar { display: flex; gap: 10px; align-items: center; padding: 10px 14px; margin-bottom: 14px; flex-wrap: wrap; }
    .search { max-width: 280px; }
    .form { margin-bottom: 1rem; }
    .form textarea { width: 100%; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: .5rem 1rem; }
    .col-2 { grid-column: span 2; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    button, select { width: auto; }
  `]
})
export class ReviewListComponent implements OnInit {
  readonly facade = inject(ReviewFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  readonly org = inject(OrgDataService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly showForm = signal(false);
  /** Whether the record-workspace drawer (child route) is active. */
  readonly detailOpen = signal(false);
  readonly search = signal('');
  readonly branchFilter = signal('');

  /** Client-side filtration over the loaded register. */
  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    const branch = this.branchFilter();
    return this.facade.list().filter((r) =>
      (!branch || r.branchId === branch)
      && (!q || `${r.reviewRef} ${r.title} ${r.status}`.toLowerCase().includes(q)));
  });

  /** Live statistics computed from the real register. */
  readonly stats = computed<ListStat[]>(() => {
    const all = this.facade.list();
    return [
      { label: this.i18n.t('stat.total'), value: all.length, tone: 'slate' },
      { label: this.i18n.t('stat.scheduled'), value: all.filter((r) => r.status === 'Scheduled').length, tone: 'blue' },
      { label: this.i18n.t('stat.closed'), value: all.filter((r) => r.status === 'Closed').length, tone: 'green' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    reviewDate: ['', [Validators.required]],
    participantIds: [[] as string[], [Validators.required]],
    branchId: [''],
    departmentId: [''],
  });

  ngOnInit(): void {
    void this.facade.loadList();
    void this.org.ensureOrg();
  }

  async schedule(): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    const id = await this.facade.schedule({
      title: raw.title,
      reviewDate: raw.reviewDate,
      // The domain stores participants as the human-readable minutes string —
      // resolve the picked users to their display names.
      participants: raw.participantIds.map((uid) => this.org.userName(uid)).filter(Boolean).join(', '),
      branchId: raw.branchId || null,
      departmentId: raw.departmentId || null,
    });
    if (id) { this.cancel(); void this.router.navigate(['/management-reviews', id]); }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset();
  }

  open(id: string): void { void this.router.navigate(['/management-reviews', id]); }

  /** Dismissing the workspace drawer returns to the plain list route. */
  closeDetail(): void { void this.router.navigate(['/management-reviews']); }
}
