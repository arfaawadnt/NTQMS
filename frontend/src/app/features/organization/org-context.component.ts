import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { GovernanceApiService } from '../../core/api/governance-api.service';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { ContextIssue, InterestedParty, RiskListItem } from '../../core/models';
import { RiskApiService } from '../../core/api/risk-api.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { LovSelectComponent } from '../../shared/ui/lov-select.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';
import { ExportColumn, ExportMenuComponent } from '../../shared/ui/export-menu.component';

type ContextTab = 'parties' | 'issues';

/**
 * Organizational context (ISO 9001 §4.1/§4.2): the interested-parties register
 * (who has a stake, what they need, which requirements the QMS commits to) and
 * the internal/external issues register (with impact, risk-register links and
 * closure). Both are living registers — entries are revised in place with the
 * field-level audit capturing every change, and archived/closed rather than
 * deleted.
 */
@Component({
    selector: 'qams-org-context',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, PageHeaderComponent, DrawerComponent, StatusPillComponent, LovSelectComponent, AuditTrailComponent, ListStatsComponent, ExportMenuComponent],
    template: `
    <qams-page-header [title]="i18n.t('ctx.title')" [subtitle]="i18n.t('ctx.subtitle')">
      @if (tab() === 'parties') {
        <qams-export-menu [title]="i18n.t('ctx.parties')" [columns]="partyExportColumns" [rows]="parties()" />
      } @else {
        <qams-export-menu [title]="i18n.t('ctx.issues')" [columns]="issueExportColumns" [rows]="issues()" />
      }
      @if (perms.can('org-context.create')) {
        <button (click)="openCreate()">{{ tab() === 'parties' ? i18n.t('ctx.newParty') : i18n.t('ctx.newIssue') }}</button>
      }
    </qams-page-header>

    <qams-list-stats [stats]="stats()" />

    <div class="tabs">
      <button class="tab" [class.active]="tab() === 'parties'" (click)="tab.set('parties')">{{ i18n.t('ctx.parties') }}</button>
      <button class="tab" [class.active]="tab() === 'issues'" (click)="tab.set('issues')">{{ i18n.t('ctx.issues') }}</button>
    </div>
    @if (error()) { <div class="error">{{ error() }}</div> }

    @if (tab() === 'parties') {
      <div class="card">
        @if (parties().length === 0) { <p class="muted">{{ i18n.t('ctx.noParties') }}</p> }
        @else {
          <table>
            <thead><tr>
              <th>{{ i18n.t('mu.ref') }}</th><th>{{ i18n.t('std.name') }}</th><th>{{ i18n.t('fbk.type') }}</th>
              <th>{{ i18n.t('ctx.needs') }}</th><th>{{ i18n.t('ctx.reviewedOn') }}</th><th>{{ i18n.t('nc.status') }}</th>
            </tr></thead>
            <tbody>
              @for (p of parties(); track p.id) {
                <tr class="clickable" (click)="openParty(p)">
                  <td class="code">{{ p.partyRef }}</td>
                  <td><b>{{ p.name }}</b></td>
                  <td>{{ p.category }}</td>
                  <td class="muted clamp">{{ p.needsAndExpectations }}</td>
                  <td>{{ p.reviewedOn | date:'mediumDate' }}</td>
                  <td><qams-status-pill [status]="p.status" /></td>
                </tr>
              }
            </tbody>
          </table>
        }
      </div>
    } @else {
      <div class="card">
        @if (issues().length === 0) { <p class="muted">{{ i18n.t('ctx.noIssues') }}</p> }
        @else {
          <table>
            <thead><tr>
              <th>{{ i18n.t('mu.ref') }}</th><th>{{ i18n.t('fbk.type') }}</th><th>{{ i18n.t('lov.category') }}</th>
              <th>{{ i18n.t('nc.description') }}</th><th>{{ i18n.t('ctx.linkedRisk') }}</th><th>{{ i18n.t('nc.status') }}</th>
            </tr></thead>
            <tbody>
              @for (issue of issues(); track issue.id) {
                <tr class="clickable" (click)="openIssue(issue)">
                  <td class="code">{{ issue.issueRef }}</td>
                  <td>{{ i18n.t('ctx.type' + issue.type) }}</td>
                  <td>{{ issue.category }}</td>
                  <td class="muted clamp">{{ issue.description }}</td>
                  <td>{{ issue.linkedRiskId ? riskRef(issue.linkedRiskId) : '—' }}</td>
                  <td><qams-status-pill [status]="issue.status" /></td>
                </tr>
              }
            </tbody>
          </table>
        }
      </div>
    }

    <!-- Interested-party workspace drawer (create or revise in place). -->
    <qams-drawer [open]="partyOpen()" [title]="editingParty()?.partyRef ?? i18n.t('ctx.newParty')" width="720px" (closed)="partyOpen.set(false)">
      <form class="drawer-form" [formGroup]="partyForm" (ngSubmit)="saveParty()">
        <label>{{ i18n.t('std.name') }}</label>
        <input formControlName="name" />
        <label>{{ i18n.t('lov.category') }}</label>
        <qams-lov-select formControlName="category" category="INTERESTED_PARTY_CATEGORY" [placeholder]="i18n.t('ctx.categoryHint')" />
        <label>{{ i18n.t('ctx.needs') }}</label>
        <textarea rows="3" formControlName="needsAndExpectations"></textarea>
        <label>{{ i18n.t('ctx.requirements') }}</label>
        <textarea rows="2" formControlName="relevantRequirements" [placeholder]="i18n.t('common.optional')"></textarea>
        <label>{{ i18n.t('ctx.reviewedOn') }}</label>
        <input type="date" formControlName="reviewedOn" />
        <div class="row">
          @if (editingParty()?.status !== 'Archived') {
            <button type="submit" [disabled]="partyForm.invalid">{{ editingParty() ? i18n.t('ctx.saveRevision') : i18n.t('qc.create') }}</button>
          }
          @if (editingParty() && editingParty()!.status === 'Active' && perms.can('org-context.void')) {
            <button type="button" class="secondary" (click)="archiveParty(editingParty()!.id)">{{ i18n.t('ctx.archive') }}</button>
          }
        </div>
        @if (editingParty()) { <div class="hint">{{ i18n.t('ctx.livingNote') }}</div> }
      </form>
      @if (editingParty(); as p) { <qams-audit-trail [subject]="p.id" /> }
    </qams-drawer>

    <!-- Context-issue workspace drawer (create, revise, link risk, close). -->
    <qams-drawer [open]="issueOpen()" [title]="editingIssue()?.issueRef ?? i18n.t('ctx.newIssue')" width="720px" (closed)="issueOpen.set(false)">
      <form class="drawer-form" [formGroup]="issueForm" (ngSubmit)="saveIssue()">
        <label>{{ i18n.t('fbk.type') }}</label>
        <select formControlName="type">
          <option value="Internal">{{ i18n.t('ctx.typeInternal') }}</option>
          <option value="External">{{ i18n.t('ctx.typeExternal') }}</option>
        </select>
        <label>{{ i18n.t('lov.category') }}</label>
        <qams-lov-select formControlName="category" category="CONTEXT_ISSUE_CATEGORY" [placeholder]="i18n.t('ctx.issueCategoryHint')" />
        <label>{{ i18n.t('nc.description') }}</label>
        <textarea rows="3" formControlName="description"></textarea>
        <label>{{ i18n.t('ctx.impact') }}</label>
        <textarea rows="2" formControlName="impact"></textarea>
        <div class="row">
          @if (editingIssue()?.status !== 'Closed') {
            <button type="submit" [disabled]="issueForm.invalid">{{ editingIssue() ? i18n.t('ctx.saveRevision') : i18n.t('qc.create') }}</button>
          }
        </div>
      </form>
      @if (editingIssue(); as issue) {
        @if (issue.status === 'Active') {
          <div class="subpanel">
            <label>{{ i18n.t('ctx.linkRisk') }}</label>
            <div class="linkrow">
              <select [value]="riskToLink()" (change)="riskToLink.set($any($event.target).value)">
                <option value="">—</option>
                @for (r of risks(); track r.id) { <option [value]="r.id">{{ r.riskRef }} — {{ r.title }}</option> }
              </select>
              <button type="button" class="secondary" [disabled]="!riskToLink()" (click)="linkRisk(issue.id)">{{ i18n.t('ctx.link') }}</button>
            </div>
            <label>{{ i18n.t('ctx.resolution') }}</label>
            <div class="linkrow">
              <input [value]="resolution()" (input)="resolution.set($any($event.target).value)" [placeholder]="i18n.t('ctx.resolutionHint')" />
              <button type="button" [disabled]="!resolution().trim()" (click)="closeIssue(issue.id)">{{ i18n.t('ctx.closeIssue') }}</button>
            </div>
          </div>
        } @else if (issue.resolution) {
          <div class="subpanel"><b>{{ i18n.t('ctx.resolution') }}:</b> {{ issue.resolution }}</div>
        }
        <qams-audit-trail [subject]="issue.id" />
      }
    </qams-drawer>
  `,
    styles: [`
    .tabs { display: flex; gap: 0; margin-bottom: 12px; background: var(--nt-filter-grey); border-radius: 8px; padding: 3px; width: fit-content; }
    .tab { background: transparent; color: var(--nt-slate); font-size: 12.5px; padding: 7px 16px; border-radius: 6px; width: auto; }
    .tab.active { background: #fff; color: var(--nt-blue); box-shadow: var(--nt-shadow-xs); font-weight: 700; }
    .clickable { cursor: pointer; }
    .clamp { max-width: 340px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .row button { width: auto; }
    .subpanel { border-top: 1px solid var(--nt-border); margin-top: 1rem; padding-top: .75rem; }
    .linkrow { display: flex; gap: 8px; margin-bottom: .75rem; }
    .linkrow select, .linkrow input { flex: 1; }
    .linkrow button { width: auto; }
    button { width: auto; }
  `]
})
export class OrgContextComponent implements OnInit {
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly api = inject(GovernanceApiService);
  private readonly riskApi = inject(RiskApiService);
  private readonly fb = inject(FormBuilder);

  readonly tab = signal<ContextTab>('parties');
  readonly parties = signal<InterestedParty[]>([]);
  readonly issues = signal<ContextIssue[]>([]);
  readonly risks = signal<RiskListItem[]>([]);
  readonly error = signal('');

  readonly partyExportColumns: ExportColumn<InterestedParty>[] = [
    { header: 'Party Ref', cell: (r) => r.partyRef },
    { header: 'Name', cell: (r) => r.name },
    { header: 'Category', cell: (r) => r.category },
    { header: 'Needs & Expectations', cell: (r) => r.needsAndExpectations },
    { header: 'Status', cell: (r) => r.status },
  ];

  readonly issueExportColumns: ExportColumn<ContextIssue>[] = [
    { header: 'Issue Ref', cell: (r) => r.issueRef },
    { header: 'Description', cell: (r) => r.description },
    { header: 'Type', cell: (r) => r.type },
    { header: 'Category', cell: (r) => r.category },
    { header: 'Impact', cell: (r) => r.impact },
    { header: 'Status', cell: (r) => r.status },
  ];

  readonly partyOpen = signal(false);
  readonly issueOpen = signal(false);
  readonly editingParty = signal<InterestedParty | null>(null);
  readonly editingIssue = signal<ContextIssue | null>(null);
  readonly riskToLink = signal('');
  readonly resolution = signal('');

  readonly stats = computed<ListStat[]>(() => [
    { label: this.i18n.t('ctx.parties'), value: this.parties().filter((p) => p.status === 'Active').length, tone: 'blue' },
    { label: this.i18n.t('ctx.issuesActive'), value: this.issues().filter((i) => i.status === 'Active').length, tone: 'orange' },
    { label: this.i18n.t('ctx.linkedRisks'), value: this.issues().filter((i) => i.linkedRiskId !== null).length, tone: 'teal' },
    { label: this.i18n.t('ctx.issuesClosed'), value: this.issues().filter((i) => i.status === 'Closed').length, tone: 'green' },
  ]);

  readonly partyForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    category: ['', [Validators.required, Validators.maxLength(100)]],
    needsAndExpectations: ['', [Validators.required, Validators.maxLength(4000)]],
    relevantRequirements: [''],
    reviewedOn: ['', [Validators.required]],
  });
  readonly issueForm = this.fb.nonNullable.group({
    type: ['Internal', [Validators.required]],
    category: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', [Validators.required, Validators.maxLength(4000)]],
    impact: ['', [Validators.required, Validators.maxLength(4000)]],
  });

  ngOnInit(): void {
    void this.load();
    void firstValueFrom(this.riskApi.list()).then((r) => this.risks.set(r.items)).catch(() => this.risks.set([]));
  }

  riskRef(id: string): string { return this.risks().find((r) => r.id === id)?.riskRef ?? id.slice(0, 8); }

  async load(): Promise<void> {
    this.error.set('');
    try {
      this.parties.set(await firstValueFrom(this.api.parties()));
      this.issues.set(await firstValueFrom(this.api.issues()));
    } catch (err) {
      this.error.set(this.describe(err));
    }
  }

  openCreate(): void {
    if (this.tab() === 'parties') {
      this.editingParty.set(null);
      this.partyForm.reset();
      this.partyOpen.set(true);
    } else {
      this.editingIssue.set(null);
      this.issueForm.reset({ type: 'Internal' });
      this.issueOpen.set(true);
    }
  }

  openParty(party: InterestedParty): void {
    this.editingParty.set(party);
    this.partyForm.reset({
      name: party.name,
      category: party.category,
      needsAndExpectations: party.needsAndExpectations,
      relevantRequirements: party.relevantRequirements ?? '',
      reviewedOn: party.reviewedOn,
    });
    this.partyOpen.set(true);
  }

  openIssue(issue: ContextIssue): void {
    this.editingIssue.set(issue);
    this.issueForm.reset({
      type: issue.type,
      category: issue.category,
      description: issue.description,
      impact: issue.impact,
    });
    this.riskToLink.set('');
    this.resolution.set('');
    this.issueOpen.set(true);
  }

  async saveParty(): Promise<void> {
    if (this.partyForm.invalid) { return; }
    const raw = this.partyForm.getRawValue();
    const body = { ...raw, relevantRequirements: raw.relevantRequirements.trim() || null };
    await this.call(async () => {
      if (this.editingParty()) {
        await firstValueFrom(this.api.reviseParty(this.editingParty()!.id, body));
      } else {
        await firstValueFrom(this.api.registerParty(body));
      }
      this.partyOpen.set(false);
    });
  }

  async archiveParty(id: string): Promise<void> {
    await this.call(async () => {
      await firstValueFrom(this.api.archiveParty(id));
      this.partyOpen.set(false);
    });
  }

  async saveIssue(): Promise<void> {
    if (this.issueForm.invalid) { return; }
    const body = this.issueForm.getRawValue();
    await this.call(async () => {
      if (this.editingIssue()) {
        await firstValueFrom(this.api.reviseIssue(this.editingIssue()!.id, body));
      } else {
        await firstValueFrom(this.api.registerIssue(body));
      }
      this.issueOpen.set(false);
    });
  }

  async linkRisk(issueId: string): Promise<void> {
    await this.call(async () => {
      await firstValueFrom(this.api.linkIssueRisk(issueId, this.riskToLink()));
      this.issueOpen.set(false);
    });
  }

  async closeIssue(issueId: string): Promise<void> {
    await this.call(async () => {
      await firstValueFrom(this.api.closeIssue(issueId, this.resolution().trim()));
      this.issueOpen.set(false);
    });
  }

  private async call(action: () => Promise<void>): Promise<void> {
    this.error.set('');
    try {
      await action();
      await this.load();
    } catch (err) {
      this.error.set(this.describe(err));
    }
  }

  private describe(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      return (err.error as { title?: string } | null)?.title ?? `Request failed (${err.status}).`;
    }
    return 'Unexpected error.';
  }
}
