import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PermissionsService } from '../../core/permissions.service';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ComplianceApiService } from '../../core/api/compliance-api.service';
import { ExportsApiService } from '../../core/api/exports-api.service';
import { I18nService } from '../../core/i18n.service';
import {
  AuditTrailEntry, AuditTrailReview, ChainVerification, SecurityEvent, SignatureRecord,
} from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { ExportColumn, ExportMenuComponent } from '../../shared/ui/export-menu.component';

type LedgerTab = 'trail' | 'signatures' | 'security' | 'reviews';

/**
 * Compliance Ledger viewer (QM/TenantAdmin/ExternalAuditor): the tamper-evident
 * audit trail (searchable), the 21 CFR Part 11 electronic-signature log, and
 * the security-event log — plus on-demand hash-chain verification that
 * recomputes the whole chain server-side and reports the first break, if any.
 */
@Component({
    selector: 'qams-compliance',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [FormsModule, DatePipe, PageHeaderComponent, ExportMenuComponent],
    template: `
    <qams-page-header [title]="i18n.t('cmp.title')" [subtitle]="i18n.t('cmp.subtitle')">
      @if (tab() === 'trail') {
        <qams-export-menu [title]="i18n.t('trail.title')" [columns]="trailExportColumns" [rows]="trail()" />
      } @else if (tab() === 'signatures') {
        <qams-export-menu [title]="i18n.t('cmp.signatures')" [columns]="signatureExportColumns" [rows]="signatures()" />
      } @else if (tab() === 'security') {
        <qams-export-menu [title]="i18n.t('cmp.security')" [columns]="securityExportColumns" [rows]="security()" />
      } @else if (tab() === 'reviews') {
        <qams-export-menu [title]="i18n.t('atr.tab')" [columns]="reviewExportColumns" [rows]="reviews()" />
      }
      <button (click)="verify()" [disabled]="verifying()">{{ i18n.t('cmp.verifyChain') }}</button>
    </qams-page-header>

    @if (chain(); as c) {
      <div class="card chain" [class.ok]="c.ok" [class.broken]="!c.ok">
        @if (c.ok) {
          ✓ {{ i18n.t('cmp.chainOk') }} — {{ c.verifiedEntries }} {{ i18n.t('cmp.entries') }}
        } @else {
          ✕ {{ i18n.t('cmp.chainBroken') }} #{{ c.brokenAtSequence }} — {{ i18n.t('cmp.chainBrokenNote') }}
        }
      </div>
    }
    @if (error()) { <div class="error">{{ error() }}</div> }

    <div class="tabs">
      <button class="tab" [class.active]="tab() === 'trail'" (click)="switchTab('trail')">{{ i18n.t('trail.title') }}</button>
      <button class="tab" [class.active]="tab() === 'signatures'" (click)="switchTab('signatures')">{{ i18n.t('cmp.signatures') }}</button>
      <button class="tab" [class.active]="tab() === 'security'" (click)="switchTab('security')">{{ i18n.t('cmp.security') }}</button>
      <button class="tab" [class.active]="tab() === 'reviews'" (click)="switchTab('reviews')">{{ i18n.t('atr.tab') }}</button>
    </div>

    @if (loading()) { <p class="muted">{{ i18n.t('common.loading') }}</p> }

    @if (tab() === 'trail' && !loading()) {
      <div class="card">
        <div class="searchrow">
          <input [(ngModel)]="subject" (keyup.enter)="loadTrail()" [placeholder]="i18n.t('cmp.searchHint')" />
          <button class="secondary" (click)="loadTrail()">{{ i18n.t('cmp.search') }}</button>
        </div>
        @if (trail().length === 0) { <p class="muted">{{ i18n.t('trail.empty') }}</p> }
        @else {
          <table>
            <thead><tr>
              <th>#</th><th>{{ i18n.t('cmp.when') }}</th><th>{{ i18n.t('cmp.event') }}</th><th>{{ i18n.t('trail.hash') }}</th>
            </tr></thead>
            <tbody>
              @for (e of trail(); track e.id) {
                <tr class="clickable" (click)="expanded.set(expanded() === e.id ? '' : e.id)">
                  <td>{{ e.sequence }}</td>
                  <td>{{ e.occurredAtUtc | date:'medium' }}</td>
                  <td>{{ prettyEvent(e.eventType) }}</td>
                  <td class="mono">{{ e.entryHash.slice(0, 12) }}…</td>
                </tr>
                @if (expanded() === e.id) {
                  <tr><td colspan="4"><pre class="payload">{{ prettyJson(e.payload) }}</pre></td></tr>
                }
              }
            </tbody>
          </table>
        }
      </div>
    }

    @if (tab() === 'signatures' && !loading()) {
      <div class="card">
        <div class="exportrow">
          <button class="secondary" (click)="exports.signaturesXlsx()">{{ i18n.t('exp.xlsx') }}</button>
        </div>
        @if (signatures().length === 0) { <p class="muted">{{ i18n.t('cmp.noSignatures') }}</p> }
        @else {
          <table>
            <thead><tr>
              <th>{{ i18n.t('cmp.when') }}</th><th>{{ i18n.t('cmp.signer') }}</th><th>{{ i18n.t('cmp.meaning') }}</th>
              <th>{{ i18n.t('cmp.subjectRef') }}</th><th>{{ i18n.t('cmp.contentHash') }}</th>
            </tr></thead>
            <tbody>
              @for (s of signatures(); track s.id) {
                <tr>
                  <td>{{ s.signedAtUtc | date:'medium' }}</td>
                  <td>{{ s.signerDisplay }}</td>
                  <td>{{ s.meaning }}</td>
                  <td class="code">{{ s.subjectRef }}</td>
                  <td class="mono">{{ s.contentHash.slice(0, 12) }}…</td>
                </tr>
              }
            </tbody>
          </table>
        }
      </div>
    }

    @if (tab() === 'security' && !loading()) {
      <div class="card">
        @if (security().length === 0) { <p class="muted">{{ i18n.t('cmp.noSecurity') }}</p> }
        @else {
          <table>
            <thead><tr>
              <th>{{ i18n.t('cmp.when') }}</th><th>{{ i18n.t('cmp.event') }}</th>
              <th>{{ i18n.t('cmp.actor') }}</th><th>IP</th><th>{{ i18n.t('cmp.detail') }}</th>
            </tr></thead>
            <tbody>
              @for (e of security(); track e.id) {
                <tr>
                  <td>{{ e.occurredAtUtc | date:'medium' }}</td>
                  <td class="code">{{ e.eventType }}</td>
                  <td>{{ e.actor ?? '—' }}</td>
                  <td class="mono">{{ e.ipAddress ?? '—' }}</td>
                  <td>{{ e.detail ?? '—' }}</td>
                </tr>
              }
            </tbody>
          </table>
        }
      </div>
    }
    @if (tab() === 'reviews' && !loading()) {
      <!-- Periodic audit-trail review (Part 11 §11.10(e)): open a period, examine the ledgers, record the conclusion. -->
      @if (perms.can('compliance.create')) {
        <div class="card openrow">
          <b>{{ i18n.t('atr.open') }}</b>
          <input type="date" [(ngModel)]="periodStart" [attr.aria-label]="i18n.t('atr.periodStart')" />
          <span class="muted">→</span>
          <input type="date" [(ngModel)]="periodEnd" [attr.aria-label]="i18n.t('atr.periodEnd')" />
          <button (click)="openReview()" [disabled]="!periodStart || !periodEnd">{{ i18n.t('atr.openBtn') }}</button>
        </div>
      }
      <div class="card">
        @if (reviews().length === 0) { <p class="muted">{{ i18n.t('atr.empty') }}</p> }
        @else {
          <table>
            <thead><tr>
              <th>{{ i18n.t('mu.ref') }}</th><th>{{ i18n.t('atr.period') }}</th><th>{{ i18n.t('atr.coverage') }}</th>
              <th>{{ i18n.t('atr.anomalies') }}</th><th>{{ i18n.t('atr.conclusion') }}</th><th>{{ i18n.t('nc.status') }}</th>
            </tr></thead>
            <tbody>
              @for (r of reviews(); track r.id) {
                <tr>
                  <td class="code">{{ r.reviewRef }}</td>
                  <td>{{ r.periodStart | date:'mediumDate' }} – {{ r.periodEnd | date:'mediumDate' }}</td>
                  <td>
                    @if (r.eventsReviewed !== null) {
                      {{ r.eventsReviewed }} {{ i18n.t('cmp.entries') }} · {{ r.fieldChangesReviewed }} {{ i18n.t('atr.fieldChanges') }}
                    } @else { — }
                  </td>
                  <td>
                    @if (r.anomaliesFound === true) { <span class="bad">✕ {{ i18n.t('atr.found') }}</span> }
                    @else if (r.anomaliesFound === false) { <span class="good">✓ {{ i18n.t('atr.none') }}</span> }
                    @else { — }
                  </td>
                  <td class="muted">{{ r.conclusion ?? '—' }}</td>
                  <td>{{ r.status }}</td>
                </tr>
                @if (r.status === 'Open' && perms.can('compliance.approve')) {
                  <tr><td colspan="6">
                    <div class="completerow">
                      <label class="chk"><input type="checkbox" [(ngModel)]="anomalies" /> {{ i18n.t('atr.anomaliesFound') }}</label>
                      <input class="grow" [(ngModel)]="conclusion" [placeholder]="i18n.t('atr.conclusionHint')" />
                      <button (click)="completeReview(r.id)" [disabled]="!conclusion.trim()">{{ i18n.t('atr.complete') }}</button>
                    </div>
                  </td></tr>
                }
              }
            </tbody>
          </table>
        }
        <div class="hint">{{ i18n.t('atr.anomalyNote') }}</div>
      </div>
    }
  `,
    styles: [`
    .chain { margin-bottom: 1rem; font-weight: 600; }
    .openrow { display: flex; gap: 10px; align-items: center; margin-bottom: 12px; flex-wrap: wrap; }
    .openrow input { max-width: 170px; }
    .completerow { display: flex; gap: 10px; align-items: center; padding: 6px 0; flex-wrap: wrap; }
    .completerow .grow { flex: 1; min-width: 240px; }
    .chk { display: flex; gap: 6px; align-items: center; white-space: nowrap; }
    .chk input { width: auto; }
    .good { color: var(--nt-green); font-weight: 600; }
    .bad { color: var(--nt-red); font-weight: 600; }
    .chain.ok { color: var(--nt-green); border-color: rgba(24, 128, 56, .35); background: rgba(24, 128, 56, .07); }
    .chain.broken { color: var(--nt-red); border-color: rgba(220, 53, 69, .4); background: rgba(220, 53, 69, .07); }
    .tabs { display: flex; gap: 0; margin-bottom: 12px; background: var(--nt-filter-grey); border-radius: 8px; padding: 3px; width: fit-content; }
    .tab { background: transparent; color: var(--nt-slate); font-size: 12.5px; padding: 7px 16px; border-radius: 6px; }
    .tab:hover { background: rgba(255,255,255,.6); }
    .tab.active { background: #fff; color: var(--nt-blue); box-shadow: var(--nt-shadow-xs); font-weight: 700; }
    .exportrow { display: flex; justify-content: flex-end; margin-bottom: 10px; }
    .searchrow { display: flex; gap: 8px; margin-bottom: 12px; }
    .searchrow input { max-width: 380px; }
    .clickable { cursor: pointer; }
    .mono { font-family: var(--nt-mono); font-size: 11px; }
    .payload {
      background: var(--nt-bg-grey); border: 1px solid var(--nt-border); border-radius: 4px;
      padding: 8px 10px; font-size: 11px; overflow-x: auto; margin: 0;
      font-family: var(--nt-mono); white-space: pre-wrap; word-break: break-word;
    }
    button { width: auto; }
  `]
})
export class ComplianceComponent implements OnInit {
  readonly i18n = inject(I18nService);
  private readonly api = inject(ComplianceApiService);
  readonly exports = inject(ExportsApiService);

  readonly perms = inject(PermissionsService);
  readonly tab = signal<LedgerTab>('trail');

  readonly trailExportColumns: ExportColumn<AuditTrailEntry>[] = [
    { header: 'Seq', cell: (r) => String(r.sequence) },
    { header: 'Event ID', cell: (r) => r.eventId },
    { header: 'Event Type', cell: (r) => r.eventType },
    { header: 'Occurred At', cell: (r) => r.occurredAtUtc },
    { header: 'Payload', cell: (r) => r.payload },
  ];

  readonly signatureExportColumns: ExportColumn<SignatureRecord>[] = [
    { header: 'Signer', cell: (r) => r.signerDisplay },
    { header: 'Meaning', cell: (r) => r.meaning },
    { header: 'Subject Ref', cell: (r) => r.subjectRef },
    { header: 'Content Hash', cell: (r) => r.contentHash },
    { header: 'Signed At', cell: (r) => r.signedAtUtc },
  ];

  readonly securityExportColumns: ExportColumn<SecurityEvent>[] = [
    { header: 'Occurred At', cell: (r) => r.occurredAtUtc },
    { header: 'Event Type', cell: (r) => r.eventType },
    { header: 'Actor', cell: (r) => r.actor ?? '' },
    { header: 'IP Address', cell: (r) => r.ipAddress ?? '' },
    { header: 'Detail', cell: (r) => r.detail ?? '' },
  ];

  readonly reviewExportColumns: ExportColumn<AuditTrailReview>[] = [
    { header: 'Ref', cell: (r) => r.reviewRef },
    { header: 'Period Start', cell: (r) => r.periodStart },
    { header: 'Period End', cell: (r) => r.periodEnd },
    { header: 'Events Reviewed', cell: (r) => `${r.eventsReviewed ?? 0}` },
    { header: 'Anomalies Found', cell: (r) => r.anomaliesFound ? 'Yes' : 'No' },
    { header: 'Status', cell: (r) => r.status },
  ];

  readonly reviews = signal<AuditTrailReview[]>([]);
  /** Open-review form state (template-driven like the trail search). */
  periodStart = '';
  periodEnd = '';
  anomalies = false;
  conclusion = '';
  readonly trail = signal<AuditTrailEntry[]>([]);
  readonly signatures = signal<SignatureRecord[]>([]);
  readonly security = signal<SecurityEvent[]>([]);
  readonly chain = signal<ChainVerification | null>(null);
  readonly loading = signal(false);
  readonly verifying = signal(false);
  readonly error = signal('');
  readonly expanded = signal('');
  /** Free-text audit-trail filter (record ref, id, or event-type fragment). */
  subject = '';

  ngOnInit(): void { void this.loadTrail(); }

  switchTab(tab: LedgerTab): void {
    this.tab.set(tab);
    if (tab === 'trail' && this.trail().length === 0) { void this.loadTrail(); }
    if (tab === 'signatures' && this.signatures().length === 0) { void this.loadSignatures(); }
    if (tab === 'security' && this.security().length === 0) { void this.loadSecurity(); }
    if (tab === 'reviews' && this.reviews().length === 0) { void this.loadReviews(); }
  }

  async loadReviews(): Promise<void> {
    await this.run(async () => this.reviews.set(await firstValueFrom(this.api.auditTrailReviews())));
  }

  async openReview(): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.openAuditTrailReview(this.periodStart, this.periodEnd));
      this.periodStart = '';
      this.periodEnd = '';
      this.reviews.set(await firstValueFrom(this.api.auditTrailReviews()));
    });
  }

  async completeReview(id: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.completeAuditTrailReview(id, this.anomalies, this.conclusion.trim()));
      this.anomalies = false;
      this.conclusion = '';
      this.reviews.set(await firstValueFrom(this.api.auditTrailReviews()));
    });
  }

  async loadTrail(): Promise<void> {
    await this.run(async () => this.trail.set(
      await firstValueFrom(this.api.auditTrail(this.subject.trim() || undefined))));
  }

  async loadSignatures(): Promise<void> {
    await this.run(async () => this.signatures.set(await firstValueFrom(this.api.signatures())));
  }

  async loadSecurity(): Promise<void> {
    await this.run(async () => this.security.set(await firstValueFrom(this.api.securityEvents())));
  }

  async verify(): Promise<void> {
    this.verifying.set(true);
    this.error.set('');
    try {
      this.chain.set(await firstValueFrom(this.api.verifyChain()));
    } catch (err) {
      this.error.set(this.describe(err));
    } finally {
      this.verifying.set(false);
    }
  }

  prettyEvent(eventType: string): string {
    const typeName = eventType.split(',')[0] ?? eventType;
    const name = typeName.split('.').pop() ?? typeName;
    return name.replace(/([a-z])([A-Z])/g, '$1 $2');
  }

  prettyJson(payload: string): string {
    try {
      return JSON.stringify(JSON.parse(payload), null, 2);
    } catch {
      return payload;
    }
  }

  private async run(operation: () => Promise<void>): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      await operation();
    } catch (err) {
      this.error.set(this.describe(err));
    } finally {
      this.loading.set(false);
    }
  }

  private describe(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      return (err.error as { title?: string } | null)?.title ??
        (err.status === 403 ? this.i18n.t('trail.restricted') : `Request failed (${err.status}).`);
    }
    return 'Unexpected error.';
  }
}
