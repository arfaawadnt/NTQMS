import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ReferenceApiService } from './api/reference-api.service';
import { UsersApiService } from './api/users-api.service';
import { Branch, Department, LovEntry, UserDirectoryEntry } from './models';
import { I18nService } from './i18n.service';

/**
 * Session-scoped caches for the organizational reference data the shared
 * controls need everywhere: the user directory, branches, departments, and
 * LOV entries per category. Each loads once per session on first demand.
 */
@Injectable({ providedIn: 'root' })
export class OrgDataService {
  private readonly users = inject(UsersApiService);
  private readonly reference = inject(ReferenceApiService);
  private readonly i18n = inject(I18nService);

  private readonly _directory = signal<UserDirectoryEntry[]>([]);
  private readonly _branches = signal<Branch[]>([]);
  private readonly _departments = signal<Department[]>([]);
  private readonly _lovs = signal<Map<string, LovEntry[]>>(new Map());
  private directoryLoaded = false;
  private orgLoaded = false;

  readonly directory = this._directory.asReadonly();
  readonly branches = this._branches.asReadonly();
  readonly departments = this._departments.asReadonly();

  async ensureDirectory(): Promise<void> {
    if (this.directoryLoaded) { return; }
    this.directoryLoaded = true;
    try {
      this._directory.set(await firstValueFrom(this.users.directory()));
    } catch {
      this.directoryLoaded = false; // Retry on next demand.
    }
  }

  async ensureOrg(): Promise<void> {
    if (this.orgLoaded) { return; }
    this.orgLoaded = true;
    try {
      const [branches, departments] = await Promise.all([
        firstValueFrom(this.reference.branches()),
        firstValueFrom(this.reference.departments()),
      ]);
      this._branches.set(branches.filter((b) => b.isActive));
      this._departments.set(departments.filter((d) => d.isActive));
    } catch {
      this.orgLoaded = false;
    }
  }

  /** LOV entries for a category, cached; empty array when none are configured. */
  async lovEntries(category: string): Promise<LovEntry[]> {
    const cached = this._lovs().get(category);
    if (cached) { return cached; }
    try {
      const entries = (await firstValueFrom(this.reference.lovs(category)))
        .filter((l) => l.isActive)
        .sort((a, b) => a.sortOrder - b.sortOrder);
      this._lovs.update((m) => new Map(m).set(category, entries));
      return entries;
    } catch {
      return [];
    }
  }

  /** Display name for a user id ('' when unknown). */
  userName(id: string | null): string {
    if (!id) { return ''; }
    return this._directory().find((u) => u.id === id)?.displayName ?? '';
  }

  /** Branch label for an id ('' when unallocated/unknown). */
  branchName(id: string | null): string {
    if (!id) { return ''; }
    const b = this._branches().find((x) => x.id === id);
    return b ? b.code : '';
  }

  /** Department label for an id ('' when unallocated/unknown). */
  departmentName(id: string | null): string {
    if (!id) { return ''; }
    const d = this._departments().find((x) => x.id === id);
    return d ? d.code : '';
  }

  /** Localized LOV display name per the active language, falling back to English. */
  lovName(entry: LovEntry): string {
    switch (this.i18n.lang()) {
      case 'ar': return entry.nameAr ?? entry.nameEn;
      case 'fr': return entry.nameFr ?? entry.nameEn;
      default: return entry.nameEn;
    }
  }
}
