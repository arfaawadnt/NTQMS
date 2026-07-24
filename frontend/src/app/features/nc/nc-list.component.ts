import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { signal } from '@angular/core';
import { NcFacade } from './nc.facade';
import { I18nService } from '../../core/i18n.service';
import { NC_SOURCE_TYPES, NcSourceType } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';

/** Nonconformance register: filterable list + a reactive "raise NC" form. */
@Component({
  selector: 'qams-nc-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, PageHeaderComponent, DrawerComponent, StatusPillComponent],
  template: `
    <qams-page-header [title]="i18n.t('nc.title')">
      <select [value]="statusFilter()" (change)="onFilter($event)" aria-label="Status filter">
        <option value="">{{ i18n.t('nc.allStatuses') }}</option>
        @for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
      </select>
      <button (click)="showForm.set(!showForm())">{{ i18n.t('nc.new') }}</button>
    </qams-page-header>

    <qams-drawer [open]="showForm()" [title]="i18n.t('nc.new')" (closed)="showForm.set(false)">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="create()">
        <div class="grid">
          <div class="col-2">
            <label>{{ i18n.t('nc.subject') }}</label>
            <input formControlName="title" />
          </div>
          <div>
            <label>{{ i18n.t('nc.source') }}</label>
            <select formControlName="sourceType">
              @for (s of sources; track s) { <option [value]="s">{{ s }}</option> }
            </select>
          </div>
          <div>
            <label>{{ i18n.t('nc.severity') }} (1-5)</label>
            <input type="number" min="1" max="5" formControlName="severity" />
          </div>
          <div>
            <label>{{ i18n.t('nc.likelihood') }} (1-5)</label>
            <input type="number" min="1" max="5" formControlName="likelihood" />
          </div>
        </div>
        <label>{{ i18n.t('nc.description') }}</label>
        <textarea rows="3" formControlName="description"></textarea>
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('nc.create') }}</button>
          <button type="button" class="secondary" (click)="showForm.set(false)">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.list().length === 0) {
      <p class="muted">{{ i18n.t('nc.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead>
            <tr>
              <th>{{ i18n.t('nc.ref') }}</th><th>{{ i18n.t('nc.subject') }}</th>
              <th>{{ i18n.t('nc.status') }}</th><th>{{ i18n.t('nc.severity') }}</th>
              <th>{{ i18n.t('nc.rpn') }}</th><th>{{ i18n.t('nc.source') }}</th>
            </tr>
          </thead>
          <tbody>
            @for (nc of facade.list(); track nc.id) {
              <tr class="clickable" (click)="open(nc.id)">
                <td>{{ nc.ncRef }}</td>
                <td>{{ nc.title }}</td>
                <td><qams-status-pill [status]="nc.status" /></td>
                <td>{{ nc.severity }}</td>
                <td [class.danger-text]="nc.rpn > 12">{{ nc.rpn }}</td>
                <td>{{ nc.sourceType }}</td>
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
   
    .danger-text { color: var(--nt-danger); font-weight: 700; }
    select, button { width: auto; }
  `],
})
export class NcListComponent implements OnInit {
  readonly facade = inject(NcFacade);
  readonly i18n = inject(I18nService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly sources = NC_SOURCE_TYPES;
  readonly statuses = ['Draft', 'Raised', 'Assigned', 'Rca', 'ActionPlan', 'PendingVerification', 'EffectivenessCheck', 'Closed', 'Rejected'];
  readonly showForm = signal(false);
  readonly statusFilter = signal('');

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(300)]],
    description: ['', [Validators.maxLength(4000)]],
    severity: [3, [Validators.required, Validators.min(1), Validators.max(5)]],
    likelihood: [3, [Validators.required, Validators.min(1), Validators.max(5)]],
    sourceType: ['Internal' as NcSourceType, [Validators.required]],
  });

  ngOnInit(): void {
    void this.facade.loadList();
  }

  onFilter(event: Event): void {
    this.statusFilter.set((event.target as HTMLSelectElement).value);
    void this.facade.loadList(this.statusFilter() || undefined);
  }

  async create(): Promise<void> {
    if (this.form.invalid) { return; }
    const id = await this.facade.raise(this.form.getRawValue());
    if (id) {
      this.showForm.set(false);
      this.form.reset({ severity: 3, likelihood: 3, sourceType: 'Internal' });
      void this.router.navigate(['/nonconformances', id]);
    }
  }

  open(id: string): void {
    void this.router.navigate(['/nonconformances', id]);
  }
}
