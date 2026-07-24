import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { PermissionsService } from './permissions.service';

/**
 * Route guards mirroring the backend's role split: platform administrators
 * live on the control plane (/platform/*), lab users on the tenant modules.
 * Each guard redirects the other audience to its own home instead of letting
 * a screen load whose API calls would only 403.
 */
export const platformOnlyGuard: CanActivateFn = () => {
  const perms = inject(PermissionsService);
  const router = inject(Router);
  return perms.isPlatformAdmin() ? true : router.createUrlTree(['/dashboard']);
};

export const tenantOnlyGuard: CanActivateFn = () => {
  const perms = inject(PermissionsService);
  const router = inject(Router);
  return perms.isPlatformAdmin() ? router.createUrlTree(['/platform/tenants']) : true;
};
