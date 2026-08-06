import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { EsignCredentials, EsignDialogComponent } from '../../shared/ui/esign-dialog.component';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { OutlierFacade } from './outlier.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Outlier-screening workspace: point entry, robust statistics (median / IQR /
 * Tukey fences), and a per-point table flagging any value outside the Tukey
 * fence or above the MAD-based modified z-score threshold. Statistics are
 * computed by the backend.
 */
@Component({
    selector: 'qams-outlier-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, DecimalPipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent, EsignDialogComponent],
    template: `
    @if (item(); as s) {
      <qams-page-header [title]="s.screeningRef + ' — ' + s.dataset" [subtitle]="s.unit">
        <a routerLink="/outlier-screenings" class="ghost-link">← {{ i18n.t('out.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="s.state" />

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="s.state" /></div>
        <div><span class="muted">{{ i18n.t('out.points') }}</span> {{ s.points.length }}</div>
        @if (s.outlierCount !== null) { <div><span class="muted">{{ i18n.t('out.outliers') }}</span> <b>{{ s.outlierCount }}</b></div> }
        @if (s.signedOffAtUtc) { <div><span class="muted">{{ i18n.t('val.signedOff') }}</span> {{ s.signedOffAtUtc | date:'medium' }}</div> }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      @if (s.median !== null) {
        <div class="components">
          <div class="comp card"><span class="muted">{{ i18n.t('lin.mean') }}</span><b>{{ s.mean | number:'1.0-4' }}</b><span class="sub">SD {{ s.sd | number:'1.0-4' }}</span></div>
          <div class="comp card"><span class="muted">Median</span><b>{{ s.median | number:'1.0-4' }}</b><span class="sub">Q1 {{ s.q1 | number:'1.0-4' }} · Q3 {{ s.q3 | number:'1.0-4' }}</span></div>
          <div class="comp card highlight"><span class="muted">{{ i18n.t('out.tukeyRange') }}</span><b>{{ s.tukeyLower | number:'1.0-3' }} – {{ s.tukeyUpper | number:'1.0-3' }}</b><span class="sub">{{ s.unit }}</span></div>
        </div>
      }

      <section class="card">
        <h3>{{ i18n.t('out.pointData') }} ({{ s.points.length }})</h3>
        @if (s.state !== 'SignedOff') {
          <form [formGroup]="entryForm" (ngSubmit)="add(s.id)">
            <div class="pair">
              <div><label>{{ i18n.t('out.value') }} ({{ s.unit }})</label><input type="number" step="any" formControlName="value" /></div>
              <div><label>{{ i18n.t('out.label') }}</label><input formControlName="label" [placeholder]="i18n.t('common.optional')" /></div>
            </div>
            <button type="submit" [disabled]="entryForm.invalid">{{ i18n.t('out.addPoint') }}</button>
          </form>
        }
        @if (s.points.length > 0) {
          <table class="mtable">
            <thead><tr>
              <th>{{ i18n.t('out.value') }}</th><th>{{ i18n.t('out.label') }}</th>
              <th>{{ i18n.t('out.zScore') }}</th><th>{{ i18n.t('out.modZ') }}</th><th>{{ i18n.t('out.flag') }}</th><th></th>
            </tr></thead>
            <tbody>
              @for (p of s.points; track p.id) {
                <tr [class.flagged]="p.isOutlier">
                  <td><b>{{ p.value | number:'1.0-4' }}</b></td>
                  <td class="muted">{{ p.label || '—' }}</td>
                  <td>{{ p.zScore | number:'1.0-2' }}</td>
                  <td>{{ p.modifiedZScore | number:'1.0-2' }}</td>
                  <td>
                    @if (s.state === 'DataEntry') { — }
                    @else if (p.isOutlier) { <qams-status-pill status="Failed" />&nbsp;{{ i18n.t('out.outlier') }} }
                    @else { <qams-status-pill status="Pass" /> }
                  </td>
                  <td>
                    @if (s.state !== 'SignedOff') {
                      <button class="link danger-link" type="button" (click)="facade.removePoint(s.id, p.id)">✕</button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
      </section>

      <section class="card">
        <h3>{{ i18n.t('val.workflow') }}</h3>
        <div class="actions">
          @if (s.state !== 'SignedOff') {
            <button (click)="facade.calculate(s.id)" [disabled]="s.points.length < 4">{{ i18n.t('out.calculate') }}</button>
            @if (s.points.length < 4) { <span class="muted">{{ i18n.t('out.minPoints') }}</span> }
          }
          @if (s.state === 'Calculated' && perms.can('analytical-quality.sign')) {
            <button (click)="esignOpen.set(true)">{{ i18n.t('mc.signOff') }}</button>
            <qams-esign-dialog [open]="esignOpen()" [meaning]="i18n.t('esign.aqMeaning')" [busy]="facade.loading()" [error]="facade.error()" (confirm)="doSignOff(s.id, $event)" (cancel)="esignOpen.set(false)" />
          }
          @if (s.state === 'SignedOff') { <p class="muted">{{ i18n.t('mc.signedOffNote') }}</p> }
        </div>
      </section>

      <qams-audit-trail [subject]="s.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; align-items: center; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .components { display: grid; grid-template-columns: repeat(3, 1fr); gap: 1rem; margin-bottom: 1rem; }
    .comp { display: flex; flex-direction: column; gap: 2px; padding: 14px 16px; }
    .comp b { font-size: 1.3rem; color: var(--nt-navy-deep); }
    .comp.highlight { border-inline-start: 4px solid var(--nt-blue); }
    .comp .sub { font-size: .72rem; color: var(--nt-grey-m); }
    section { margin-bottom: 1rem; }
    .pair { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    .mtable { margin-top: 1rem; }
    tr.flagged { background: color-mix(in srgb, var(--nt-red) 8%, transparent); }
    .danger-link { color: var(--nt-red); }
    .actions { display: flex; gap: .75rem; align-items: center; flex-wrap: wrap; }
    .actions button { width: auto; }
    form { border-bottom: 1px solid var(--nt-border); padding-bottom: .75rem; }
    form button { width: auto; margin-top: .5rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    @media (max-width: 800px) { .components, .pair { grid-template-columns: 1fr; } }
  `]
})
export class OutlierDetailComponent implements OnInit {
  readonly facade = inject(OutlierFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  readonly id = input.required<string>();

  /** Whether the Part 11 e-signature dialog is open for the sign-off. */
  readonly esignOpen = signal(false);

  /** Signs off through the ceremony dialog; closes on success, stays open (showing the error) on failure. */
  async doSignOff(id: string, credentials: EsignCredentials): Promise<void> {
    await this.facade.signOff(id, credentials);
    if (this.facade.error() === '') { this.esignOpen.set(false); }
  }

  readonly flowSteps = ['DataEntry', 'Calculated', 'SignedOff'] as const;
  readonly item = this.facade.selected;

  readonly entryForm = this.fb.nonNullable.group({
    value: [null as number | null, [Validators.required]],
    label: ['', [Validators.maxLength(120)]],
  });

  ngOnInit(): void { void this.facade.loadDetail(this.id()); }

  async add(id: string): Promise<void> {
    if (this.entryForm.invalid) { return; }
    const raw = this.entryForm.getRawValue();
    await this.facade.addPoint(id, raw.value!, raw.label.trim() || null);
    this.entryForm.reset({ value: null, label: '' });
  }
}
