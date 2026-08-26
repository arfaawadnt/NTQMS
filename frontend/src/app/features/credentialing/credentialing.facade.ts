import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { CredentialingApiService } from '../../core/api/credentialing-api.service';
import {
  AddLicenceRequest, CredentialRequest, DenyPrivilegeRequest, ExpiringCredential, GrantPrivilegeRequest,
  PractitionerDetail, PractitionerListItem, PrivilegeCheckResult, RegisterPractitionerRequest,
  RequestPrivilegeRequest, SuspendPractitionerRequest, VerifyLicenceRequest,
} from '../../core/models';

/**
 * Signal-based facade for Credentialing & Privileging (HQMS M13). Holds the practitioner register,
 * the licence-expiry register, the selected practitioner and the last point-of-care check result;
 * refreshes the loaded practitioner after each write.
 */
@Injectable({ providedIn: 'root' })
export class CredentialingFacade {
  private readonly api = inject(CredentialingApiService);

  private readonly _practitioners = signal<PractitionerListItem[]>([]);
  private readonly _expiring = signal<ExpiringCredential[]>([]);
  private readonly _selected = signal<PractitionerDetail | null>(null);
  private readonly _check = signal<PrivilegeCheckResult | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly practitioners = this._practitioners.asReadonly();
  readonly expiring = this._expiring.asReadonly();
  readonly selected = this._selected.asReadonly();
  readonly check = this._check.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  private lastSpecialty?: string;
  private lastStatus?: string;

  readonly credentialedCount = computed(() => this._practitioners().filter((p) => p.status === 'Credentialed').length);
  readonly expiredCount = computed(() => this._expiring().filter((e) => e.tier === 'Expired').length);

  async loadAll(specialty?: string, status?: string): Promise<void> {
    this.lastSpecialty = specialty;
    this.lastStatus = status;
    await this.run(async () => {
      this._practitioners.set(await firstValueFrom(this.api.list(specialty, status)));
      this._expiring.set(await firstValueFrom(this.api.expiring(90)));
    });
  }

  async loadPractitioner(id: string): Promise<void> {
    await this.run(async () => this._selected.set(await firstValueFrom(this.api.getById(id))));
  }

  async register(r: RegisterPractitionerRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.register(r))).id);
  }

  async verifyPrivilege(id: string, privilege: string): Promise<void> {
    await this.run(async () => this._check.set(await firstValueFrom(this.api.verifyPrivilege(id, privilege))));
  }

  clearCheck(): void { this._check.set(null); }

  async addLicence(id: string, r: AddLicenceRequest): Promise<void> { await this.mutate(id, () => this.api.addLicence(id, r)); }
  async verifyLicence(id: string, licenceId: string, r: VerifyLicenceRequest): Promise<void> { await this.mutate(id, () => this.api.verifyLicence(id, licenceId, r)); }
  async requestPrivilege(id: string, r: RequestPrivilegeRequest): Promise<void> { await this.mutate(id, () => this.api.requestPrivilege(id, r)); }
  async grantPrivilege(id: string, privilegeId: string, r: GrantPrivilegeRequest): Promise<void> { await this.mutate(id, () => this.api.grantPrivilege(id, privilegeId, r)); }
  async denyPrivilege(id: string, privilegeId: string, r: DenyPrivilegeRequest): Promise<void> { await this.mutate(id, () => this.api.denyPrivilege(id, privilegeId, r)); }
  async credential(id: string, r: CredentialRequest): Promise<void> { await this.mutate(id, () => this.api.credential(id, r)); }
  async reappoint(id: string, r: CredentialRequest): Promise<void> { await this.mutate(id, () => this.api.reappoint(id, r)); }
  async suspend(id: string, r: SuspendPractitionerRequest): Promise<void> { await this.mutate(id, () => this.api.suspend(id, r)); }
  async reinstate(id: string): Promise<void> { await this.mutate(id, () => this.api.reinstate(id)); }

  private async mutate<T>(id: string, call: () => Observable<T>): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(call());
      this._selected.set(await firstValueFrom(this.api.getById(id)));
    });
    if (this._error() === '') {
      this._practitioners.set(await firstValueFrom(this.api.list(this.lastSpecialty, this.lastStatus)));
      this._expiring.set(await firstValueFrom(this.api.expiring(90)));
    }
  }

  private async run<T>(operation: () => Promise<T>): Promise<T | null> {
    this._loading.set(true);
    this._error.set('');
    try {
      return await operation();
    } catch (err) {
      this._error.set(this.describe(err));
      return null;
    } finally {
      this._loading.set(false);
    }
  }

  private describe(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      return (err.error as { title?: string } | null)?.title ?? `Request failed (${err.status}).`;
    }
    return 'Unexpected error.';
  }
}
