import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { I18nService } from '../../core/i18n.service';
import { NotificationsApiService } from '../../core/api/notifications-api.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';

/**
 * Mail Management: the tenant's mail <em>sender identity</em> and HTML branding for
 * Mail-type notifications — the From name/address, an optional reply-to, an on/off
 * switch, and the brand accent and footer used by the HTML e-mail template. It does
 * NOT collect SMTP transport credentials: the relay stays in secured server
 * configuration, so no reversible secret is entered or stored here. Admin-gated
 * (notifications.manage); the server re-enforces.
 */
@Component({
  selector: 'qams-mail-management',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, PageHeaderComponent],
  template: `
    <qams-page-header [title]="i18n.t('mail.title')" [subtitle]="i18n.t('mail.subtitle')" />

    @if (loading()) { <p class="muted">{{ i18n.t('common.loading') }}</p> }
    @else {
      @if (!configured()) { <p class="notice">{{ i18n.t('mail.notConfigured') }}</p> }

      <form class="card form" [formGroup]="form" (ngSubmit)="save()">
        <div class="grid">
          <div>
            <label>{{ i18n.t('mail.fromName') }}</label>
            <input formControlName="fromName" [placeholder]="i18n.t('mail.fromNameHint')" />
          </div>
          <div>
            <label>{{ i18n.t('mail.fromAddress') }}</label>
            <input formControlName="fromAddress" type="email" placeholder="quality@lab.example" />
          </div>
          <div>
            <label>{{ i18n.t('mail.replyTo') }}</label>
            <input formControlName="replyTo" type="email" [placeholder]="i18n.t('common.optional')" />
          </div>
          <div>
            <label>{{ i18n.t('mail.brandColor') }}</label>
            <input formControlName="brandColor" [placeholder]="'#1E3A5F'" maxlength="7" />
          </div>
        </div>

        <label>{{ i18n.t('mail.footerNote') }}</label>
        <textarea rows="2" formControlName="footerNote" [placeholder]="i18n.t('common.optional')"></textarea>

        <label class="check">
          <input type="checkbox" formControlName="enabled" />
          {{ i18n.t('mail.enabled') }}
        </label>
        <p class="helper">{{ i18n.t('mail.transportNote') }}</p>

        @if (error()) { <div class="error">{{ error() }}</div> }
        @if (saved()) { <div class="ok">{{ i18n.t('mail.saved') }}</div> }

        <div class="row">
          <button type="submit" [disabled]="form.invalid || saving()">
            {{ saving() ? i18n.t('common.saving') : i18n.t('mail.save') }}
          </button>
        </div>
      </form>
    }
  `,
  styles: [`
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    .form { padding: 18px 20px; }
    .check { display: flex; align-items: center; gap: 8px; margin-top: 12px; font-weight: 600; }
    .check input { width: auto; }
    .row { margin-top: 14px; }
    button { width: auto; }
    .ok { color: var(--nt-ink-ok); font-weight: 600; margin-top: 8px; }
    .notice { color: var(--nt-ink-warn); }
    @media (max-width: 700px) { .grid { grid-template-columns: 1fr; } }
  `],
})
export class MailManagementComponent implements OnInit {
  readonly i18n = inject(I18nService);
  private readonly api = inject(NotificationsApiService);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly saved = signal(false);
  readonly error = signal('');
  readonly configured = signal(false);

  readonly form = this.fb.nonNullable.group({
    fromName: ['', [Validators.required, Validators.maxLength(150)]],
    fromAddress: ['', [Validators.required, Validators.email, Validators.maxLength(320)]],
    replyTo: ['', [Validators.maxLength(320)]],
    enabled: [true],
    brandColor: ['', [Validators.pattern(/^#[0-9A-Fa-f]{6}$/)]],
    footerNote: ['', [Validators.maxLength(500)]],
  });

  async ngOnInit(): Promise<void> {
    try {
      const s = await firstValueFrom(this.api.mailSettings());
      this.configured.set(s.configured);
      this.form.patchValue({
        fromName: s.fromName, fromAddress: s.fromAddress, replyTo: s.replyTo ?? '',
        enabled: s.enabled, brandColor: s.brandColor ?? '', footerNote: s.footerNote ?? '',
      });
    } catch (err) {
      this.error.set(this.describe(err));
    } finally {
      this.loading.set(false);
    }
  }

  async save(): Promise<void> {
    if (this.form.invalid) { return; }
    this.saving.set(true);
    this.saved.set(false);
    this.error.set('');
    const raw = this.form.getRawValue();
    try {
      await firstValueFrom(this.api.updateMailSettings({
        fromName: raw.fromName.trim(),
        fromAddress: raw.fromAddress.trim(),
        replyTo: raw.replyTo.trim() || null,
        enabled: raw.enabled,
        brandColor: raw.brandColor.trim() || null,
        footerNote: raw.footerNote.trim() || null,
      }));
      this.configured.set(true);
      this.saved.set(true);
    } catch (err) {
      this.error.set(this.describe(err));
    } finally {
      this.saving.set(false);
    }
  }

  private describe(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      return (err.error as { title?: string } | null)?.title ?? `Request failed (${err.status}).`;
    }
    return 'Unexpected error.';
  }
}
