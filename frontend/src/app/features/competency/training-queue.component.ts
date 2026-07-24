import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { CompetencyFacade } from './competency.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';

/** Training queue: assign training (role-gated), toggle completed, and mark items complete. */
@Component({
  selector: 'qams-training-queue',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, PageHeaderComponent, DrawerComponent],
  template: `
    <qams-page-header [title]="i18n.t('train.title')">
      <label class="inline"><input type="checkbox" [checked]="includeCompleted()" (change)="onToggle($event)" /> {{ i18n.t('train.showCompleted') }}</label>
      @if (perms.canAssignTraining()) {
        <button (click)="showForm.set(!showForm())">{{ i18n.t('train.new') }}</button>
      }
    </qams-page-header>

    <qams-drawer [open]="showForm()" [title]="i18n.t('train.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="assign()">
        <div class="grid">
          <div class="col-2"><label>{{ i18n.t('comp.subject') }}</label><input formControlName="subject" /></div>
          <div><label>{{ i18n.t('comp.trainee') }}</label><input formControlName="traineeId" [placeholder]="i18n.t('comp.userId')" /></div>
          <div><label>{{ i18n.t('train.dueDate') }}</label><input type="date" formControlName="dueDate" /></div>
          <div class="col-2"><label>{{ i18n.t('comp.documentId') }}</label><input formControlName="documentId" [placeholder]="i18n.t('common.optional')" /></div>
        </div>
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('comp.assign') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.training().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.training().length === 0) {
      <p class="muted">{{ i18n.t('train.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('comp.subject') }}</th><th>{{ i18n.t('comp.trainee') }}</th>
            <th>{{ i18n.t('train.dueDate') }}</th><th>{{ i18n.t('nc.status') }}</th><th></th>
          </tr></thead>
          <tbody>
            @for (t of facade.training(); track t.id) {
              <tr [class.overdue]="!t.completed && isOverdue(t.dueDate)">
                <td>{{ t.subject }}</td><td class="mono">{{ t.traineeId }}</td>
                <td>{{ t.dueDate | date:'mediumDate' }}</td>
                <td>{{ t.completed ? i18n.t('train.completed') : i18n.t('train.pending') }}</td>
                <td>
                  @if (!t.completed) {
                    <button class="link" (click)="complete(t.id)">{{ i18n.t('train.markComplete') }}</button>
                  } @else {
                    <span class="muted">{{ t.completedAtUtc | date:'mediumDate' }}</span>
                  }
                </td>
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
    .inline { display: inline-flex; align-items: center; gap: .35rem; font-size: .85rem; }
    .inline input { width: auto; }
    .mono { font-family: var(--nt-mono, monospace); font-size: .82rem; }
    .overdue td { color: var(--nt-red, #b42318); }
    .link { width: auto; background: none; color: var(--nt-blue); padding: 0; }
    button, select { width: auto; }
  `],
})
export class TrainingQueueComponent implements OnInit {
  readonly facade = inject(CompetencyFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  readonly showForm = signal(false);
  readonly includeCompleted = signal(false);

  readonly form = this.fb.nonNullable.group({
    subject: ['', [Validators.required, Validators.maxLength(200)]],
    traineeId: ['', [Validators.required]],
    dueDate: ['', [Validators.required]],
    documentId: [''],
  });

  ngOnInit(): void { void this.facade.loadTraining(this.includeCompleted()); }

  onToggle(event: Event): void {
    this.includeCompleted.set((event.target as HTMLInputElement).checked);
    void this.facade.loadTraining(this.includeCompleted());
  }

  isOverdue(dueDate: string): boolean { return new Date(dueDate).getTime() < Date.now(); }

  async assign(): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    const id = await this.facade.assignTraining({
      subject: raw.subject,
      traineeId: raw.traineeId,
      dueDate: raw.dueDate,
      documentId: raw.documentId || null,
    });
    if (id) { this.cancel(); void this.facade.loadTraining(this.includeCompleted()); }
  }

  async complete(id: string): Promise<void> {
    await this.facade.completeTraining(id, this.includeCompleted());
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset();
  }
}
