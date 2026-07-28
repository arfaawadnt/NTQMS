import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterOutlet } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthorizationsFacade } from './authorizations.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { OrgDataService } from '../../core/org-data.service';
import { ReferenceApiService } from '../../core/api/reference-api.service';
import { CompetencyApiService } from '../../core/api/competency-api.service';
import { AUTHORIZATION_SCOPES, CompetencyListItem, TestCatalogItem } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';
import { UserSelectComponent } from '../../shared/ui/user-select.component';

/**
 * Personnel authorization matrix (ISO 17025 §6.2.6): people × catalog tests,
 * each cell carrying scope chips (Perform / Review&Release / Train) colored by
 * status. Grants are evidenced by a current Authorized competency of the same
 * person — the drawer only offers those.
 */
@Component({
    selector: 'qams-authorization-matrix',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, PageHeaderComponent, DrawerComponent, RouterOutlet, ListStatsComponent, UserSelectComponent],
    template: `
    <qams-page-header [title]="i18n.t('authz.title')" [subtitle]="i18n.t('authz.subtitle')">
      @if (perms.canAssignTraining()) {
        <button (click)="showForm.set(!showForm())">{{ i18n.t('authz.new') }}</button>
      }
    </qams-page-header>

    <qams-list-stats [stats]="stats()" />

    <div class="filterbar card">
      <input class="search" [value]="search()" (input)="search.set($any($event.target).value)" [placeholder]="i18n.t('common.search')" />
      <select [value]="statusFilter()" (change)="statusFilter.set($any($event.target).value)" aria-label="Status filter">
        <option value="">{{ i18n.t('nc.allStatuses') }}</option>
        @for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
      </select>
    </div>

    <qams-drawer [open]="showForm()" [title]="i18n.t('authz.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="grant()">
        <label>{{ i18n.t('authz.person') }}</label>
        <qams-user-select formControlName="userId" />
        <label>{{ i18n.t('authz.test') }}</label>
        <select formControlName="testCatalogItemId">
          <option value="">—</option>
          @for (t of activeTests(); track t.id) { <option [value]="t.id">{{ t.testCode }} — {{ t.testName }}</option> }
        </select>
        <label>{{ i18n.t('authz.scope') }}</label>
        <select formControlName="scope">
          @for (s of scopes; track s) { <option [value]="s">{{ i18n.t('authz.scope' + s) }}</option> }
        </select>
        <label>{{ i18n.t('authz.evidence') }}</label>
        <select formControlName="competencyRecordId">
          <option value="">—</option>
          @for (c of evidence(); track c.id) {
            <option [value]="c.id">{{ c.subject }} ({{ i18n.t('authz.until') }} {{ c.expiresAt }})</option>
          }
        </select>
        <div class="hint">{{ i18n.t('authz.evidenceHint') }}</div>
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('authz.grant') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (matrixUsers().length === 0) {
      <p class="muted">{{ i18n.t('authz.empty') }}</p>
    } @else {
      <div class="card matrix-wrap">
        <table class="matrix">
          <thead>
            <tr>
              <th class="sticky">{{ i18n.t('authz.person') }}</th>
              @for (t of matrixTests(); track t.id) {
                <th><span class="code">{{ t.code }}</span><br /><span class="muted small">{{ t.name }}</span></th>
              }
            </tr>
          </thead>
          <tbody>
            @for (u of matrixUsers(); track u.id) {
              <tr>
                <td class="sticky"><b>{{ u.name }}</b></td>
                @for (t of matrixTests(); track t.id) {
                  <td>
                    @for (a of cell(u.id, t.id); track a.id) {
                      <button type="button" class="chip" [class]="'chip ' + a.status.toLowerCase()"
                              [title]="a.scope + ' · ' + a.status + ' · ' + i18n.t('authz.until') + ' ' + a.expiresOn"
                              (click)="open(a.id)">{{ scopeInitial(a.scope) }}</button>
                    }
                  </td>
                }
              </tr>
            }
          </tbody>
        </table>
      </div>
      <div class="legend muted">
        {{ i18n.t('authz.legend') }} — P = {{ i18n.t('authz.scopePerform') }},
        R = {{ i18n.t('authz.scopeReviewAndRelease') }}, T = {{ i18n.t('authz.scopeTrain') }}
      </div>
    }

    <!-- Record workspace: the routed detail renders in a wide drawer over the matrix. -->
    <qams-drawer [open]="detailOpen()" [title]="i18n.t('authz.title')" width="920px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .filterbar { display: flex; gap: 10px; align-items: center; padding: 10px 14px; margin-bottom: 14px; flex-wrap: wrap; }
    .search { max-width: 280px; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    button, select { width: auto; }
    .matrix-wrap { overflow-x: auto; }
    .matrix th, .matrix td { text-align: center; vertical-align: middle; min-width: 90px; }
    .matrix .sticky { position: sticky; inset-inline-start: 0; background: var(--nt-card-bg, #fff); text-align: start; z-index: 1; }
    .small { font-size: .7rem; font-weight: normal; }
    .chip { display: inline-block; width: 26px; height: 26px; margin: 1px; padding: 0; border: none; border-radius: 50%;
            font-weight: 700; font-size: .75rem; color: #fff; cursor: pointer; }
    .chip.active { background: var(--nt-green, #2e7d32); }
    .chip.suspended { background: var(--nt-orange, #ef6c00); }
    .chip.expired { background: var(--nt-slate, #3B4658); }
    .chip.revoked { background: var(--nt-red, #c62828); }
    .legend { margin-top: .5rem; font-size: .8rem; }
  `]
})
export class AuthorizationMatrixComponent implements OnInit {
  readonly facade = inject(AuthorizationsFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  readonly org = inject(OrgDataService);
  private readonly referenceApi = inject(ReferenceApiService);
  private readonly competencyApi = inject(CompetencyApiService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly statuses = ['Active', 'Suspended', 'Expired', 'Revoked'];
  readonly scopes = AUTHORIZATION_SCOPES;
  readonly showForm = signal(false);
  /** Whether the record-workspace drawer (child route) is active. */
  readonly detailOpen = signal(false);
  readonly statusFilter = signal('');
  readonly search = signal('');

  private readonly tests = signal<TestCatalogItem[]>([]);
  readonly activeTests = computed(() => this.tests().filter((t) => t.isActive));
  /** Authorized competencies of the person picked in the grant drawer. */
  readonly evidence = signal<CompetencyListItem[]>([]);

  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    const status = this.statusFilter();
    return this.facade.list().filter((a) =>
      (!status || a.status === status)
      && (!q || `${a.testCode} ${a.testName} ${this.org.userName(a.userId)} ${a.scope} ${a.status}`.toLowerCase().includes(q)));
  });

  /** Matrix rows: every person holding at least one (filtered) authorization. */
  readonly matrixUsers = computed(() => {
    const ids = [...new Set(this.filtered().map((a) => a.userId))];
    return ids
      .map((id) => ({ id, name: this.org.userName(id) || id.slice(0, 8) }))
      .sort((a, b) => a.name.localeCompare(b.name));
  });

  /** Matrix columns: every test appearing in the (filtered) register. */
  readonly matrixTests = computed(() => {
    const seen = new Map<string, { id: string; code: string; name: string }>();
    for (const a of this.filtered()) {
      seen.set(a.testCatalogItemId, { id: a.testCatalogItemId, code: a.testCode, name: a.testName });
    }
    return [...seen.values()].sort((a, b) => a.code.localeCompare(b.code));
  });

  readonly stats = computed<ListStat[]>(() => {
    const all = this.facade.list();
    const in30 = new Date(Date.now() + 30 * 86_400_000).toISOString().slice(0, 10);
    return [
      { label: this.i18n.t('stat.total'), value: all.length, tone: 'slate' },
      { label: this.i18n.t('std.active'), value: all.filter((a) => a.status === 'Active').length, tone: 'green' },
      { label: this.i18n.t('authz.suspended'), value: all.filter((a) => a.status === 'Suspended').length, tone: 'orange' },
      { label: this.i18n.t('authz.expiringSoon'), value: all.filter((a) => a.status === 'Active' && a.expiresOn <= in30).length, tone: 'gold' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    userId: ['', [Validators.required]],
    testCatalogItemId: ['', [Validators.required]],
    scope: ['Perform' as (typeof AUTHORIZATION_SCOPES)[number], [Validators.required]],
    competencyRecordId: ['', [Validators.required]],
  });

  ngOnInit(): void {
    void this.facade.loadList();
    void this.org.ensureDirectory();
    void firstValueFrom(this.referenceApi.testCatalog())
      .then((tests) => this.tests.set(tests))
      .catch(() => this.tests.set([]));
    // The evidence dropdown tracks the picked person; unsubscribed with the component.
    this.form.controls.userId.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((userId) => {
      this.form.controls.competencyRecordId.setValue('');
      if (!userId) { this.evidence.set([]); return; }
      void firstValueFrom(this.competencyApi.listCompetencies(userId, 'Authorized'))
        .then((records) => this.evidence.set(records.items))
        .catch(() => this.evidence.set([]));
    });
  }

  cell(userId: string, testId: string) {
    return this.filtered().filter((a) => a.userId === userId && a.testCatalogItemId === testId);
  }

  scopeInitial(scope: string): string {
    return scope === 'ReviewAndRelease' ? 'R' : scope === 'Train' ? 'T' : 'P';
  }

  async grant(): Promise<void> {
    if (this.form.invalid) { return; }
    const id = await this.facade.grant(this.form.getRawValue());
    if (id) { this.cancel(); void this.router.navigate(['/authorizations', id]); }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset({ scope: 'Perform' });
    this.evidence.set([]);
  }

  open(id: string): void { void this.router.navigate(['/authorizations', id]); }

  /** Dismissing the workspace drawer returns to the plain matrix route. */
  closeDetail(): void { void this.router.navigate(['/authorizations']); }
}
