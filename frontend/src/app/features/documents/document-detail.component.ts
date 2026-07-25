import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { DocumentsFacade } from './documents.facade';
import { DocumentsApiService } from '../../core/api/documents-api.service';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { VERSION_BUMPS, VersionBump , SignatureRecord } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Controlled-document workspace: version history + the state-appropriate action
 * (submit, recommend/reject, PIN-signed publish, draft new version, retire).
 * Recommend/publish/retire are hidden for non-approvers (server still enforces
 * both the role and the author≠reviewer≠approver segregation-of-duties rules).
 */
@Component({
  selector: 'qams-document-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, AuditTrailComponent],
  template: `
    @if (doc(); as d) {
      <qams-page-header [title]="d.code + ' — ' + d.title" [subtitle]="d.category">
        <a routerLink="/documents" class="ghost-link">← {{ i18n.t('doc.backToList') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="d.status" />

      <div class="grid">
        <section class="card">
          <div class="meta">
            <div><span class="muted">{{ i18n.t('nc.status') }}</span><qams-status-pill [status]="d.status" /></div>
            <div><span class="muted">{{ i18n.t('doc.created') }}</span> {{ d.createdAtUtc | date:'mediumDate' }}</div>
          </div>

          <h3>{{ i18n.t('doc.versions') }}</h3>
          <table>
            <thead><tr>
              <th>{{ i18n.t('doc.version') }}</th><th>{{ i18n.t('doc.state') }}</th>
              <th>{{ i18n.t('doc.changeSummary') }}</th><th>{{ i18n.t('doc.file') }}</th>
            </tr></thead>
            <tbody>
              @for (v of d.versions; track v.id) {
                <tr>
                  <td>{{ v.version }}</td>
                  <td><qams-status-pill [status]="v.state" /></td>
                  <td>{{ v.changeSummary }}@if (v.rejectionReason) { <span class="error"> — {{ v.rejectionReason }}</span> }</td>
                  <td><a [href]="facade.downloadUrl(v.fileId)" target="_blank" rel="noopener">{{ i18n.t('doc.download') }}</a></td>
                </tr>
              }
            </tbody>
          </table>
        </section>

        <section class="card actions">
          <h3>{{ i18n.t('nc.workflow') }}</h3>
          @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

          @if (inFlightState() === 'Draft' && d.status !== 'Obsolete') {
            <button (click)="facade.submit(d.id)">{{ i18n.t('doc.submit') }}</button>
          }
          @if (inFlightState() === 'UnderReview') {
            @if (perms.canApprove()) {
              <button (click)="facade.recommend(d.id)">{{ i18n.t('doc.recommend') }}</button>
              <form [formGroup]="rejectForm" (ngSubmit)="facade.reject(d.id, rejectForm.getRawValue())">
                <label>{{ i18n.t('nc.rejectReason') }}</label>
                <input formControlName="reason" />
                <button type="submit" class="secondary" [disabled]="rejectForm.invalid">{{ i18n.t('nc.reject') }}</button>
              </form>
            } @else { <p class="muted">{{ i18n.t('doc.awaitReview') }}</p> }
          }
          @if (inFlightState() === 'Approved') {
            @if (perms.canApprove()) {
              <form [formGroup]="publishForm" (ngSubmit)="facade.publish(d.id, publishForm.getRawValue())">
                <label>{{ i18n.t('doc.signPassword') }}</label>
                <input formControlName="password" type="password" autocomplete="current-password" />
                <label>{{ i18n.t('doc.pin') }}</label>
                <input formControlName="pin" inputmode="numeric" maxlength="4" [placeholder]="i18n.t('doc.pinHint')" />
                <div class="hint">{{ i18n.t('doc.twoComponentHint') }}</div>
                <button type="submit" [disabled]="publishForm.invalid">{{ i18n.t('doc.publish') }}</button>
              </form>
            } @else { <p class="muted">{{ i18n.t('doc.awaitPublish') }}</p> }
          }
          @if (d.status === 'Published') {
            <form [formGroup]="versionForm" (ngSubmit)="draftVersion(d.id)">
              <label>{{ i18n.t('doc.newVersion') }}</label>
              <select formControlName="bump">@for (b of bumps; track b) { <option [value]="b">{{ b }}</option> }</select>
              <input formControlName="changeSummary" [placeholder]="i18n.t('doc.changeSummary')" />
              <input type="file" (change)="onFile($event)" />
              <button type="submit" [disabled]="versionForm.invalid || !file()">{{ i18n.t('doc.addVersion') }}</button>
            </form>
            @if (perms.canApprove()) {
              <button class="secondary" (click)="facade.retire(d.id)">{{ i18n.t('doc.retire') }}</button>
            }
          }
          @if (d.status === 'Obsolete') { <p class="muted">{{ i18n.t('doc.obsolete') }}</p> }
        </section>
      </div>
    
      @if (signatures().length > 0) {
        <section class="card">
          <h3>{{ i18n.t('doc.signatures') }}</h3>
          @for (s of signatures(); track s.id) {
            <div class="sig-row">
              <b>{{ s.signerDisplay }}</b>
              <span>{{ s.meaning }}</span>
              <span class="muted">{{ s.signedAtUtc | date:'medium' }}</span>
              <span class="mono" [title]="s.contentHash">{{ s.contentHash.slice(0, 12) }}…</span>
            </div>
          }
        </section>
      }

      <qams-audit-trail [subject]="d.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
  styles: [`
    .grid { display: grid; grid-template-columns: 2fr 1fr; gap: 1rem; align-items: start; }
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .actions form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    .actions button { margin-top: .5rem; margin-inline-end: .5rem; width: auto; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    .sig-row { display: flex; gap: 14px; align-items: baseline; padding: 6px 0; border-bottom: 1px solid var(--nt-border); font-size: 12.5px; flex-wrap: wrap; }
    .mono { font-family: var(--nt-mono); font-size: 10.5px; color: var(--nt-grey-m); }
    h3 { margin-top: 1rem; }
    @media (max-width: 800px) { .grid { grid-template-columns: 1fr; } }
  `],
})
export class DocumentDetailComponent implements OnInit {
  readonly facade = inject(DocumentsFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly docsApi = inject(DocumentsApiService);

  /** Route-bound document id. */
  readonly id = input.required<string>();

  /** Canonical workflow path for the stepper (off-path states render as terminal). */
  readonly flowSteps = ['Draft', 'Published', 'Obsolete'] as const;

  readonly doc = this.facade.selected;
  readonly bumps = VERSION_BUMPS;
  readonly file = signal<File | null>(null);
  /** Part 11 §11.50 signature manifest for this document. */
  readonly signatures = signal<SignatureRecord[]>([]);

  /** The state of the single in-flight (non-published, non-obsolete) version, if any. */
  readonly inFlightState = computed<string | null>(() => {
    const inFlight = this.doc()?.versions.find(
      (v) => v.state === 'Draft' || v.state === 'UnderReview' || v.state === 'Approved');
    return inFlight?.state ?? null;
  });

  readonly rejectForm = this.fb.nonNullable.group({ reason: ['', [Validators.required, Validators.maxLength(1000)]] });
  readonly publishForm = this.fb.nonNullable.group({
    password: ['', [Validators.required]],
    pin: ['', [Validators.required, Validators.pattern(/^\d{4}$/)]],
  });
  readonly versionForm = this.fb.nonNullable.group({
    bump: ['Minor' as VersionBump, [Validators.required]],
    changeSummary: ['', [Validators.required, Validators.maxLength(1000)]],
  });

  ngOnInit(): void {
    void this.facade.loadDetail(this.id());
    void this.loadSignatures();
  }

  private async loadSignatures(): Promise<void> {
    try {
      this.signatures.set(await firstValueFrom(this.docsApi.signatures(this.id())));
    } catch {
      this.signatures.set([]); // Manifest is additive — never block the workspace.
    }
  }

  onFile(event: Event): void {
    this.file.set((event.target as HTMLInputElement).files?.[0] ?? null);
  }

  async draftVersion(id: string): Promise<void> {
    const selected = this.file();
    if (this.versionForm.invalid || !selected) { return; }
    const { bump, changeSummary } = this.versionForm.getRawValue();
    await this.facade.draftNewVersion(id, selected, changeSummary, bump);
    this.file.set(null);
    this.versionForm.reset({ bump: 'Minor', changeSummary: '' });
  }
}
