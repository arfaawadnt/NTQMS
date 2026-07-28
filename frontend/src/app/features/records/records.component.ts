import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RecordsFacade } from './records.facade';
import { ChangeReasonService } from '../../core/change-reason.service';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { ARCHIVE_SOURCE_MODULES, RETENTION_CLASSES, RetentionClass } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { LoadMoreComponent } from '../../shared/ui/load-more.component';

/**
 * Records & Retention register: archived record snapshots with per-row
 * lifecycle actions — retrieve (check out), return, and QM-gated disposal.
 * Disposal of permanent-retention records, or before the retention expiry,
 * is refused by the backend and surfaced here (ARC-013/014).
 */
@Component({
  selector: 'qams-records',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, PageHeaderComponent, DrawerComponent, StatusPillComponent, LoadMoreComponent],
  template: `
    <qams-page-header [title]="i18n.t('arc.title')" [subtitle]="i18n.t('arc.subtitle')">
      <select [value]="stateFilter()" (change)="onFilter($event)" aria-label="State filter">
        <option value="">{{ i18n.t('nc.allStatuses') }}</option>
        @for (s of states; track s) { <option [value]="s">{{ s }}</option> }
      </select>
      <button (click)="showForm.set(!showForm())">{{ i18n.t('arc.new') }}</button>
    </qams-page-header>

    <qams-drawer [open]="showForm()" [title]="i18n.t('arc.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="archive()">
        <label>{{ i18n.t('arc.sourceModule') }}</label>
        <select formControlName="sourceModule">
          @for (m of modules; track m) { <option [value]="m">{{ m }}</option> }
        </select>
        <label>{{ i18n.t('arc.sourceRef') }}</label>
        <input formControlName="sourceRef" [placeholder]="i18n.t('arc.sourceRefHint')" />
        <label>{{ i18n.t('arc.retention') }}</label>
        <select formControlName="retentionClass">
          @for (c of classes; track c) { <option [value]="c">{{ c }}</option> }
        </select>
        <label>{{ i18n.t('arc.snapshot') }} *</label>
        <input type="file" (change)="onFile($event)" required />
        <p class="hint">{{ i18n.t('arc.snapshotRequired') }}</p>
        <div class="row">
          <button type="submit" [disabled]="form.invalid || !snapshot() || facade.loading()">{{ i18n.t('arc.archive') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.error() && !showForm()) { <div class="error">{{ facade.error() }}</div> }
    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.list().length === 0) {
      <p class="muted">{{ i18n.t('arc.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('arc.ref') }}</th><th>{{ i18n.t('arc.sourceModule') }}</th><th>{{ i18n.t('arc.sourceRef') }}</th>
            <th>{{ i18n.t('arc.retention') }}</th><th>{{ i18n.t('arc.expiry') }}</th><th>{{ i18n.t('nc.status') }}</th><th></th>
          </tr></thead>
          <tbody>
            @for (a of facade.list(); track a.id) {
              <tr>
                <td class="code">{{ a.archiveRef }}</td>
                <td>{{ a.sourceModule }}</td>
                <td class="code">{{ a.sourceRef }}</td>
                <td>{{ a.retentionClass }}</td>
                <td>{{ a.retentionExpiry ? (a.retentionExpiry | date:'mediumDate') : i18n.t('arc.permanent') }}</td>
                <td>
                  <qams-status-pill [status]="a.state" />
                  @if (a.isOnLegalHold) { <span class="hold" [title]="i18n.t('arc.onHoldNote')">⚖ {{ i18n.t('arc.onHold') }}</span> }
                </td>
                <td class="actions">
                  @if (a.state === 'Archived') {
                    <button class="link" type="button" (click)="facade.retrieve(a.id)">{{ i18n.t('arc.retrieve') }}</button>
                    @if (perms.canApprove() && a.retentionClass !== 'Permanent' && !a.isOnLegalHold) {
                      <button class="link danger-link" type="button" (click)="facade.dispose(a.id)">{{ i18n.t('arc.dispose') }}</button>
                    }
                  } @else if (a.state === 'Retrieved') {
                    <button class="link" type="button" (click)="facade.return(a.id)">{{ i18n.t('arc.return') }}</button>
                  }
                  @if (perms.canApprove() && a.state !== 'Disposed') {
                    @if (a.isOnLegalHold) {
                      <button class="link" type="button" (click)="facade.releaseLegalHold(a.id)">{{ i18n.t('arc.releaseHold') }}</button>
                    } @else {
                      <button class="link" type="button" (click)="placeHold(a.id)">{{ i18n.t('arc.placeHold') }}</button>
                    }
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      <qams-load-more [shown]="facade.list().length" [total]="facade.total()" [hasMore]="facade.hasMore()"
                      [loading]="facade.loading()" (more)="facade.loadMore()" />
    }
  `,
  styles: [`
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .actions { white-space: nowrap; }
    .danger-link { color: var(--nt-red); }
    .hint { font-size: 11.5px; color: var(--nt-slate); margin: 2px 0 0; }
    .hold { margin-left: 6px; font-size: 11px; font-weight: 700; color: var(--nt-red); white-space: nowrap; }
    button, select { width: auto; }
  `],
})
export class RecordsComponent implements OnInit {
  readonly facade = inject(RecordsFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly reasons = inject(ChangeReasonService);

  readonly states = ['Archived', 'Retrieved', 'Disposed'];
  readonly modules = ARCHIVE_SOURCE_MODULES;
  readonly classes = RETENTION_CLASSES;
  readonly showForm = signal(false);
  readonly stateFilter = signal('');
  readonly snapshot = signal<File | null>(null);

  readonly form = this.fb.nonNullable.group({
    sourceModule: ['Nonconformance', [Validators.required]],
    sourceRef: ['', [Validators.required, Validators.maxLength(100)]],
    retentionClass: ['FiveYears' as RetentionClass, [Validators.required]],
  });

  ngOnInit(): void { void this.facade.loadList(); }

  onFilter(event: Event): void {
    this.stateFilter.set((event.target as HTMLSelectElement).value);
    void this.facade.loadList(this.stateFilter() || undefined);
  }

  onFile(event: Event): void { this.snapshot.set((event.target as HTMLInputElement).files?.[0] ?? null); }

  /** Legal hold requires a reason (litigation/investigation ref) — captured in the Part 11 modal. */
  async placeHold(id: string): Promise<void> {
    const reason = await this.reasons.request('arc.placeHold');
    if (reason) { void this.facade.placeLegalHold(id, reason); }
  }

  async archive(): Promise<void> {
    if (this.form.invalid) { return; }
    const { sourceModule, sourceRef, retentionClass } = this.form.getRawValue();
    if (await this.facade.archive(sourceModule, sourceRef, retentionClass, this.snapshot())) {
      this.cancel();
    }
  }

  cancel(): void {
    this.showForm.set(false);
    this.snapshot.set(null);
    this.form.reset({ sourceModule: 'Nonconformance', retentionClass: 'FiveYears' });
  }
}
