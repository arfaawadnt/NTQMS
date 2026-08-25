import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuditProgramsFacade } from './audit-programs.facade';
import { AuditsApiService } from '../../core/api/audits-api.service';
import { I18nService } from '../../core/i18n.service';
import { OrgDataService } from '../../core/org-data.service';
import { PermissionsService } from '../../core/permissions.service';
import { AuditListItem, PLANNED_AUDIT_PRIORITIES, PlannedAudit, PlannedAuditPriority } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

interface QuarterGroup { quarter: number; lines: PlannedAudit[]; }

/**
 * Annual audit-programme workspace (HQMS M05): coverage at a glance, the plan grouped by
 * quarter, and the actions that move each line from planned → scheduled → completed.
 * Editing is privilege-gated (affordance only).
 */
@Component({
    selector: 'qams-audit-programs-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, DecimalPipe, RouterLink, PageHeaderComponent, StatusPillComponent, AuditTrailComponent],
    template: `
    @if (program(); as p) {
      <qams-page-header [title]="p.year + ' — ' + p.title">
        <a routerLink="/audit-programs" class="ghost-link">← {{ i18n.t('apg.backToList') }}</a>
      </qams-page-header>

      <div class="meta">
        <div><span class="muted">{{ i18n.t('apg.status') }}</span><qams-status-pill [status]="p.status" /></div>
        @if (p.status === 'Draft' && perms.can('audits.approve')) {
          <button (click)="facade.activate(p.id)">{{ i18n.t('apg.activate') }}</button>
        }
        @if (p.status === 'Active' && perms.can('audits.void')) {
          <button class="secondary" (click)="facade.close(p.id)">{{ i18n.t('apg.close') }}</button>
        }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      <!-- Coverage -->
      <section class="card coverage">
        <div class="cov-item"><span class="big">{{ p.coverage.coveragePercent | number:'1.0-0' }}%</span><span class="muted">{{ i18n.t('apg.coverage') }}</span></div>
        <div class="cov-bars">
          <div class="cov-line"><span class="lbl">{{ i18n.t('apg.completed') }}</span><div class="bar"><span class="ok" [style.width.%]="p.coverage.coveragePercent"></span></div><span class="n">{{ p.coverage.completed }}/{{ p.coverage.planned }}</span></div>
          <div class="cov-line"><span class="lbl">{{ i18n.t('apg.scheduledOrDone') }}</span><div class="bar"><span class="info" [style.width.%]="p.coverage.scheduledPercent"></span></div><span class="n">{{ p.coverage.scheduledPercent | number:'1.0-0' }}%</span></div>
        </div>
      </section>

      <!-- Add plan line -->
      @if (p.status !== 'Closed' && perms.can('audits.create')) {
        <section class="card">
          <h3>{{ i18n.t('apg.addLine') }}</h3>
          <form class="drawer-form" [formGroup]="lineForm" (ngSubmit)="addLine(p.id)">
            <div class="grid">
              <div class="col-2"><label>{{ i18n.t('apg.scopeArea') }}</label><input formControlName="scopeArea" /></div>
              <div>
                <label>{{ i18n.t('apg.department') }}</label>
                <select formControlName="departmentId">
                  <option value="">{{ i18n.t('apg.noDepartment') }}</option>
                  @for (d of org.departments(); track d.id) { <option [value]="d.id">{{ d.name }}</option> }
                </select>
              </div>
              <div><label>{{ i18n.t('apg.standardChapter') }}</label><input formControlName="standardChapter" /></div>
              <div>
                <label>{{ i18n.t('apg.priority') }}</label>
                <select formControlName="priority">@for (pr of priorities; track pr) { <option [value]="pr">{{ i18n.t('apg.pr.' + pr) }}</option> }</select>
              </div>
              <div><label>{{ i18n.t('apg.quarter') }} (1-4)</label><input type="number" min="1" max="4" formControlName="plannedQuarter" /></div>
            </div>
            <button type="submit" [disabled]="lineForm.invalid">{{ i18n.t('apg.addLine') }}</button>
          </form>
        </section>
      }

      <!-- Plan grouped by quarter -->
      <section class="card">
        <h3>{{ i18n.t('apg.plan') }} ({{ p.plan.length }})</h3>
        @if (p.plan.length === 0) { <p class="muted">{{ i18n.t('apg.noPlan') }}</p> }
        @for (qg of quarters(); track qg.quarter) {
          <h4>Q{{ qg.quarter }}</h4>
          <table>
            <thead><tr>
              <th>{{ i18n.t('apg.scopeArea') }}</th><th>{{ i18n.t('apg.standardChapter') }}</th>
              <th>{{ i18n.t('apg.priority') }}</th><th>{{ i18n.t('apg.status') }}</th><th></th>
            </tr></thead>
            <tbody>
              @for (l of qg.lines; track l.id) {
                <tr>
                  <td>{{ l.scopeArea }} @if (l.departmentId) { <span class="muted">· {{ org.departmentName(l.departmentId) }}</span> }</td>
                  <td>{{ l.standardChapter || '—' }}</td>
                  <td>{{ i18n.t('apg.pr.' + l.priority) }}</td>
                  <td><qams-status-pill [status]="l.status" /> @if (l.completedOn) { <span class="muted">{{ l.completedOn | date:'mediumDate' }}</span> }</td>
                  <td class="actions">
                    @if (p.status === 'Active' && perms.can('audits.approve')) {
                      @if (l.status === 'Planned') {
                        <button class="link" (click)="scheduleLineId.set(scheduleLineId() === l.id ? null : l.id)">{{ i18n.t('apg.schedule') }}</button>
                      } @else if (l.status === 'Scheduled') {
                        <button class="link" (click)="completeLineId.set(completeLineId() === l.id ? null : l.id)">{{ i18n.t('apg.complete') }}</button>
                      }
                    }
                  </td>
                </tr>
                @if (scheduleLineId() === l.id) {
                  <tr class="sub"><td colspan="5">
                    <div class="inline">
                      <select #auditSel>
                        <option value="">{{ i18n.t('apg.pickAudit') }}</option>
                        @for (a of audits(); track a.id) { <option [value]="a.id">{{ a.auditRef }} — {{ a.title }}</option> }
                      </select>
                      <button [disabled]="!auditSel.value" (click)="schedule(p.id, l.id, auditSel.value)">{{ i18n.t('apg.linkAudit') }}</button>
                    </div>
                  </td></tr>
                }
                @if (completeLineId() === l.id) {
                  <tr class="sub"><td colspan="5">
                    <div class="inline">
                      <input type="date" #dateInp />
                      <button [disabled]="!dateInp.value" (click)="complete(p.id, l.id, dateInp.value)">{{ i18n.t('apg.markComplete') }}</button>
                    </div>
                  </td></tr>
                }
              }
            </tbody>
          </table>
        }
      </section>

      <qams-audit-trail [subject]="p.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1rem; align-items: center; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .meta button { width: auto; }
    .coverage { display: flex; gap: 2rem; align-items: center; flex-wrap: wrap; }
    .cov-item .big { font-size: 2.2rem; font-weight: 700; display: block; color: var(--nt-ink-info); }
    .cov-bars { flex: 1; min-width: 240px; display: grid; gap: .4rem; }
    .cov-line { display: grid; grid-template-columns: 130px 1fr 60px; gap: .6rem; align-items: center; font-size: .85rem; }
    .bar { height: 9px; background: #e6ebf1; border-radius: 5px; overflow: hidden; }
    .bar > span { display: block; height: 100%; }
    .bar > span.ok { background: var(--nt-ink-ok); } .bar > span.info { background: var(--nt-ink-info); }
    .n { text-align: end; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: .5rem 1rem; }
    .col-2 { grid-column: span 2; }
    .inline { display: flex; gap: .5rem; flex-wrap: wrap; align-items: center; }
    .sub td { background: var(--nt-surface-alt, #f4f7fa); }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    select, button, input { width: auto; }
    h4 { margin-top: 1rem; }
  `]
})
export class AuditProgramsDetailComponent implements OnInit {
  readonly facade = inject(AuditProgramsFacade);
  readonly i18n = inject(I18nService);
  readonly org = inject(OrgDataService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly auditsApi = inject(AuditsApiService);

  readonly id = input.required<string>();
  readonly program = this.facade.selected;
  readonly priorities = PLANNED_AUDIT_PRIORITIES;

  /** Audits available to fulfil a plan line (loaded once). */
  readonly audits = signal<AuditListItem[]>([]);
  readonly scheduleLineId = signal<string | null>(null);
  readonly completeLineId = signal<string | null>(null);

  readonly quarters = computed<QuarterGroup[]>(() => {
    const groups = new Map<number, QuarterGroup>();
    for (const l of this.program()?.plan ?? []) {
      const g = groups.get(l.plannedQuarter) ?? { quarter: l.plannedQuarter, lines: [] };
      g.lines.push(l);
      groups.set(l.plannedQuarter, g);
    }
    return [...groups.values()].sort((a, b) => a.quarter - b.quarter);
  });

  readonly lineForm = this.fb.nonNullable.group({
    scopeArea: ['', [Validators.required, Validators.maxLength(200)]],
    departmentId: [''],
    standardChapter: ['', [Validators.maxLength(120)]],
    priority: ['Medium' as PlannedAuditPriority, [Validators.required]],
    plannedQuarter: [1, [Validators.required, Validators.min(1), Validators.max(4)]],
  });

  ngOnInit(): void {
    void this.facade.loadDetail(this.id());
    void this.org.ensureOrg();
    void this.loadAudits();
  }

  private async loadAudits(): Promise<void> {
    try {
      this.audits.set((await firstValueFrom(this.auditsApi.list(undefined, 1, 200))).items);
    } catch {
      this.audits.set([]);
    }
  }

  async addLine(id: string): Promise<void> {
    if (this.lineForm.invalid) { return; }
    const raw = this.lineForm.getRawValue();
    await this.facade.addPlanned(id, {
      scopeArea: raw.scopeArea,
      departmentId: raw.departmentId || null,
      standardChapter: raw.standardChapter || null,
      priority: raw.priority,
      plannedQuarter: raw.plannedQuarter,
    });
    if (this.facade.error() === '') { this.lineForm.reset({ priority: 'Medium', plannedQuarter: 1 }); }
  }

  async schedule(id: string, lineId: string, auditId: string): Promise<void> {
    if (!auditId) { return; }
    await this.facade.schedule(id, lineId, { auditId });
    if (this.facade.error() === '') { this.scheduleLineId.set(null); }
  }

  async complete(id: string, lineId: string, completedOn: string): Promise<void> {
    if (!completedOn) { return; }
    await this.facade.complete(id, lineId, { completedOn });
    if (this.facade.error() === '') { this.completeLineId.set(null); }
  }
}
