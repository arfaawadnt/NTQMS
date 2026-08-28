import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { CredentialingFacade } from './credentialing.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { CREDENTIAL_TYPES, CredentialType } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Practitioner workspace (HQMS M13): licences with primary-source verification, privilege
 * delineation (request → grant/deny), the credential / reappoint / suspend lifecycle, and the
 * point-of-care privilege check. Credentialing needs a verified licence and a granted privilege.
 */
@Component({
    selector: 'qams-credentialing-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, RouterLink, PageHeaderComponent, StatusPillComponent, AuditTrailComponent],
    template: `
    @if (practitioner(); as p) {
      <qams-page-header [title]="p.practitionerRef + ' — ' + p.fullName">
        <a routerLink="/credentialing" class="ghost-link">← {{ i18n.t('crd.backToList') }}</a>
      </qams-page-header>

      <div class="meta card">
        <div><span class="muted">{{ i18n.t('crd.status') }}</span><qams-status-pill [status]="p.status" /></div>
        <div><span class="muted">{{ i18n.t('crd.specialty') }}</span> {{ p.specialty }}</div>
        <div><span class="muted">{{ i18n.t('crd.appointedUntil') }}</span> {{ p.appointedUntil ? (p.appointedUntil | date:'mediumDate') : '—' }}</div>
        @if (p.suspensionReason) { <div><span class="muted">{{ i18n.t('crd.suspensionReason') }}</span> <b class="danger-text">{{ p.suspensionReason }}</b></div> }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      <div class="grid">
        <!-- Licences -->
        <section class="card">
          <h3>{{ i18n.t('crd.licences') }}</h3>
          @if (p.licences.length === 0) { <p class="muted">{{ i18n.t('crd.noLicences') }}</p> }
          @for (l of p.licences; track l.id) {
            <div class="row-item">
              <div>
                <b>{{ i18n.t('crd.ct.' + l.type) }}</b> · {{ l.identifier }} <span class="muted">({{ l.issuer }})</span><br />
                <span class="muted">{{ i18n.t('crd.expiresOn') }}: {{ l.expiresOn | date:'mediumDate' }}</span>
                @if (l.expired) { <span class="tier expired">{{ i18n.t('crd.expired') }}</span> }
              </div>
              <div>
                @if (l.verificationStatus === 'Verified') {
                  <span class="verified">✓ {{ i18n.t('crd.verified') }}</span>
                  <div class="muted small">{{ l.verificationSource }}</div>
                } @else if (perms.can('credentialing.approve')) {
                  @if (verifyingLicence() === l.id) {
                    <form [formGroup]="verifyForm" (ngSubmit)="verifyLicence(p.id, l.id)">
                      <input formControlName="source" [placeholder]="i18n.t('crd.verificationSource')" />
                      <button type="submit" [disabled]="verifyForm.invalid">{{ i18n.t('crd.confirmVerify') }}</button>
                    </form>
                  } @else {
                    <button class="link" (click)="startVerify(l.id)">{{ i18n.t('crd.verify') }}</button>
                  }
                } @else { <span class="muted">{{ i18n.t('crd.unverified') }}</span> }
              </div>
            </div>
          }
          @if (p.status !== 'Suspended' && perms.can('credentialing.edit')) {
            <form class="addform" [formGroup]="licenceForm" (ngSubmit)="addLicence(p.id)">
              <h4>{{ i18n.t('crd.addLicence') }}</h4>
              <div class="grid2">
                <select formControlName="type">@for (t of credentialTypes; track t) { <option [value]="t">{{ i18n.t('crd.ct.' + t) }}</option> }</select>
                <input formControlName="identifier" [placeholder]="i18n.t('crd.identifier')" />
                <input formControlName="issuer" [placeholder]="i18n.t('crd.issuer')" />
                <input type="date" formControlName="expiresOn" />
              </div>
              <button type="submit" [disabled]="licenceForm.invalid || facade.loading()">{{ i18n.t('crd.add') }}</button>
            </form>
          }
        </section>

        <!-- Privileges -->
        <section class="card">
          <h3>{{ i18n.t('crd.privileges') }}</h3>
          @if (p.privileges.length === 0) { <p class="muted">{{ i18n.t('crd.noPrivileges') }}</p> }
          @for (pr of p.privileges; track pr.id) {
            <div class="row-item">
              <div>
                <b>{{ pr.name }}</b> <qams-status-pill [status]="pr.status" />
                @if (pr.grantedUntil) { <span class="muted small">{{ i18n.t('crd.until') }} {{ pr.grantedUntil | date:'mediumDate' }}</span> }
                @if (pr.denialReason) { <div class="muted small">{{ pr.denialReason }}</div> }
              </div>
              @if (pr.status === 'Requested' && perms.can('credentialing.approve')) {
                <div class="decide">
                  @if (decidingPrivilege() === pr.id) {
                    <form [formGroup]="grantForm" (ngSubmit)="grant(p.id, pr.id)">
                      <input type="date" formControlName="grantedUntil" />
                      <button type="submit">{{ i18n.t('crd.grant') }}</button>
                    </form>
                    <form [formGroup]="denyForm" (ngSubmit)="deny(p.id, pr.id)">
                      <input formControlName="reason" [placeholder]="i18n.t('crd.denialReason')" />
                      <button type="submit" class="secondary" [disabled]="denyForm.invalid">{{ i18n.t('crd.deny') }}</button>
                    </form>
                  } @else {
                    <button class="link" (click)="startDecide(pr.id)">{{ i18n.t('crd.decide') }}</button>
                  }
                </div>
              }
            </div>
          }
          @if (p.status !== 'Suspended' && perms.can('credentialing.edit')) {
            <form class="addform" [formGroup]="privilegeForm" (ngSubmit)="requestPrivilege(p.id)">
              <h4>{{ i18n.t('crd.requestPrivilege') }}</h4>
              <input formControlName="name" [placeholder]="i18n.t('crd.privilegeName')" />
              <button type="submit" [disabled]="privilegeForm.invalid || facade.loading()">{{ i18n.t('crd.request') }}</button>
            </form>
          }
        </section>
      </div>

      <div class="grid">
        <!-- Lifecycle -->
        <section class="card actions">
          <h3>{{ i18n.t('crd.appointment') }}</h3>
          @switch (p.status) {
            @case ('Pending') {
              @if (perms.can('credentialing.approve')) {
                <p class="muted">{{ i18n.t('crd.credentialHint') }}</p>
                <form [formGroup]="appointForm" (ngSubmit)="credential(p.id)">
                  <label>{{ i18n.t('crd.appointedUntil') }}</label>
                  <input type="date" formControlName="appointedUntil" />
                  <button type="submit" [disabled]="appointForm.invalid || facade.loading()">{{ i18n.t('crd.credential') }}</button>
                </form>
              } @else { <p class="muted">{{ i18n.t('crd.awaitCredential') }}</p> }
            }
            @case ('Credentialed') {
              @if (perms.can('credentialing.approve')) {
                <form [formGroup]="reappointForm" (ngSubmit)="reappoint(p.id)">
                  <label>{{ i18n.t('crd.reappointUntil') }}</label>
                  <input type="date" formControlName="appointedUntil" />
                  <button type="submit" [disabled]="reappointForm.invalid || facade.loading()">{{ i18n.t('crd.reappoint') }}</button>
                </form>
              }
              @if (perms.can('credentialing.void')) {
                <form [formGroup]="suspendForm" (ngSubmit)="suspend(p.id)">
                  <label>{{ i18n.t('crd.suspensionReason') }}</label>
                  <input formControlName="reason" />
                  <button type="submit" class="danger" [disabled]="suspendForm.invalid || facade.loading()">{{ i18n.t('crd.suspend') }}</button>
                </form>
              }
            }
            @case ('Suspended') {
              @if (perms.can('credentialing.edit')) {
                <button (click)="facade.reinstate(p.id)" [disabled]="facade.loading()">{{ i18n.t('crd.reinstate') }}</button>
              } @else { <p class="muted">{{ i18n.t('crd.suspended') }}</p> }
            }
          }
        </section>

        <!-- Point-of-care check -->
        <section class="card">
          <h3>{{ i18n.t('crd.pocCheck') }}</h3>
          <p class="muted">{{ i18n.t('crd.pocHint') }}</p>
          <form [formGroup]="checkForm" (ngSubmit)="verifyPrivilege(p.id)">
            <input formControlName="privilege" [placeholder]="i18n.t('crd.privilegeName')" />
            <button type="submit" [disabled]="checkForm.invalid">{{ i18n.t('crd.checkNow') }}</button>
          </form>
          @if (facade.check(); as r) {
            <div class="result" [class.ok]="r.holds" [class.no]="!r.holds">
              <b>{{ r.holds ? i18n.t('crd.holds') : i18n.t('crd.notHeld') }}</b> — {{ r.privilegeName }}
              @if (r.detail) { <div class="muted small">{{ r.detail }}</div> }
            </div>
          }
        </section>
      </div>

      <qams-audit-trail [subject]="p.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; padding: 12px 14px; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; align-items: start; margin-bottom: 1rem; }
    .row-item { display: flex; justify-content: space-between; gap: 1rem; padding: .6rem 0; border-bottom: 1px solid var(--nt-border); }
    .addform { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    .grid2 { display: grid; grid-template-columns: 1fr 1fr; gap: .5rem; margin-bottom: .5rem; }
    .actions form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    .actions button, .addform button, .decide button { width: auto; margin-top: .4rem; }
    .decide form { display: flex; gap: .4rem; margin-top: .3rem; }
    button.link { background: none; border: none; color: var(--nt-blue); cursor: pointer; padding: 0; text-decoration: underline; }
    .verified { color: var(--nt-ink-ok); font-weight: 700; }
    .small { font-size: .72rem; } .danger-text { color: var(--nt-ink-crit); }
    .tier.expired { background: color-mix(in srgb, var(--nt-ink-crit) 14%, var(--nt-surface)); color: var(--nt-ink-crit); padding: 1px 6px; border-radius: 999px; font-size: 11px; margin-inline-start: 6px; }
    .result { margin-top: .6rem; padding: .5rem .7rem; border-radius: 6px; }
    .result.ok { background: color-mix(in srgb, var(--nt-ink-ok) 14%, transparent); }
    .result.no { background: color-mix(in srgb, var(--nt-ink-crit) 12%, transparent); }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    h3 { margin-top: 0; } h4 { margin: .3rem 0; }
    @media (max-width: 800px) { .grid { grid-template-columns: 1fr; } }
  `]
})
export class CredentialingDetailComponent implements OnInit {
  readonly facade = inject(CredentialingFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound practitioner id (provided via withComponentInputBinding). */
  readonly id = input.required<string>();
  readonly practitioner = this.facade.selected;
  readonly credentialTypes = CREDENTIAL_TYPES;

  readonly verifyingLicence = signal<string | null>(null);
  readonly decidingPrivilege = signal<string | null>(null);

  readonly licenceForm = this.fb.nonNullable.group({
    type: ['MedicalLicence' as CredentialType, [Validators.required]],
    identifier: ['', [Validators.required, Validators.maxLength(100)]],
    issuer: ['', [Validators.maxLength(150)]],
    expiresOn: ['', [Validators.required]],
  });
  readonly verifyForm = this.fb.nonNullable.group({ source: ['', [Validators.required, Validators.maxLength(300)]] });
  readonly privilegeForm = this.fb.nonNullable.group({ name: ['', [Validators.required, Validators.maxLength(200)]] });
  readonly grantForm = this.fb.nonNullable.group({ grantedUntil: [''] });
  readonly denyForm = this.fb.nonNullable.group({ reason: ['', [Validators.required, Validators.maxLength(1000)]] });
  readonly appointForm = this.fb.nonNullable.group({ appointedUntil: ['', [Validators.required]] });
  readonly reappointForm = this.fb.nonNullable.group({ appointedUntil: ['', [Validators.required]] });
  readonly suspendForm = this.fb.nonNullable.group({ reason: ['', [Validators.required, Validators.maxLength(1000)]] });
  readonly checkForm = this.fb.nonNullable.group({ privilege: ['', [Validators.required]] });

  ngOnInit(): void {
    void this.facade.loadPractitioner(this.id());
  }

  startVerify(licenceId: string): void { this.verifyForm.reset({ source: '' }); this.verifyingLicence.set(licenceId); }
  startDecide(privilegeId: string): void { this.grantForm.reset({ grantedUntil: '' }); this.denyForm.reset({ reason: '' }); this.decidingPrivilege.set(privilegeId); }

  async addLicence(id: string): Promise<void> {
    if (this.licenceForm.invalid) { return; }
    const raw = this.licenceForm.getRawValue();
    await this.facade.addLicence(id, { type: raw.type, identifier: raw.identifier, issuer: raw.issuer, expiresOn: raw.expiresOn });
    if (this.facade.error() === '') { this.licenceForm.reset({ type: 'MedicalLicence', identifier: '', issuer: '', expiresOn: '' }); }
  }

  async verifyLicence(id: string, licenceId: string): Promise<void> {
    if (this.verifyForm.invalid) { return; }
    await this.facade.verifyLicence(id, licenceId, this.verifyForm.getRawValue());
    if (this.facade.error() === '') { this.verifyingLicence.set(null); }
  }

  async requestPrivilege(id: string): Promise<void> {
    if (this.privilegeForm.invalid) { return; }
    await this.facade.requestPrivilege(id, this.privilegeForm.getRawValue());
    if (this.facade.error() === '') { this.privilegeForm.reset({ name: '' }); }
  }

  async grant(id: string, privilegeId: string): Promise<void> {
    const raw = this.grantForm.getRawValue();
    await this.facade.grantPrivilege(id, privilegeId, { grantedUntil: raw.grantedUntil || null });
    if (this.facade.error() === '') { this.decidingPrivilege.set(null); }
  }

  async deny(id: string, privilegeId: string): Promise<void> {
    if (this.denyForm.invalid) { return; }
    await this.facade.denyPrivilege(id, privilegeId, this.denyForm.getRawValue());
    if (this.facade.error() === '') { this.decidingPrivilege.set(null); }
  }

  async credential(id: string): Promise<void> {
    if (this.appointForm.invalid) { return; }
    await this.facade.credential(id, this.appointForm.getRawValue());
  }

  async reappoint(id: string): Promise<void> {
    if (this.reappointForm.invalid) { return; }
    await this.facade.reappoint(id, this.reappointForm.getRawValue());
  }

  async suspend(id: string): Promise<void> {
    if (this.suspendForm.invalid) { return; }
    await this.facade.suspend(id, this.suspendForm.getRawValue());
  }

  async verifyPrivilege(id: string): Promise<void> {
    if (this.checkForm.invalid) { return; }
    await this.facade.verifyPrivilege(id, this.checkForm.getRawValue().privilege);
  }
}
