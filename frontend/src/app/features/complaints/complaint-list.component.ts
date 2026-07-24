import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { ComplaintsFacade } from './complaints.facade';
import { I18nService } from '../../core/i18n.service';
import { COMPLAINT_CHANNELS, ComplaintChannel } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';

/** Complaints registry: status-filterable list + a log form (any authenticated user). */
@Component({
  selector: 'qams-complaint-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent],
  template: `
    <qams-page-header [title]="i18n.t('cmpl.title')" [subtitle]="i18n.t('cmpl.subtitle')">
      <select [value]="statusFilter()" (change)="onFilter($event)" aria-label="Status filter">
        <option value="">{{ i18n.t('nc.allStatuses') }}</option>
        @for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
      </select>
      <button (click)="showForm.set(!showForm())">{{ i18n.t('cmpl.new') }}</button>
    </qams-page-header>

    <qams-drawer [open]="showForm()" [title]="i18n.t('cmpl.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="log()">
        <label>{{ i18n.t('cmpl.subject') }}</label>
        <input formControlName="subject" />
        <label>{{ i18n.t('nc.description') }}</label>
        <textarea formControlName="description" rows="4"></textarea>
        <label>{{ i18n.t('cmpl.channel') }}</label>
        <select formControlName="channel">
          @for (c of channels; track c) { <option [value]="c">{{ c }}</option> }
        </select>
        <label>{{ i18n.t('cmpl.complainant') }}</label>
        <input formControlName="complainantName" />
        <label>{{ i18n.t('cmpl.contact') }}</label>
        <input formControlName="complainantContact" [placeholder]="i18n.t('common.optional')" />
        <label class="inline">
          <input type="checkbox" formControlName="confidential" /> {{ i18n.t('cmpl.confidential') }}
        </label>
        <div class="hint">{{ i18n.t('cmpl.confidentialHint') }}</div>
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('cmpl.log') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.list().length === 0) {
      <p class="muted">{{ i18n.t('cmpl.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('cmpl.ref') }}</th><th>{{ i18n.t('cmpl.subject') }}</th>
            <th>{{ i18n.t('cmpl.channel') }}</th><th>{{ i18n.t('cmpl.complainant') }}</th>
            <th>{{ i18n.t('cmpl.loggedAt') }}</th><th>{{ i18n.t('nc.status') }}</th>
          </tr></thead>
          <tbody>
            @for (c of facade.list(); track c.id) {
              <tr class="clickable" (click)="open(c.id)">
                <td class="code">{{ c.complaintRef }}</td>
                <td>{{ c.subject }}</td>
                <td>{{ c.channel }}</td>
                <td>{{ c.complainantName }} @if (c.confidential) { <span class="conf" [title]="i18n.t('cmpl.confidential')">🔒</span> }</td>
                <td>{{ c.loggedAtUtc | date:'short' }}</td>
                <td><qams-status-pill [status]="c.status" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <!-- Record workspace: the routed detail renders in a wide drawer over the list. -->
    <qams-drawer [open]="detailOpen()" [title]="i18n.t('cmpl.title')" width="920px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
  styles: [`
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .inline { display: inline-flex; align-items: center; gap: 6px; }
    .inline input { width: auto; }
    .conf { font-size: 11px; }
    .clickable { cursor: pointer; }
    button, select { width: auto; }
  `],
})
export class ComplaintListComponent implements OnInit {
  readonly facade = inject(ComplaintsFacade);
  readonly i18n = inject(I18nService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly statuses = ['Logged', 'Acknowledged', 'Validated', 'Investigating', 'OutcomeLogged', 'Resolved', 'Closed', 'Invalid'];
  readonly channels = COMPLAINT_CHANNELS;
  readonly showForm = signal(false);
  /** Whether the record-workspace drawer (child route) is active. */
  readonly detailOpen = signal(false);
  readonly statusFilter = signal('');

  readonly form = this.fb.nonNullable.group({
    subject: ['', [Validators.required, Validators.maxLength(300)]],
    description: ['', [Validators.required, Validators.maxLength(4000)]],
    channel: ['Email' as ComplaintChannel, [Validators.required]],
    complainantName: ['', [Validators.required, Validators.maxLength(300)]],
    complainantContact: [''],
    confidential: [false],
  });

  ngOnInit(): void { void this.facade.loadList(); }

  onFilter(event: Event): void {
    this.statusFilter.set((event.target as HTMLSelectElement).value);
    void this.facade.loadList(this.statusFilter() || undefined);
  }

  async log(): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    const id = await this.facade.log({ ...raw, complainantContact: raw.complainantContact.trim() || null });
    if (id) { this.cancel(); void this.router.navigate(['/complaints', id]); }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset({ channel: 'Email', confidential: false });
  }

  open(id: string): void { void this.router.navigate(['/complaints', id]); }

  /** Dismissing the workspace drawer returns to the plain list route. */
  closeDetail(): void { void this.router.navigate(['/complaints']); }
}
