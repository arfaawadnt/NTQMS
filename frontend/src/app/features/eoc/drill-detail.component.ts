import { ChangeDetectionStrategy, Component, OnInit, inject, input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { EocFacade } from './eoc.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';

/**
 * Drill workspace (HQMS M15): execute the scheduled drill (recording participants), then evaluate
 * it with a 0–100 effectiveness score and improvement notes. The score drives the effectiveness
 * tier shown on the EOC dashboard.
 */
@Component({
    selector: 'qams-drill-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent],
    template: `
    @if (drill(); as d) {
      <qams-page-header [title]="d.drillRef + ' — ' + i18n.t('eoc.dt.' + d.type)">
        <a routerLink="/eoc" class="ghost-link">← {{ i18n.t('eoc.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="steps" [current]="d.status" />

      <div class="grid">
        <section class="card">
          <div class="meta">
            <div><span class="muted">{{ i18n.t('eoc.status') }}</span><qams-status-pill [status]="d.status" /></div>
            <div><span class="muted">{{ i18n.t('eoc.location') }}</span> {{ d.location }}</div>
            <div><span class="muted">{{ i18n.t('eoc.scheduledDate') }}</span> {{ d.scheduledDate | date:'mediumDate' }}</div>
            @if (d.executedAtUtc) { <div><span class="muted">{{ i18n.t('eoc.executedAt') }}</span> {{ d.executedAtUtc | date:'medium' }}</div> }
            @if (d.participantCount !== null) { <div><span class="muted">{{ i18n.t('eoc.participants') }}</span> {{ d.participantCount }}</div> }
            @if (d.evaluationScore !== null) { <div><span class="muted">{{ i18n.t('eoc.score') }}</span> <b>{{ d.evaluationScore }}</b> <span class="eff" [class]="'eff ' + (d.effectiveness ?? '').toLowerCase()">{{ i18n.t('eoc.eff.' + d.effectiveness) }}</span></div> }
          </div>
          @if (d.improvementNotes) {
            <h3>{{ i18n.t('eoc.improvementNotes') }}</h3>
            <p>{{ d.improvementNotes }}</p>
          }
        </section>

        <section class="card actions">
          <h3>{{ i18n.t('eoc.workflow') }}</h3>
          @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

          @switch (d.status) {
            @case ('Scheduled') {
              @if (perms.can('environment-of-care.edit')) {
                <form [formGroup]="executeForm" (ngSubmit)="execute(d.id)">
                  <label>{{ i18n.t('eoc.executedAt') }}</label>
                  <input type="datetime-local" formControlName="executedAt" />
                  <label>{{ i18n.t('eoc.participants') }}</label>
                  <input type="number" min="0" formControlName="participantCount" />
                  <button type="submit" [disabled]="executeForm.invalid || facade.loading()">{{ i18n.t('eoc.execute') }}</button>
                </form>
              } @else { <p class="muted">{{ i18n.t('eoc.awaitExecute') }}</p> }
            }
            @case ('Executed') {
              @if (perms.can('environment-of-care.approve')) {
                <form [formGroup]="evaluateForm" (ngSubmit)="evaluate(d.id)">
                  <label>{{ i18n.t('eoc.score') }} (0–100)</label>
                  <input type="number" min="0" max="100" formControlName="score" />
                  <label>{{ i18n.t('eoc.improvementNotes') }}</label>
                  <textarea rows="3" formControlName="improvementNotes"></textarea>
                  <button type="submit" [disabled]="evaluateForm.invalid || facade.loading()">{{ i18n.t('eoc.evaluate') }}</button>
                </form>
              } @else { <p class="muted">{{ i18n.t('eoc.awaitEvaluate') }}</p> }
            }
            @default { <p class="muted">{{ i18n.t('eoc.drillDone') }}</p> }
          }
        </section>
      </div>
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .grid { display: grid; grid-template-columns: 2fr 1fr; gap: 1rem; align-items: start; }
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .actions form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    .actions button { margin-top: .5rem; width: auto; }
    .eff { padding: 1px 7px; border-radius: 999px; font-size: 11px; font-weight: 700; }
    .eff.effective { background: color-mix(in srgb, var(--nt-ink-ok) 18%, transparent); color: var(--nt-ink-ok); }
    .eff.partiallyeffective { background: color-mix(in srgb, var(--nt-ink-warn) 22%, transparent); color: #3a2d00; }
    .eff.ineffective { background: var(--nt-ink-crit); color: #fff; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    h3 { margin-top: 1rem; }
    @media (max-width: 800px) { .grid { grid-template-columns: 1fr; } }
  `]
})
export class DrillDetailComponent implements OnInit {
  readonly facade = inject(EocFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound drill id (provided via withComponentInputBinding). */
  readonly id = input.required<string>();
  readonly drill = this.facade.drill;
  readonly steps = ['Scheduled', 'Executed', 'Evaluated'] as const;

  readonly executeForm = this.fb.nonNullable.group({
    executedAt: ['', [Validators.required]],
    participantCount: [0, [Validators.required, Validators.min(0)]],
  });
  readonly evaluateForm = this.fb.nonNullable.group({
    score: [80, [Validators.required, Validators.min(0), Validators.max(100)]],
    improvementNotes: ['', [Validators.maxLength(4000)]],
  });

  ngOnInit(): void {
    void this.facade.loadDrill(this.id());
  }

  async execute(id: string): Promise<void> {
    if (this.executeForm.invalid) { return; }
    const raw = this.executeForm.getRawValue();
    await this.facade.executeDrill(id, { executedAtUtc: new Date(raw.executedAt).toISOString(), participantCount: Number(raw.participantCount) });
  }

  async evaluate(id: string): Promise<void> {
    if (this.evaluateForm.invalid) { return; }
    const raw = this.evaluateForm.getRawValue();
    await this.facade.evaluateDrill(id, { score: Number(raw.score), improvementNotes: raw.improvementNotes });
  }
}
