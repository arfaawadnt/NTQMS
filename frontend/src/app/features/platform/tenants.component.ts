import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { TenantsApiService } from '../../core/api/tenants-api.service';
import { I18nService } from '../../core/i18n.service';
import { Tenant } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';

/**
 * Platform administration portal (control plane): the tenant register and the
 * provisioning form. Provisioning creates the laboratory together with its
 * first tenant administrator atomically; the credentials are then handed to
 * the lab out-of-band. PlatformAdmin-only, mirroring the backend gate.
 */
@Component({
  selector: 'qams-tenants',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, PageHeaderComponent, DrawerComponent, StatusPillComponent],
  template: `
    <qams-page-header [title]="i18n.t('tenants.title')" [subtitle]="i18n.t('tenants.subtitle')">
      <button (click)="showForm.set(!showForm())">{{ i18n.t('tenants.new') }}</button>
    </qams-page-header>

    <qams-drawer [open]="showForm()" [title]="i18n.t('tenants.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="provision()">
        <div class="grid">
          <div>
            <label>{{ i18n.t('tenants.identifier') }}</label>
            <input formControlName="identifier" [placeholder]="i18n.t('tenants.identifierHint')" />
            <div class="hint">{{ i18n.t('tenants.identifierHelp') }}</div>
          </div>
          <div>
            <label>{{ i18n.t('tenants.name') }}</label>
            <input formControlName="name" />
          </div>
          <div>
            <label>{{ i18n.t('tenants.adminEmail') }}</label>
            <input formControlName="adminEmail" type="email" />
          </div>
          <div>
            <label>{{ i18n.t('tenants.adminName') }}</label>
            <input formControlName="adminDisplayName" />
          </div>
          <div>
            <label>{{ i18n.t('tenants.adminPassword') }}</label>
            <input formControlName="adminPassword" type="text" [placeholder]="i18n.t('users.passwordHint')" />
          </div>
        </div>
        <div class="row">
          <button type="submit" [disabled]="form.invalid || loading()">{{ i18n.t('tenants.provision') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (error()) { <div class="error">{{ error() }}</div> }
      </form>
    </qams-drawer>

    @if (provisioned()) {
      <div class="card ok-note">
        {{ i18n.t('tenants.provisionedNote') }} <b>{{ provisioned() }}</b>
      </div>
    }

    @if (loading() && tenants().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (tenants().length === 0) {
      <p class="muted">{{ i18n.t('tenants.empty') }}</p>
    } @else {
      @if (error() && !showForm()) { <div class="error">{{ error() }}</div> }
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('tenants.identifier') }}</th><th>{{ i18n.t('tenants.name') }}</th>
            <th>{{ i18n.t('nc.status') }}</th><th>{{ i18n.t('tenants.created') }}</th>
          </tr></thead>
          <tbody>
            @for (t of tenants(); track t.id) {
              <tr>
                <td class="code">{{ t.identifier }}</td>
                <td>{{ t.name }}</td>
                <td><qams-status-pill [status]="t.status" /></td>
                <td>{{ t.createdAtUtc | date:'medium' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: [`
    .form { margin-bottom: 1rem; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: .5rem 1rem; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .ok-note {
      margin-bottom: 1rem; color: var(--nt-green);
      background: rgba(24, 128, 56, .08); border-color: rgba(24, 128, 56, .3);
    }
    button { width: auto; }
  `],
})
export class TenantsComponent implements OnInit {
  readonly i18n = inject(I18nService);
  private readonly api = inject(TenantsApiService);
  private readonly fb = inject(FormBuilder);

  readonly tenants = signal<Tenant[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly showForm = signal(false);
  /** Identifier of the most recently provisioned tenant (confirmation note). */
  readonly provisioned = signal('');

  readonly form = this.fb.nonNullable.group({
    identifier: ['', [Validators.required, Validators.pattern(/^[a-z0-9][a-z0-9-]{1,62}$/)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    adminEmail: ['', [Validators.required, Validators.email]],
    adminDisplayName: ['', [Validators.required, Validators.maxLength(200)]],
    adminPassword: ['', [Validators.required, Validators.minLength(10)]],
  });

  ngOnInit(): void { void this.load(); }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      this.tenants.set(await firstValueFrom(this.api.list()));
    } catch (err) {
      this.error.set(this.describe(err));
    } finally {
      this.loading.set(false);
    }
  }

  async provision(): Promise<void> {
    if (this.form.invalid) { return; }
    this.loading.set(true);
    this.error.set('');
    const request = this.form.getRawValue();
    try {
      await firstValueFrom(this.api.provision(request));
      this.provisioned.set(request.identifier);
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
    this.form.reset();
  }

  private describe(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      return (err.error as { title?: string } | null)?.title ?? `Request failed (${err.status}).`;
    }
    return 'Unexpected error.';
  }
}
