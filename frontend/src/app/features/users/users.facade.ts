import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { UsersApiService } from '../../core/api/users-api.service';
import { ChangeUserRoleRequest, RegisterUserRequest, ResetUserPasswordRequest, UserAccount } from '../../core/models';

/** Signal-based facade for tenant user administration. */
@Injectable({ providedIn: 'root' })
export class UsersFacade {
  private readonly api = inject(UsersApiService);

  private readonly _users = signal<UserAccount[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly users = this._users.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  async load(): Promise<void> {
    await this.run(async () => this._users.set(await firstValueFrom(this.api.list())));
  }

  async register(request: RegisterUserRequest): Promise<boolean> {
    const result = await this.run(async () => {
      await firstValueFrom(this.api.register(request));
      this._users.set(await firstValueFrom(this.api.list()));
      return true;
    });
    return result ?? false;
  }

  async changeRole(id: string, request: ChangeUserRoleRequest): Promise<void> { await this.mutate(() => this.api.changeRole(id, request)); }
  async deactivate(id: string): Promise<void> { await this.mutate(() => this.api.deactivate(id)); }
  async reactivate(id: string): Promise<void> { await this.mutate(() => this.api.reactivate(id)); }
  async resetPassword(id: string, request: ResetUserPasswordRequest): Promise<void> { await this.mutate(() => this.api.resetPassword(id, request)); }

  private async mutate(call: () => Observable<void>): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(call());
      this._users.set(await firstValueFrom(this.api.list()));
    });
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
