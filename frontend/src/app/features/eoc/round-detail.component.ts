import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { EocFacade } from './eoc.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { FINDING_SEVERITIES, FindingSeverity } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';

/**
 * Safety-round workspace (HQMS M15): start the round, log findings by severity, resolve them with a
 * corrective note, then complete the round. Open High/Critical findings are the environmental risk
 * backlog carried on the EOC dashboard.
 */
@Component({
    selector: 'qams-round-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent],
    template: `
    @if (round(); as r) {
      <qams-page-header [title]="r.roundRef + ' — ' + r.area">
        <a routerLink="/eoc" class="ghost-link">← {{ i18n.t('eoc.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="steps" [current]="r.status" />

      <div class="grid">
        <section class="card">
          <div class="meta">
            <div><span class="muted">{{ i18n.t('eoc.status') }}</span><qams-status-pill [status]="r.status" /></div>
            <div><span class="muted">{{ i18n.t('eoc.type') }}</span> {{ i18n.t('eoc.rt.' + r.type) }}</div>
            <div><span class="muted">{{ i18n.t('eoc.scheduledDate') }}</span> {{ r.scheduledDate | date:'mediumDate' }}</div>
          </div>

          <h3>{{ i18n.t('eoc.findings') }}</h3>
          @if (r.findings.length === 0) { <p class="muted">{{ i18n.t('eoc.noFindings') }}</p> }
          @for (f of r.findings; track f.id) {
            <div class="row-item">
              <div>
                <span class="sev" [class]="'sev ' + f.severity.toLowerCase()">{{ i18n.t('eoc.sev.' + f.severity) }}</span>
                {{ f.description }}
                @if (f.correctiveNote) { <div class="muted small">✓ {{ f.correctiveNote }}</div> }
              </div>
              <div class="finding-actions">
                @if (f.status === 'Open') {
                  @if (perms.can('environment-of-care.edit')) {
                    @if (resolving() === f.id) {
                      <form [formGroup]="resolveForm" (ngSubmit)="resolve(r.id, f.id)">
                        <input formControlName="note" [placeholder]="i18n.t('eoc.correctiveNote')" />
                        <button type="submit" [disabled]="resolveForm.invalid">{{ i18n.t('eoc.resolve') }}</button>
                      </form>
                    } @else { <button class="link" (click)="startResolve(f.id)">{{ i18n.t('eoc.resolve') }}</button> }
                  } @else { <span class="muted">{{ i18n.t('eoc.open') }}</span> }
                } @else { <span class="resolved">✓ {{ i18n.t('eoc.resolved') }}</span> }

                <!-- M-22: manual, suggested hand-off to the CAPA pipeline. -->
                @if (f.raisedNcRef) {
                  <span class="nc-linked" [title]="i18n.t('eoc.ncRaisedHint')">↳ {{ i18n.t('eoc.ncRaised') }} {{ f.raisedNcRef }}</span>
                } @else if (perms.can('nc.create')) {
                  <button class="link raise-nc" [class.suggested]="isSignificant(f.severity)"
                          [disabled]="facade.loading() || raising() === f.id" (click)="raiseNc(r.id, f.id)">
                    {{ i18n.t('eoc.raiseNc') }}@if (isSignificant(f.severity)) { <span class="hint">· {{ i18n.t('eoc.raiseNcSuggested') }}</span> }
                  </button>
                }
              </div>
            </div>
          }
        </section>

        <section class="card actions">
          <h3>{{ i18n.t('eoc.workflow') }}</h3>
          @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

          @switch (r.status) {
            @case ('Scheduled') {
              @if (perms.can('environment-of-care.edit')) {
                <button (click)="facade.startRound(r.id)" [disabled]="facade.loading()">{{ i18n.t('eoc.startRound') }}</button>
              } @else { <p class="muted">{{ i18n.t('eoc.awaitStart') }}</p> }
            }
            @case ('InProgress') {
              @if (perms.can('environment-of-care.edit')) {
                <form [formGroup]="findingForm" (ngSubmit)="addFinding(r.id)">
                  <label>{{ i18n.t('eoc.addFinding') }}</label>
                  <select formControlName="severity">@for (s of severities; track s) { <option [value]="s">{{ i18n.t('eoc.sev.' + s) }}</option> }</select>
                  <input formControlName="description" [placeholder]="i18n.t('eoc.findingDescription')" />
                  <button type="submit" [disabled]="findingForm.invalid">{{ i18n.t('eoc.logFinding') }}</button>
                </form>
              }
              @if (perms.can('environment-of-care.void')) {
                <button class="secondary" (click)="facade.completeRound(r.id)" [disabled]="facade.loading()">{{ i18n.t('eoc.completeRound') }}</button>
              }
            }
            @default { <p class="muted">{{ i18n.t('eoc.roundClosed') }}</p> }
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
    .row-item { display: flex; justify-content: space-between; gap: 1rem; padding: .55rem 0; border-bottom: 1px solid var(--nt-border); }
    .actions form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    .actions button { margin-top: .5rem; margin-inline-end: .5rem; width: auto; }
    button.link { background: none; border: none; color: var(--nt-blue); cursor: pointer; padding: 0; text-decoration: underline; }
    .finding-actions { display: flex; flex-direction: column; align-items: flex-end; gap: .35rem; }
    button.raise-nc.suggested { font-weight: 700; }
    button.raise-nc .hint { font-weight: 400; font-size: .72rem; opacity: .8; }
    .nc-linked { color: var(--nt-ink-ok); font-size: .78rem; font-weight: 600; }
    .small { font-size: .72rem; } .resolved { color: var(--nt-ink-ok); font-weight: 700; }
    .sev { padding: 1px 7px; border-radius: 999px; font-size: 11px; font-weight: 700; margin-inline-end: 6px; }
    .sev.low { background: color-mix(in srgb, var(--nt-slate) 18%, transparent); color: var(--nt-slate); }
    .sev.medium { background: color-mix(in srgb, var(--nt-ink-warn) 22%, transparent); color: #3a2d00; }
    .sev.high { background: color-mix(in srgb, var(--nt-ink-serious) 20%, transparent); color: var(--nt-ink-serious); }
    .sev.critical { background: var(--nt-ink-crit); color: #fff; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    h3 { margin-top: 1rem; }
    @media (max-width: 800px) { .grid { grid-template-columns: 1fr; } }
  `]
})
export class RoundDetailComponent implements OnInit {
  readonly facade = inject(EocFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound round id (provided via withComponentInputBinding). */
  readonly id = input.required<string>();
  readonly round = this.facade.round;
  readonly severities = FINDING_SEVERITIES;
  readonly steps = ['Scheduled', 'InProgress', 'Completed'] as const;

  readonly resolving = signal<string | null>(null);
  readonly raising = signal<string | null>(null);

  readonly findingForm = this.fb.nonNullable.group({
    severity: ['Medium' as FindingSeverity, [Validators.required]],
    description: ['', [Validators.required, Validators.maxLength(2000)]],
  });
  readonly resolveForm = this.fb.nonNullable.group({ note: ['', [Validators.required, Validators.maxLength(2000)]] });

  ngOnInit(): void {
    void this.facade.loadRound(this.id());
  }

  startResolve(findingId: string): void { this.resolveForm.reset({ note: '' }); this.resolving.set(findingId); }

  async addFinding(id: string): Promise<void> {
    if (this.findingForm.invalid) { return; }
    await this.facade.addFinding(id, this.findingForm.getRawValue());
    if (this.facade.error() === '') { this.findingForm.reset({ severity: 'Medium', description: '' }); }
  }

  async resolve(id: string, findingId: string): Promise<void> {
    if (this.resolveForm.invalid) { return; }
    await this.facade.resolveFinding(id, findingId, this.resolveForm.getRawValue());
    if (this.facade.error() === '') { this.resolving.set(null); }
  }

  /** High/Critical findings are the ones the round screen suggests raising an NC for. */
  isSignificant(severity: string): boolean { return severity === 'High' || severity === 'Critical'; }

  async raiseNc(id: string, findingId: string): Promise<void> {
    if (this.raising() !== null) { return; }
    this.raising.set(findingId);
    try {
      await this.facade.raiseNcFromFinding(id, findingId);
    } finally {
      this.raising.set(null);
    }
  }
}
