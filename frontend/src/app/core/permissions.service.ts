import { Injectable, computed, inject } from '@angular/core';
import { AuthService } from './auth.service';

/**
 * Mirrors the backend's role-based `[Authorize(Roles=…)]` gates so the UI can
 * hide actions the current user cannot perform. Authoritative enforcement stays
 * on the server; this is purely for affordance (never a security boundary).
 */
@Injectable({ providedIn: 'root' })
export class PermissionsService {
  private readonly auth = inject(AuthService);

  /** True for Quality Managers and Tenant Administrators (the approval roles). */
  readonly canApprove = computed(() => this.isInRole('QualityManager', 'TenantAdmin'));

  /** True for administrators managing configuration (tenant admins). */
  readonly isTenantAdmin = computed(() => this.isInRole('TenantAdmin'));

  /** True for platform (cross-tenant) administrators. */
  readonly isPlatformAdmin = computed(() => this.isInRole('PlatformAdmin'));

  private isInRole(...roles: readonly string[]): boolean {
    return roles.includes(this.auth.role());
  }
}
