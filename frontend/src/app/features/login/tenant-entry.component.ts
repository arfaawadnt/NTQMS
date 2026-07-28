import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';

/**
 * The tenant's front door: every laboratory has its own address
 * (/t/{lab-identifier}). Landing here pins the lab for this browser and
 * hands over to the sign-in page — the tenant is defined by the URL, never
 * typed into the login form.
 */
@Component({
  selector: 'qams-tenant-entry',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Eager,
  template: '',
})
export class TenantEntryComponent {
  constructor() {
    const route = inject(ActivatedRoute);
    const router = inject(Router);
    const auth = inject(AuthService);

    const slug = route.snapshot.paramMap.get('tenant');
    auth.setTenantSlug(slug);
    // A different lab's door invalidates any session from the previous lab.
    if (auth.isAuthenticated()) {
      auth.logout();
    }

    void router.navigate(['/login'], { replaceUrl: true });
  }
}
