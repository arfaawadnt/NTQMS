import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { CompetencyFacade } from './competency.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';

/** Competency matrix: status-filterable list + an assign form (role-gated). */
@Component({
  selector: 'qams-competency-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, PageHeaderComponent, StatusPillComponent],
  template: `
    <qams-page-header [title]="i18n.t('comp.title')">
      <select [value]="statusFilter()" (change)="onFilter($event)" aria-label="Status filter">
        <option value="">{{ i18n.t('nc.allStatuses') }}</option>
        @for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
      </select>
      @if (perms.canAssignTraining()) {
        <button (click)="showForm.set(!showForm())">{{ i18n.t('comp.new') }}</button>
      }
    </qams-page-header>

    @if (showForm()) {
      <form class="card form" [formGroup]="form" (ngSubmit)="assign()">
        <div class="grid">
          <div class="col-2"><label>{{ i18n.t('comp.subject') }}</label><input formControlName="subject" /></div>
          <div><label>{{ i18n.t('comp.trainee') }}</label><input formControlName="traineeId" [placeholder]="i18n.t('comp.userId')" /></div>
          <div><label>{{ i18n.t('comp.validity') }}</label><input type="number" min="1" formControlName="validityMonths" /></div>
          <div class="col-2"><label>{{ i18n.t('comp.documentId') }}</label><input formControlName="documentId" [placeholder]="i18n.t('common.optional')" /></div>
        </div>
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('comp.assign') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    }

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.list().length === 0) {
      <p class="muted">{{ i18n.t('comp.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('comp.subject') }}</th><th>{{ i18n.t('comp.trainee') }}</th>
            <th>{{ i18n.t('nc.status') }}</th><th>{{ i18n.t('comp.expires') }}</th>
          </tr></thead>
          <tbody>
            @for (c of facade.list(); track c.id) {
              <tr class="clickable" (click)="open(c.id)">
                <td>{{ c.subject }}</td><td class="mono">{{ c.traineeId }}</td>
                <td><qams-status-pill [status]="c.status" /></td>
                <td>{{ c.expiresAt ? (c.expiresAt | date:'mediumDate') : '—' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: [`
    .form { margin-bottom: 1rem; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: .5rem 1rem; }
    .col-2 { grid-column: span 2; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    .mono { font-family: var(--nt-mono, monospace); font-size: .82rem; }
    button, select { width: auto; }
  `],
})
export class CompetencyListComponent implements OnInit {
  readonly facade = inject(CompetencyFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly statuses = ['PendingTraining', 'Evaluated', 'Authorized', 'Revoked', 'Expired'];
  readonly showForm = signal(false);
  readonly statusFilter = signal('');

  readonly form = this.fb.nonNullable.group({
    subject: ['', [Validators.required, Validators.maxLength(200)]],
    traineeId: ['', [Validators.required]],
    validityMonths: [12, [Validators.required, Validators.min(1)]],
    documentId: [''],
  });

  ngOnInit(): void { void this.facade.loadList(); }

  onFilter(event: Event): void {
    this.statusFilter.set((event.target as HTMLSelectElement).value);
    void this.facade.loadList(this.statusFilter() || undefined);
  }

  async assign(): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    const id = await this.facade.assignCompetency({
      subject: raw.subject,
      traineeId: raw.traineeId,
      validityMonths: raw.validityMonths,
      documentId: raw.documentId || null,
    });
    if (id) { this.cancel(); void this.router.navigate(['/competencies', id]); }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset({ validityMonths: 12 });
  }

  open(id: string): void { void this.router.navigate(['/competencies', id]); }
}
