import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { NotificationsApiService } from '../../core/api/notifications-api.service';
import { I18nService } from '../../core/i18n.service';
import {
  DispatchMonitorItem, NOTIFICATION_EVENT_KEYS, NotificationRule, TENANT_ROLES,
} from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';

/**
 * Notification administration (QM/TenantAdmin): rules mapping engine event
 * keys to recipient roles and templates ({{token}} placeholders filled from
 * the event payload), and the dispatch monitor showing queued/sent/failed
 * emails with their errors.
 */
@Component({
  selector: 'qams-notification-admin',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, PageHeaderComponent, DrawerComponent, StatusPillComponent],
  template: `
    <qams-page-header [title]="i18n.t('nrule.title')" [subtitle]="i18n.t('nrule.subtitle')">
      <button (click)="showForm.set(!showForm())">{{ i18n.t('nrule.new') }}</button>
    </qams-page-header>

    <qams-drawer [open]="showForm()" [title]="i18n.t('nrule.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="upsert()">
        <label>{{ i18n.t('nrule.eventKey') }}</label>
        <select formControlName="eventKey">
          @for (k of eventKeys; track k) { <option [value]="k">{{ k }}</option> }
        </select>
        <label>{{ i18n.t('nrule.recipients') }}</label>
        <div class="roles">
          @for (r of roles; track r) {
            <label class="inline">
              <input type="checkbox" [checked]="selectedRoles().includes(r)" (change)="toggleRole(r)" /> {{ r }}
            </label>
          }
        </div>
        <label class="inline email">
          <input type="checkbox" formControlName="emailEnabled" /> {{ i18n.t('nrule.emailEnabled') }}
        </label>
        <label>{{ i18n.t('nrule.subjectTemplate') }}</label>
        <input formControlName="subjectTemplate" [placeholder]="i18n.t('nrule.templateHint')" />
        <label>{{ i18n.t('nrule.bodyTemplate') }}</label>
        <textarea formControlName="bodyTemplate" rows="4" [placeholder]="i18n.t('nrule.templateHint')"></textarea>
        <div class="row">
          <button type="submit" [disabled]="form.invalid || selectedRoles().length === 0 || loading()">{{ i18n.t('nrule.save') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (error()) { <div class="error">{{ error() }}</div> }
      </form>
    </qams-drawer>

    <section class="card">
      <h3>{{ i18n.t('nrule.rulesHeading') }}</h3>
      @if (rules().length === 0) {
        <p class="muted">{{ i18n.t('nrule.noRules') }}</p>
      } @else {
        <table>
          <thead><tr>
            <th>{{ i18n.t('nrule.eventKey') }}</th><th>{{ i18n.t('nrule.recipients') }}</th>
            <th>{{ i18n.t('nrule.email') }}</th><th>{{ i18n.t('nrule.subjectTemplate') }}</th><th>{{ i18n.t('nc.status') }}</th>
          </tr></thead>
          <tbody>
            @for (r of rules(); track r.id) {
              <tr class="clickable" (click)="edit(r)">
                <td class="code">{{ r.eventKey }}</td>
                <td>{{ r.recipientRoles }}</td>
                <td>{{ r.emailEnabled ? i18n.t('common.yes') : i18n.t('common.no') }}</td>
                <td>{{ r.subjectTemplate }}</td>
                <td><qams-status-pill [status]="r.isActive ? 'Active' : 'Obsolete'" /></td>
              </tr>
            }
          </tbody>
        </table>
      }
    </section>

    <section class="card">
      <h3>{{ i18n.t('nrule.monitorHeading') }}</h3>
      <div class="filter">
        <select [value]="statusFilter()" (change)="onFilter($event)" aria-label="Status filter">
          <option value="">{{ i18n.t('nc.allStatuses') }}</option>
          @for (s of dispatchStatuses; track s) { <option [value]="s">{{ s }}</option> }
        </select>
      </div>
      @if (monitor().length === 0) {
        <p class="muted">{{ i18n.t('nrule.noDispatches') }}</p>
      } @else {
        <table>
          <thead><tr>
            <th>{{ i18n.t('nrule.when') }}</th><th>{{ i18n.t('nrule.eventKey') }}</th>
            <th>{{ i18n.t('nrule.recipient') }}</th><th>{{ i18n.t('nrule.subject') }}</th>
            <th>{{ i18n.t('nc.status') }}</th><th>{{ i18n.t('nrule.error') }}</th>
          </tr></thead>
          <tbody>
            @for (d of monitor(); track d.id) {
              <tr>
                <td>{{ d.createdAtUtc | date:'short' }}</td>
                <td class="code">{{ d.eventKey }}</td>
                <td>{{ d.recipientEmail ?? d.recipientUserId }}</td>
                <td>{{ d.subject }}</td>
                <td><qams-status-pill [status]="d.emailStatus" /></td>
                <td class="err-cell">{{ d.error ?? '—' }}</td>
              </tr>
            }
          </tbody>
        </table>
      }
    </section>
  `,
  styles: [`
    section { margin-bottom: 1rem; }
    .roles { display: flex; flex-wrap: wrap; gap: 12px; margin: 4px 0; }
    .inline { display: inline-flex; align-items: center; gap: 6px; margin: 0; font-weight: 400; }
    .inline input { width: auto; }
    .email { margin-top: 12px; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .filter { margin-bottom: 10px; }
    .clickable { cursor: pointer; }
    .err-cell { max-width: 260px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; color: var(--nt-red); }
    button, select { width: auto; }
  `],
})
export class NotificationAdminComponent implements OnInit {
  readonly i18n = inject(I18nService);
  private readonly api = inject(NotificationsApiService);
  private readonly fb = inject(FormBuilder);

  readonly eventKeys = NOTIFICATION_EVENT_KEYS;
  readonly roles = TENANT_ROLES;
  readonly dispatchStatuses = ['Queued', 'Sent', 'Failed'];

  readonly rules = signal<NotificationRule[]>([]);
  readonly monitor = signal<DispatchMonitorItem[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly showForm = signal(false);
  readonly statusFilter = signal('');
  readonly selectedRoles = signal<string[]>([]);

  readonly form = this.fb.nonNullable.group({
    eventKey: ['NC_RAISED', [Validators.required]],
    emailEnabled: [true],
    subjectTemplate: ['', [Validators.required, Validators.maxLength(300)]],
    bodyTemplate: ['', [Validators.required, Validators.maxLength(4000)]],
  });

  ngOnInit(): void { void this.load(); }

  toggleRole(role: string): void {
    const current = this.selectedRoles();
    this.selectedRoles.set(current.includes(role) ? current.filter((r) => r !== role) : [...current, role]);
  }

  /** Pre-fills the drawer from an existing rule (upsert is keyed by event key). */
  edit(rule: NotificationRule): void {
    this.form.setValue({
      eventKey: rule.eventKey,
      emailEnabled: rule.emailEnabled,
      subjectTemplate: rule.subjectTemplate,
      bodyTemplate: rule.bodyTemplate,
    });
    this.selectedRoles.set(rule.recipientRoles.split(',').map((r) => r.trim()).filter(Boolean));
    this.showForm.set(true);
  }

  onFilter(event: Event): void {
    this.statusFilter.set((event.target as HTMLSelectElement).value);
    void this.loadMonitor();
  }

  async upsert(): Promise<void> {
    if (this.form.invalid || this.selectedRoles().length === 0) { return; }
    this.loading.set(true);
    this.error.set('');
    try {
      await firstValueFrom(this.api.upsertRule({
        ...this.form.getRawValue(),
        recipientRoles: this.selectedRoles().join(','),
      }));
      this.cancel();
      await this.load();
    } catch (err) {
      this.error.set(this.describe(err));
    } finally {
      this.loading.set(false);
    }
  }

  cancel(): void {
    this.showForm.set(false);
    this.selectedRoles.set([]);
    this.form.reset({ eventKey: 'NC_RAISED', emailEnabled: true });
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      this.rules.set(await firstValueFrom(this.api.rules()));
      await this.loadMonitor();
    } catch (err) {
      this.error.set(this.describe(err));
    } finally {
      this.loading.set(false);
    }
  }

  private async loadMonitor(): Promise<void> {
    this.monitor.set((await firstValueFrom(this.api.monitor(this.statusFilter() || undefined))).items);
  }

  private describe(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      return (err.error as { title?: string } | null)?.title ?? `Request failed (${err.status}).`;
    }
    return 'Unexpected error.';
  }
}
