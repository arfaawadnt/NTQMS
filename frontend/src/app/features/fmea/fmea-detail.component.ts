import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FmeaFacade } from './fmea.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * FMEA / HFMEA worksheet (HQMS M04): the failure modes ranked by RPN, an add-mode form,
 * and per-mode recommended action + post-action re-score. Editing is privilege-gated.
 */
@Component({
    selector: 'qams-fmea-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, RouterLink, PageHeaderComponent, StatusPillComponent, AuditTrailComponent],
    template: `
    @if (fmea(); as f) {
      <qams-page-header [title]="f.fmeaRef + ' — ' + f.title">
        <a routerLink="/fmea" class="ghost-link">← {{ i18n.t('fme.backToList') }}</a>
      </qams-page-header>

      <div class="meta">
        <div><span class="muted">{{ i18n.t('fme.status') }}</span><qams-status-pill [status]="f.status" /></div>
        <div><span class="muted">{{ i18n.t('fme.type') }}</span> {{ i18n.t('fme.ty.' + f.type) }}</div>
        <div><span class="muted">{{ i18n.t('fme.process') }}</span> {{ f.processName }}</div>
        @if (f.status === 'Draft' && perms.can('risks.approve')) {
          <button (click)="facade.activate(f.id)">{{ i18n.t('fme.activate') }}</button>
        }
        @if (f.status === 'Active' && perms.can('risks.void')) {
          <button class="secondary" (click)="facade.close(f.id)">{{ i18n.t('fme.close') }}</button>
        }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      @if (f.status !== 'Closed' && perms.can('risks.create')) {
        <section class="card">
          <h3>{{ i18n.t('fme.addMode') }}</h3>
          <form class="drawer-form" [formGroup]="modeForm" (ngSubmit)="addMode(f.id)">
            <div class="grid">
              <div class="col-2"><label>{{ i18n.t('fme.processStep') }}</label><input formControlName="processStep" /></div>
              <div class="col-2"><label>{{ i18n.t('fme.failureMode') }}</label><input formControlName="failureMode" /></div>
              <div class="col-2"><label>{{ i18n.t('fme.effect') }}</label><input formControlName="effect" /></div>
              <div class="col-2"><label>{{ i18n.t('fme.cause') }}</label><input formControlName="cause" /></div>
              <div><label>{{ i18n.t('fme.severity') }} (1-10)</label><input type="number" min="1" max="10" formControlName="severity" /></div>
              <div><label>{{ i18n.t('fme.occurrence') }} (1-10)</label><input type="number" min="1" max="10" formControlName="occurrence" /></div>
              <div><label>{{ i18n.t('fme.detection') }} (1-10)</label><input type="number" min="1" max="10" formControlName="detection" /></div>
            </div>
            <button type="submit" [disabled]="modeForm.invalid">{{ i18n.t('fme.addMode') }}</button>
          </form>
        </section>
      }

      <section class="card">
        <h3>{{ i18n.t('fme.worksheet') }} ({{ f.failureModes.length }})</h3>
        @if (f.failureModes.length === 0) { <p class="muted">{{ i18n.t('fme.noModes') }}</p> }
        @else {
          <table>
            <thead><tr>
              <th>{{ i18n.t('fme.processStep') }}</th><th>{{ i18n.t('fme.failureMode') }}</th>
              <th title="Severity">S</th><th title="Occurrence">O</th><th title="Detection">D</th><th>{{ i18n.t('fme.rpn') }}</th>
              <th>{{ i18n.t('fme.residual') }}</th><th></th>
            </tr></thead>
            <tbody>
              @for (m of f.failureModes; track m.id) {
                <tr>
                  <td>{{ m.processStep }}</td>
                  <td>{{ m.failureMode }}<br><span class="muted small">{{ m.effect }}</span></td>
                  <td>{{ m.severity }}</td><td>{{ m.occurrence }}</td><td>{{ m.detection }}</td>
                  <td><b [class.danger-text]="m.rpn >= f.highRpnThreshold">{{ m.rpn }}</b></td>
                  <td>@if (m.residualRpn !== null) { <b [class.ok-text]="m.residualRpn < m.rpn">{{ m.residualRpn }}</b> } @else { <span class="muted">—</span> }</td>
                  <td class="actions">
                    @if (f.status === 'Active' && perms.can('risks.edit')) {
                      <button class="link" (click)="toggle(m.id)">{{ i18n.t('fme.action') }}</button>
                    }
                  </td>
                </tr>
                @if (expandedId() === m.id) {
                  <tr class="sub"><td colspan="8">
                    @if (m.recommendedAction) { <p><b>{{ i18n.t('fme.recommendedAction') }}:</b> {{ m.recommendedAction }}</p> }
                    <form class="inline" [formGroup]="actionForm" (ngSubmit)="recommend(f.id, m.id)">
                      <input formControlName="action" [placeholder]="i18n.t('fme.recommendedAction')" />
                      <button type="submit" [disabled]="actionForm.invalid">{{ i18n.t('fme.saveAction') }}</button>
                    </form>
                    <form class="inline" [formGroup]="residualForm" (ngSubmit)="residual(f.id, m.id)">
                      <span class="muted">{{ i18n.t('fme.residualScore') }}:</span>
                      <input type="number" min="1" max="10" formControlName="severity" title="Severity" />
                      <input type="number" min="1" max="10" formControlName="occurrence" title="Occurrence" />
                      <input type="number" min="1" max="10" formControlName="detection" title="Detection" />
                      <button type="submit" [disabled]="residualForm.invalid">{{ i18n.t('fme.saveResidual') }}</button>
                    </form>
                  </td></tr>
                }
              }
            </tbody>
          </table>
        }
      </section>

      <qams-audit-trail [subject]="f.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1rem; align-items: center; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .meta button { width: auto; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(120px, 1fr)); gap: .5rem 1rem; }
    .col-2 { grid-column: span 2; }
    .inline { display: flex; gap: .5rem; flex-wrap: wrap; align-items: center; margin-top: .4rem; }
    .inline input[type=number] { width: 64px; }
    .sub td { background: var(--nt-surface-alt, #f4f7fa); }
    .danger-text { color: var(--nt-ink-crit); } .ok-text { color: var(--nt-ink-ok); }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    select, button, input { width: auto; }
  `]
})
export class FmeaDetailComponent implements OnInit {
  readonly facade = inject(FmeaFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  readonly id = input.required<string>();
  readonly fmea = this.facade.selected;
  readonly expandedId = signal<string | null>(null);

  readonly modeForm = this.fb.nonNullable.group({
    processStep: ['', [Validators.required, Validators.maxLength(200)]],
    failureMode: ['', [Validators.required, Validators.maxLength(500)]],
    effect: ['', [Validators.maxLength(1000)]],
    cause: ['', [Validators.maxLength(1000)]],
    severity: [5, [Validators.required, Validators.min(1), Validators.max(10)]],
    occurrence: [5, [Validators.required, Validators.min(1), Validators.max(10)]],
    detection: [5, [Validators.required, Validators.min(1), Validators.max(10)]],
  });

  readonly actionForm = this.fb.nonNullable.group({
    action: ['', [Validators.required, Validators.maxLength(2000)]],
  });

  readonly residualForm = this.fb.nonNullable.group({
    severity: [5, [Validators.required, Validators.min(1), Validators.max(10)]],
    occurrence: [5, [Validators.required, Validators.min(1), Validators.max(10)]],
    detection: [5, [Validators.required, Validators.min(1), Validators.max(10)]],
  });

  ngOnInit(): void {
    void this.facade.loadDetail(this.id());
  }

  toggle(modeId: string): void {
    this.expandedId.set(this.expandedId() === modeId ? null : modeId);
  }

  async addMode(id: string): Promise<void> {
    if (this.modeForm.invalid) { return; }
    await this.facade.addFailureMode(id, this.modeForm.getRawValue());
    if (this.facade.error() === '') { this.modeForm.reset({ severity: 5, occurrence: 5, detection: 5 }); }
  }

  async recommend(id: string, modeId: string): Promise<void> {
    if (this.actionForm.invalid) { return; }
    await this.facade.recommend(id, modeId, { action: this.actionForm.getRawValue().action, ownerId: null });
    if (this.facade.error() === '') { this.actionForm.reset(); }
  }

  async residual(id: string, modeId: string): Promise<void> {
    if (this.residualForm.invalid) { return; }
    await this.facade.residual(id, modeId, this.residualForm.getRawValue());
    if (this.facade.error() === '') { this.expandedId.set(null); }
  }
}
