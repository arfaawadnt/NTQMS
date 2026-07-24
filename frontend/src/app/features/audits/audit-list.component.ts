import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { AuditsFacade } from './audits.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { AUDIT_TYPES, AuditType, ChecklistItemRequest } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';

/** Audit register + a schedule form with a dynamic ISO-clause checklist (FormArray). */
@Component({
  selector: 'qams-audit-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, PageHeaderComponent, StatusPillComponent],
  template: `
    <qams-page-header [title]="i18n.t('audit.title')">
      @if (perms.canApprove()) { <button (click)="showForm.set(!showForm())">{{ i18n.t('audit.new') }}</button> }
    </qams-page-header>

    @if (showForm()) {
      <form class="card form" [formGroup]="form" (ngSubmit)="schedule()">
        <div class="grid">
          <div class="col-2"><label>{{ i18n.t('audit.auditTitle') }}</label><input formControlName="title" /></div>
          <div><label>{{ i18n.t('audit.type') }}</label>
            <select formControlName="type">@for (t of types; track t) { <option [value]="t">{{ t }}</option> }</select></div>
          <div><label>{{ i18n.t('audit.leadAuditor') }}</label>
            <input formControlName="leadAuditorId" [placeholder]="i18n.t('nc.userIdHint')" /></div>
          <div><label>{{ i18n.t('audit.plannedDate') }}</label><input type="date" formControlName="plannedDate" /></div>
        </div>

        <div class="checklist-head">
          <label>{{ i18n.t('audit.checklist') }}</label>
          <button type="button" class="ghost" (click)="addItem()">＋ {{ i18n.t('audit.addItem') }}</button>
        </div>
        <div formArrayName="checklist">
          @for (row of checklist.controls; track $index; let i = $index) {
            <div class="item" [formGroupName]="i">
              <input formControlName="isoClause" [placeholder]="i18n.t('audit.clause')" class="clause" />
              <input formControlName="question" [placeholder]="i18n.t('audit.question')" />
              <button type="button" class="ghost" (click)="removeItem(i)" [disabled]="checklist.length === 1">✕</button>
            </div>
          }
        </div>

        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('audit.schedule') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    }

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.list().length === 0) {
      <p class="muted">{{ i18n.t('audit.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('audit.ref') }}</th><th>{{ i18n.t('audit.auditTitle') }}</th>
            <th>{{ i18n.t('audit.type') }}</th><th>{{ i18n.t('audit.plannedDate') }}</th><th>{{ i18n.t('nc.status') }}</th>
          </tr></thead>
          <tbody>
            @for (a of facade.list(); track a.id) {
              <tr class="clickable" (click)="open(a.id)">
                <td>{{ a.auditRef }}</td><td>{{ a.title }}</td><td>{{ a.type }}</td>
                <td>{{ a.plannedDate | date:'mediumDate' }}</td>
                <td><qams-status-pill [status]="a.status" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: [`
    .form { margin-bottom: 1rem; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: .5rem 1rem; }
    .col-2 { grid-column: span 2; }
    .checklist-head { display: flex; justify-content: space-between; align-items: center; margin-top: 1rem; }
    .item { display: flex; gap: .5rem; margin-bottom: .4rem; }
    .item .clause { max-width: 120px; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    button { width: auto; }
  `],
})
export class AuditListComponent implements OnInit {
  readonly facade = inject(AuditsFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly types = AUDIT_TYPES;
  readonly showForm = signal(false);

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(300)]],
    type: ['Internal' as AuditType, [Validators.required]],
    leadAuditorId: ['', [Validators.required]],
    plannedDate: ['', [Validators.required]],
    checklist: this.fb.array([this.newItem()]),
  });

  get checklist(): FormArray { return this.form.controls.checklist; }

  ngOnInit(): void { void this.facade.loadList(); }

  private newItem(): FormGroup {
    return this.fb.nonNullable.group({
      isoClause: ['', [Validators.maxLength(30)]],
      question: ['', [Validators.required, Validators.maxLength(1000)]],
    });
  }

  addItem(): void { this.checklist.push(this.newItem()); }
  removeItem(index: number): void { if (this.checklist.length > 1) { this.checklist.removeAt(index); } }

  async schedule(): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    const checklist: ChecklistItemRequest[] = raw.checklist.map((c) => ({ isoClause: c['isoClause'], question: c['question'] }));
    const id = await this.facade.schedule({
      title: raw.title, type: raw.type, leadAuditorId: raw.leadAuditorId,
      plannedDate: raw.plannedDate, checklist,
    });
    if (id) { this.cancel(); void this.router.navigate(['/audits', id]); }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset({ type: 'Internal' });
    this.checklist.clear();
    this.checklist.push(this.newItem());
  }

  open(id: string): void { void this.router.navigate(['/audits', id]); }
}
