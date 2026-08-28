import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { CredentialingFacade } from './credentialing.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PRACTITIONER_STATUSES } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';

/**
 * Credentialing register (HQMS M13): the practitioner roster with a register drawer and
 * drawer-detail, a summary strip, and the tiered licence-expiry register so lapses are chased
 * before they occur.
 */
@Component({
    selector: 'qams-credentialing-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, ListStatsComponent],
    template: `
    <qams-page-header [title]="i18n.t('crd.title')">
      @if (perms.can('credentialing.create')) {
        <button (click)="form.reset({ fullName: '', specialty: '' }); showForm.set(true)">{{ i18n.t('crd.register') }}</button>
      }
    </qams-page-header>

    <qams-list-stats [stats]="stats()" />

    <div class="filterbar card">
      <select [value]="statusFilter()" (change)="onFilter($event)" aria-label="Status filter">
        <option value="">{{ i18n.t('crd.allStatuses') }}</option>
        @for (s of statuses; track s) { <option [value]="s">{{ i18n.t('crd.ps.' + s) }}</option> }
      </select>
    </div>

    <qams-drawer [open]="showForm()" [title]="i18n.t('crd.register')" (closed)="showForm.set(false)">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="create()">
        <label>{{ i18n.t('crd.fullName') }}</label>
        <input formControlName="fullName" />
        <label>{{ i18n.t('crd.specialty') }}</label>
        <input formControlName="specialty" />
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('crd.save') }}</button>
          <button type="button" class="secondary" (click)="showForm.set(false)">{{ i18n.t('common.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    <h3>{{ i18n.t('crd.practitioners') }}</h3>
    @if (facade.loading() && facade.practitioners().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.practitioners().length === 0) {
      <p class="muted">{{ i18n.t('crd.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('crd.ref') }}</th><th>{{ i18n.t('crd.fullName') }}</th><th>{{ i18n.t('crd.specialty') }}</th>
            <th>{{ i18n.t('crd.verifiedLicences') }}</th><th>{{ i18n.t('crd.grantedPrivileges') }}</th>
            <th>{{ i18n.t('crd.appointedUntil') }}</th><th>{{ i18n.t('crd.status') }}</th>
          </tr></thead>
          <tbody>
            @for (p of facade.practitioners(); track p.id) {
              <tr class="clickable" (click)="open(p.id)">
                <td class="code">{{ p.practitionerRef }}</td>
                <td>{{ p.fullName }}</td>
                <td>{{ p.specialty }}</td>
                <td>{{ p.verifiedLicences }}</td>
                <td>{{ p.grantedPrivileges }}</td>
                <td>{{ p.appointedUntil ? (p.appointedUntil | date:'mediumDate') : '—' }}</td>
                <td><qams-status-pill [status]="p.status" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <h3>{{ i18n.t('crd.expiryRegister') }}</h3>
    @if (facade.expiring().length === 0) {
      <p class="muted">{{ i18n.t('crd.noExpiring') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('crd.practitioner') }}</th><th>{{ i18n.t('crd.licenceType') }}</th><th>{{ i18n.t('crd.identifier') }}</th>
            <th>{{ i18n.t('crd.expiresOn') }}</th><th>{{ i18n.t('crd.daysToExpiry') }}</th><th>{{ i18n.t('crd.tier') }}</th>
          </tr></thead>
          <tbody>
            @for (e of facade.expiring(); track e.licenceId) {
              <tr class="clickable" (click)="open(e.practitionerId)">
                <td>{{ e.fullName }}</td>
                <td>{{ i18n.t('crd.ct.' + e.type) }}</td>
                <td class="code">{{ e.identifier }}</td>
                <td>{{ e.expiresOn | date:'mediumDate' }}</td>
                <td>{{ e.daysToExpiry }}</td>
                <td><span class="tier" [class]="'tier ' + e.tier.toLowerCase()">{{ i18n.t('crd.tier.' + e.tier) }}</span></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <qams-drawer [open]="detailOpen()" [title]="i18n.t('crd.title')" width="900px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .filterbar { display: flex; gap: 10px; padding: 10px 14px; margin-bottom: 14px; flex-wrap: wrap; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    h3 { margin: 1.4rem 0 .6rem; }
    select, button { width: auto; }
    .tier { padding: 2px 8px; border-radius: 999px; font-size: 11.5px; font-weight: 700; }
    .tier.expired { background: var(--nt-ink-crit); color: #fff; }
    .tier.critical { background: var(--nt-ink-serious); color: #fff; }
    .tier.warning { background: var(--nt-ink-warn); color: #3a2d00; }
    .tier.ok { background: color-mix(in srgb, var(--nt-slate) 18%, transparent); color: var(--nt-slate); }
  `]
})
export class CredentialingListComponent implements OnInit {
  readonly facade = inject(CredentialingFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly statuses = PRACTITIONER_STATUSES;
  readonly showForm = signal(false);
  readonly detailOpen = signal(false);
  readonly statusFilter = signal('');

  readonly stats = computed<ListStat[]>(() => [
    { label: this.i18n.t('crd.stat.practitioners'), value: this.facade.practitioners().length, tone: 'slate' },
    { label: this.i18n.t('crd.stat.credentialed'), value: this.facade.credentialedCount(), tone: 'teal' },
    { label: this.i18n.t('crd.stat.expiring'), value: this.facade.expiring().length, tone: 'orange' },
    { label: this.i18n.t('crd.stat.expired'), value: this.facade.expiredCount(), tone: 'red' },
  ]);

  readonly form = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(200)]],
    specialty: ['', [Validators.required, Validators.maxLength(150)]],
  });

  ngOnInit(): void {
    void this.facade.loadAll();
  }

  onFilter(event: Event): void {
    this.statusFilter.set((event.target as HTMLSelectElement).value);
    void this.facade.loadAll(undefined, this.statusFilter() || undefined);
  }

  async create(): Promise<void> {
    if (this.form.invalid) { return; }
    const id = await this.facade.register(this.form.getRawValue());
    if (id) {
      this.showForm.set(false);
      void this.facade.loadAll(undefined, this.statusFilter() || undefined);
      void this.router.navigate(['/credentialing', id]);
    }
  }

  open(id: string): void { void this.router.navigate(['/credentialing', id]); }
  closeDetail(): void { void this.router.navigate(['/credentialing']); }
}
