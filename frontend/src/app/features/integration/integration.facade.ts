import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { IntegrationApiService } from '../../core/api/integration-api.service';
import { EndpointListItem, IntegrationMessage, PatientCensus, RegisterEndpointRequest } from '../../core/models';

/**
 * Signal-based facade for Integration & Interoperability monitoring (HQMS M24). Holds the
 * endpoint health list, the live census projection, and the messages of the expanded endpoint.
 */
@Injectable({ providedIn: 'root' })
export class IntegrationFacade {
  private readonly api = inject(IntegrationApiService);

  private readonly _endpoints = signal<EndpointListItem[]>([]);
  private readonly _census = signal<PatientCensus | null>(null);
  private readonly _messages = signal<IntegrationMessage[]>([]);
  private readonly _expandedId = signal<string | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly endpoints = this._endpoints.asReadonly();
  readonly census = this._census.asReadonly();
  readonly messages = this._messages.asReadonly();
  readonly expandedId = this._expandedId.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  readonly unhealthyCount = computed(() => this._endpoints().filter((e) => !e.healthy).length);
  readonly failedCount = computed(() => this._endpoints().reduce((n, e) => n + e.failed, 0));

  async load(): Promise<void> {
    await this.run(async () => {
      this._endpoints.set(await firstValueFrom(this.api.endpoints()));
      this._census.set(await firstValueFrom(this.api.census(30)));
    });
  }

  /** Toggles the expanded endpoint and loads its messages. */
  async toggleMessages(id: string): Promise<void> {
    if (this._expandedId() === id) { this._expandedId.set(null); return; }
    this._expandedId.set(id);
    const msgs = await this.run(() => firstValueFrom(this.api.messages(id)));
    if (msgs) { this._messages.set(msgs); }
  }

  async register(request: RegisterEndpointRequest): Promise<string | null> {
    return this.run(async () => {
      const id = (await firstValueFrom(this.api.register(request))).id;
      this._endpoints.set(await firstValueFrom(this.api.endpoints()));
      return id;
    });
  }

  async suspend(id: string): Promise<void> { await this.refresh(() => this.api.suspend(id)); }
  async resume(id: string): Promise<void> { await this.refresh(() => this.api.resume(id)); }

  private async refresh<T>(call: () => Observable<T>): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(call());
      this._endpoints.set(await firstValueFrom(this.api.endpoints()));
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
