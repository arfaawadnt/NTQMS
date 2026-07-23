import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ChangeFacade } from './change.facade';
import { I18nService } from '../../core/i18n.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';

/** Change Control register: status-filterable list + a propose form. */
@Component({
  selector: 'qams-change-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, PageHeaderComponent, StatusPillComponent],
  template: `
    <qams-page-header [title]="i18n.t('chg.title')">
      <select [value]="statusFilter()" (change)="onFilter($event)" aria-label="Status filter">
        <option value="">{{ i18n.t('nc.allStatuses') }}</option>
        @for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
      </select>
      <button (click)="showForm.set(!showForm())">{{ i18n.t('chg.new') }}</button>
    </qams-page-header>

    @if (showForm()) {
      <form class="card form" [formGroup]="form" (ngSubmit)="propose()">
        <label>{{ i18n.t('chg.changeTitle') }}</label>
        <input formControlName="title" />
        <label>{{ i18n.t('chg.impact') }}</label>
        <textarea formControlName="impactAnalysis" rows="4" [placeholder]="i18n.t('chg.impactHint')"></textarea>
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('chg.propose') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    }

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.list().length === 0) {
      <p class="muted">{{ i18n.t('chg.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('chg.ref') }}</th><th>{{ i18n.t('chg.changeTitle') }}</th>
            <th>{{ i18n.t('nc.status') }}</th><th>{{ i18n.t('chg.riskLinked') }}</th>
          </tr></thead>
          <tbody>
            @for (c of facade.list(); track c.id) {
              <tr class="clickable" (click)="open(c.id)">
                <td>{{ c.changeRef }}</td><td>{{ c.title }}</td>
                <td><qams-status-pill [status]="c.status" /></td>
                <td>{{ c.riskItemId ? i18n.t('common.yes') : i18n.t('common.no') }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: [`
    .form { margin-bottom: 1rem; }
    .form textarea { width: 100%; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; } .clickable:hover { background: #f4f6f9; }
    button, select { width: auto; }
  `],
})
export class ChangeListComponent implements OnInit {
  readonly facade = inject(ChangeFacade);
  readonly i18n = inject(I18nService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly statuses = ['Proposed', 'Approved', 'Rejected', 'Closed'];
  readonly showForm = signal(false);
  readonly statusFilter = signal('');

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    impactAnalysis: ['', [Validators.required, Validators.maxLength(4000)]],
  });

  ngOnInit(): void { void this.facade.loadList(); }

  onFilter(event: Event): void {
    this.statusFilter.set((event.target as HTMLSelectElement).value);
    void this.facade.loadList(this.statusFilter() || undefined);
  }

  async propose(): Promise<void> {
    if (this.form.invalid) { return; }
    const id = await this.facade.propose(this.form.getRawValue());
    if (id) { this.cancel(); void this.router.navigate(['/changes', id]); }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset();
  }

  open(id: string): void { void this.router.navigate(['/changes', id]); }
}
