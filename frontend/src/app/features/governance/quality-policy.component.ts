import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { QualityPolicyApiService } from '../../core/api/quality-policy-api.service';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { QualityPolicy } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';

/**
 * The controlled quality policy (ISO 9001 §5.2 / ISO 17025 §8.2). Everyone sees the
 * current statement (it must be communicated); quality management can draft a new
 * version, edit it while it is a draft, and approve it — approval activates it, and
 * the backend supersedes the prior version so exactly one policy is ever in force.
 * The approver cannot be the author (segregation of duties).
 */
@Component({
    selector: 'qams-quality-policy',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [FormsModule, DatePipe, PageHeaderComponent],
    template: `
    <qams-page-header [title]="i18n.t('qp.title')" [subtitle]="i18n.t('qp.subtitle')">
      @if (perms.canApprove()) {
        <button (click)="showDraft.set(!showDraft())">{{ i18n.t('qp.newVersion') }}</button>
      }
    </qams-page-header>

    @if (error()) { <div class="error">{{ error() }}</div> }
    @if (loading()) { <p class="muted">{{ i18n.t('common.loading') }}</p> }

    @if (!loading()) {
      @if (active(); as a) {
        <div class="card policy">
          <div class="policy-meta">
            <span class="badge">{{ i18n.t('qp.inForce') }}</span>
            <span class="code">{{ a.policyRef }} · v{{ a.version }}</span>
            @if (a.effectiveDate) { <span class="muted">{{ i18n.t('qp.effective') }} {{ a.effectiveDate | date:'mediumDate' }}</span> }
          </div>
          <p class="statement">{{ a.statement }}</p>
        </div>
      } @else {
        <div class="card"><p class="muted">{{ i18n.t('qp.none') }}</p></div>
      }

      @if (perms.canApprove()) {
        @if (showDraft()) {
          <div class="card">
            <h3>{{ i18n.t('qp.draftHeading') }}</h3>
            <textarea rows="6" [(ngModel)]="draftText" [placeholder]="i18n.t('qp.statementHint')"></textarea>
            <div class="row">
              <button (click)="createDraft()" [disabled]="!draftText.trim()">{{ i18n.t('qp.saveDraft') }}</button>
              <button class="secondary" (click)="cancelDraft()">{{ i18n.t('nc.cancel') }}</button>
            </div>
          </div>
        }

        <div class="card">
          <h3>{{ i18n.t('qp.history') }}</h3>
          @if (history().length === 0) { <p class="muted">{{ i18n.t('qp.noHistory') }}</p> }
          @else {
            <table>
              <thead><tr>
                <th>{{ i18n.t('mu.ref') }}</th><th>v</th><th>{{ i18n.t('nc.status') }}</th>
                <th>{{ i18n.t('qp.effective') }}</th><th>{{ i18n.t('qp.statement') }}</th><th></th>
              </tr></thead>
              <tbody>
                @for (p of history(); track p.id) {
                  <tr>
                    <td class="code">{{ p.policyRef }}</td>
                    <td>{{ p.version }}</td>
                    <td>{{ p.status }}</td>
                    <td>{{ p.effectiveDate ? (p.effectiveDate | date:'mediumDate') : '—' }}</td>
                    <td class="truncate">{{ p.statement }}</td>
                    <td class="actions">
                      @if (p.status === 'Draft') {
                        <input type="date" [(ngModel)]="effectiveDates[p.id]" [attr.aria-label]="i18n.t('qp.effective')" />
                        <button class="link" type="button" (click)="approve(p.id)" [disabled]="!effectiveDates[p.id]">{{ i18n.t('qp.approve') }}</button>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      }
    }
  `,
    styles: [`
    .policy { border-left: 3px solid var(--nt-blue); }
    .policy-meta { display: flex; gap: 12px; align-items: center; margin-bottom: 10px; flex-wrap: wrap; }
    .badge { background: rgba(24,128,56,.1); color: var(--nt-green); font-weight: 700; font-size: 11px; padding: 3px 10px; border-radius: 999px; }
    .statement { font-size: 15px; line-height: 1.6; white-space: pre-wrap; margin: 0; }
    h3 { margin: 0 0 12px; font-size: 14px; }
    textarea { width: 100%; font: inherit; padding: 8px 10px; box-sizing: border-box; }
    .row { display: flex; gap: .6rem; margin-top: .8rem; }
    .actions { white-space: nowrap; display: flex; gap: 8px; align-items: center; }
    .actions input { max-width: 160px; }
    .truncate { max-width: 340px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    button, select { width: auto; }
  `]
})
export class QualityPolicyComponent implements OnInit {
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly api = inject(QualityPolicyApiService);

  readonly active = signal<QualityPolicy | null>(null);
  readonly history = signal<QualityPolicy[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly showDraft = signal(false);
  draftText = '';
  /** Per-draft effective date entered before approval (keyed by policy id). */
  effectiveDates: Record<string, string> = {};

  ngOnInit(): void { void this.load(); }

  async load(): Promise<void> {
    await this.run(async () => {
      this.active.set(await firstValueFrom(this.api.active()));
      if (this.perms.canApprove()) {
        this.history.set(await firstValueFrom(this.api.history()));
      }
    });
  }

  async createDraft(): Promise<void> {
    if (!this.draftText.trim()) { return; }
    await this.run(async () => {
      await firstValueFrom(this.api.draft(this.draftText.trim()));
      this.cancelDraft();
      await this.reload();
    });
  }

  async approve(id: string): Promise<void> {
    const effectiveDate = this.effectiveDates[id];
    if (!effectiveDate) { return; }
    await this.run(async () => {
      await firstValueFrom(this.api.approve(id, effectiveDate));
      await this.reload();
    });
  }

  cancelDraft(): void {
    this.showDraft.set(false);
    this.draftText = '';
  }

  private async reload(): Promise<void> {
    this.active.set(await firstValueFrom(this.api.active()));
    this.history.set(await firstValueFrom(this.api.history()));
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
      return (err.error as { title?: string } | null)?.title ?? `Request failed (${err.status}).`;
    }
    return 'Unexpected error.';
  }
}
