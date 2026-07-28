import { ChangeDetectionStrategy, Component, OnInit, inject, input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ObjectivesFacade } from './objectives.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { OrgDataService } from '../../core/org-data.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Objective workspace: the target and live on-target verdict, dated progress
 * measurements (append-only), and honest closure — Achieved is refused by the
 * backend when the latest measurement misses the target.
 */
@Component({
    selector: 'qams-objective-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, DecimalPipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent],
    template: `
    @if (item(); as o) {
      <qams-page-header [title]="o.objectiveRef + ' — ' + o.title" [subtitle]="o.metric">
        <a routerLink="/quality-objectives" class="ghost-link">← {{ i18n.t('obj.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="o.status" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="o.status" /></div>
        <div><span class="muted">{{ i18n.t('obj.target') }}</span> <b>{{ i18n.t('obj.dir' + o.direction) }} {{ o.targetValue | number:'1.0-2' }} {{ o.unit }}</b></div>
        <div>
          <span class="muted">{{ i18n.t('obj.current') }}</span>
          @if (o.currentValue !== null) {
            <b [class.good]="o.onTarget === true" [class.bad]="o.onTarget === false">{{ o.currentValue | number:'1.0-2' }} {{ o.unit }}</b>
          } @else { — }
        </div>
        <div><span class="muted">{{ i18n.t('mrv.owner') }}</span> {{ org.userName(o.ownerId) || '—' }}</div>
        <div><span class="muted">{{ i18n.t('atr.period') }}</span> {{ o.periodStart | date:'mediumDate' }} – {{ o.periodEnd | date:'mediumDate' }}</div>
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      @if (o.description) { <section class="card"><p>{{ o.description }}</p></section> }

      <section class="card">
        <h3>{{ i18n.t('obj.progress') }} ({{ o.updates.length }})</h3>
        @if (o.updates.length === 0) { <p class="muted">{{ i18n.t('obj.noProgress') }}</p> }
        @else {
          <table>
            <thead><tr>
              <th>{{ i18n.t('obj.measuredOn') }}</th><th>{{ i18n.t('env.value') }}</th>
              <th>{{ i18n.t('equip.performedBy') }}</th><th>{{ i18n.t('obj.comment') }}</th>
            </tr></thead>
            <tbody>
              @for (u of o.updates; track u.id) {
                <tr>
                  <td>{{ u.measuredOn | date:'mediumDate' }}</td>
                  <td><b>{{ u.value | number:'1.0-2' }} {{ o.unit }}</b></td>
                  <td>{{ org.userName(u.recordedById) || '—' }}</td>
                  <td class="muted">{{ u.comment ?? '—' }}</td>
                </tr>
              }
            </tbody>
          </table>
        }
        @if (o.status === 'Active') {
          <form [formGroup]="progressForm" (ngSubmit)="record(o.id)">
            <div class="trio">
              <div><label>{{ i18n.t('obj.measuredOn') }}</label><input type="date" formControlName="measuredOn" /></div>
              <div><label>{{ i18n.t('env.value') }} ({{ o.unit }})</label><input type="number" step="any" formControlName="value" /></div>
              <div><label>{{ i18n.t('obj.comment') }}</label><input formControlName="comment" /></div>
            </div>
            <button type="submit" [disabled]="progressForm.invalid">{{ i18n.t('obj.recordProgress') }}</button>
          </form>
        }
      </section>

      @if (o.status === 'Active' && perms.canApprove()) {
        <section class="card">
          <h3>{{ i18n.t('obj.close') }}</h3>
          <form [formGroup]="closeForm" (ngSubmit)="close(o.id)">
            <div class="pair">
              <div>
                <label>{{ i18n.t('val.verdict') }}</label>
                <select formControlName="outcome">
                  <option value="Achieved">{{ i18n.t('obj.achievedOpt') }}</option>
                  <option value="Missed">{{ i18n.t('obj.missedOpt') }}</option>
                  <option value="Cancelled">{{ i18n.t('obj.cancelledOpt') }}</option>
                </select>
              </div>
              <div><label>{{ i18n.t('obj.note') }}</label><input formControlName="note" [placeholder]="i18n.t('obj.noteHint')" /></div>
            </div>
            <div class="hint">{{ i18n.t('obj.honestNote') }}</div>
            <button type="submit" class="secondary" [disabled]="closeForm.invalid">{{ i18n.t('obj.closeBtn') }}</button>
          </form>
        </section>
      }
      @if (o.closureNote) {
        <section class="card"><h3>{{ i18n.t('obj.note') }}</h3><p>{{ o.closureNote }}</p></section>
      }

      <qams-audit-trail [subject]="o.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; align-items: center; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    section { margin-bottom: 1rem; }
    .trio { display: grid; grid-template-columns: 1fr 1fr 2fr; gap: 1rem; }
    .pair { display: grid; grid-template-columns: 1fr 2fr; gap: 1rem; }
    .good { color: var(--nt-green); }
    .bad { color: var(--nt-red); }
    form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    form button { width: auto; margin-top: .5rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    @media (max-width: 800px) { .trio, .pair { grid-template-columns: 1fr; } }
  `]
})
export class ObjectiveDetailComponent implements OnInit {
  readonly facade = inject(ObjectivesFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  readonly org = inject(OrgDataService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound objective id. */
  readonly id = input.required<string>();

  /** Canonical path (Missed/Cancelled render off-path). */
  readonly flowSteps = ['Active', 'Achieved'] as const;

  readonly item = this.facade.selected;

  readonly progressForm = this.fb.nonNullable.group({
    measuredOn: ['', [Validators.required]],
    value: [null as number | null, [Validators.required]],
    comment: ['', [Validators.maxLength(1000)]],
  });
  readonly closeForm = this.fb.nonNullable.group({
    outcome: ['Achieved', [Validators.required]],
    note: ['', [Validators.required, Validators.maxLength(2000)]],
  });

  ngOnInit(): void {
    void this.facade.loadDetail(this.id());
    void this.org.ensureDirectory();
  }

  async record(id: string): Promise<void> {
    if (this.progressForm.invalid) { return; }
    const raw = this.progressForm.getRawValue();
    await this.facade.recordProgress(id, raw.measuredOn, raw.value!, raw.comment.trim() || null);
    this.progressForm.reset();
  }

  async close(id: string): Promise<void> {
    if (this.closeForm.invalid) { return; }
    const raw = this.closeForm.getRawValue();
    await this.facade.close(id, raw.outcome, raw.note);
  }
}
