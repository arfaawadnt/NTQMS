import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AccessReviewsApiService } from '../../core/api/access-reviews-api.service';
import { I18nService } from '../../core/i18n.service';
import { UserAccessReview } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { ExportColumn, ExportMenuComponent } from '../../shared/ui/export-menu.component';

/**
 * Periodic user-access review / recertification (21 CFR Part 11 §11.10(d) /
 * EU Annex 11 §12). A tenant administrator opens a review, examines the account
 * roster and roles on the Users screen, then records the conclusion here. The
 * completed review — with the count of accounts recertified — is the evidence.
 */
@Component({
    selector: 'qams-access-reviews',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [FormsModule, DatePipe, PageHeaderComponent, ExportMenuComponent],
    template: `
    <qams-page-header [title]="i18n.t('uar.title')" [subtitle]="i18n.t('uar.subtitle')">
      <qams-export-menu [title]="i18n.t('uar.title')" [columns]="exportColumns" [rows]="reviews()"
                        [filtersSummary]="i18n.t('exp.allRecords')" />
      <button (click)="open()" [disabled]="loading()">{{ i18n.t('uar.open') }}</button>
    </qams-page-header>

    @if (error()) { <div class="error">{{ error() }}</div> }
    @if (loading()) { <p class="muted">{{ i18n.t('common.loading') }}</p> }

    @if (!loading()) {
      <div class="card">
        @if (reviews().length === 0) { <p class="muted">{{ i18n.t('uar.empty') }}</p> }
        @else {
          <table>
            <thead><tr>
              <th>{{ i18n.t('mu.ref') }}</th><th>{{ i18n.t('uar.openedOn') }}</th><th>{{ i18n.t('nc.status') }}</th>
              <th>{{ i18n.t('uar.accounts') }}</th><th>{{ i18n.t('uar.changes') }}</th><th>{{ i18n.t('atr.conclusion') }}</th>
            </tr></thead>
            <tbody>
              @for (r of reviews(); track r.id) {
                <tr>
                  <td class="code">{{ r.reviewRef }}</td>
                  <td>{{ r.openedOn | date:'mediumDate' }}</td>
                  <td>{{ r.status }}</td>
                  <td>{{ r.accountsReviewed ?? '—' }}</td>
                  <td>
                    @if (r.changesRequired === true) { <span class="bad">✕ {{ i18n.t('common.yes') }}</span> }
                    @else if (r.changesRequired === false) { <span class="good">✓ {{ i18n.t('common.no') }}</span> }
                    @else { — }
                  </td>
                  <td class="muted">{{ r.conclusion ?? '—' }}</td>
                </tr>
                @if (r.status === 'Open') {
                  <tr><td colspan="6">
                    <div class="completerow">
                      <label class="chk"><input type="checkbox" [(ngModel)]="changesRequired" /> {{ i18n.t('uar.changesFound') }}</label>
                      <input class="grow" [(ngModel)]="conclusion" [placeholder]="i18n.t('uar.conclusionHint')" />
                      <button (click)="complete(r.id)" [disabled]="!conclusion.trim()">{{ i18n.t('uar.complete') }}</button>
                    </div>
                  </td></tr>
                }
              }
            </tbody>
          </table>
        }
        <div class="hint">{{ i18n.t('uar.note') }}</div>
      </div>
    }
  `,
    styles: [`
    .completerow { display: flex; gap: 10px; align-items: center; padding: 6px 0; flex-wrap: wrap; }
    .completerow .grow { flex: 1; min-width: 240px; }
    .chk { display: flex; gap: 6px; align-items: center; white-space: nowrap; }
    .chk input { width: auto; }
    .good { color: var(--nt-green); font-weight: 600; }
    .bad { color: var(--nt-red); font-weight: 600; }
    .hint { font-size: 11.5px; color: var(--nt-slate); margin-top: 10px; }
    button { width: auto; }
  `]
})
export class AccessReviewsComponent implements OnInit {
  readonly i18n = inject(I18nService);
  private readonly api = inject(AccessReviewsApiService);

  readonly reviews = signal<UserAccessReview[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');

  /** Export columns — the printed grid mirrors the on-screen table. */
  readonly exportColumns: ExportColumn<UserAccessReview>[] = [
    { header: this.i18n.t('mu.ref'), cell: (r) => r.reviewRef },
    { header: this.i18n.t('uar.openedOn'), cell: (r) => r.openedOn },
    { header: this.i18n.t('nc.status'), cell: (r) => r.status },
    { header: this.i18n.t('uar.accounts'), cell: (r) => r.accountsReviewed === null ? '—' : `${r.accountsReviewed}` },
    {
      header: this.i18n.t('uar.changes'),
      cell: (r) => r.changesRequired === true
        ? `✕ ${this.i18n.t('common.yes')}`
        : r.changesRequired === false ? `✓ ${this.i18n.t('common.no')}` : '—',
    },
    { header: this.i18n.t('atr.conclusion'), cell: (r) => r.conclusion ?? '—' },
  ];
  changesRequired = false;
  conclusion = '';

  ngOnInit(): void { void this.load(); }

  async load(): Promise<void> {
    await this.run(async () => this.reviews.set(await firstValueFrom(this.api.list())));
  }

  async open(): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.open());
      this.reviews.set(await firstValueFrom(this.api.list()));
    });
  }

  async complete(id: string): Promise<void> {
    if (!this.conclusion.trim()) { return; }
    await this.run(async () => {
      await firstValueFrom(this.api.complete(id, this.changesRequired, this.conclusion.trim()));
      this.changesRequired = false;
      this.conclusion = '';
      this.reviews.set(await firstValueFrom(this.api.list()));
    });
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
