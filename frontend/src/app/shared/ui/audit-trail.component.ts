import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { ComplianceApiService } from '../../core/api/compliance-api.service';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { AuditTrailEntry, FieldChange } from '../../core/models';

/**
 * Per-record audit trail: the tamper-evident ledger entries whose payload
 * references the record (filtered server-side by the record id). Rendered as
 * the design system's timeline. The ledger is readable by Quality Managers,
 * Tenant Admins and External Auditors only — other roles see a notice instead
 * of a broken call.
 */
@Component({
    selector: 'qams-audit-trail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [DatePipe],
    template: `
    <section class="card">
      <h3>{{ i18n.t('trail.title') }}</h3>
      @if (!perms.canViewCompliance()) {
        <p class="muted">{{ i18n.t('trail.restricted') }}</p>
      } @else if (loading()) {
        <p class="muted">{{ i18n.t('common.loading') }}</p>
      } @else if (error()) {
        <div class="error">{{ error() }}</div>
      } @else if (entries().length === 0 && changes().length === 0) {
        <p class="muted">{{ i18n.t('trail.empty') }}</p>
      } @else {
        <ol class="timeline">
          @for (e of entries(); track e.id) {
            <li>
              <span class="marker"></span>
              <div class="entry">
                <div class="head">
                  <b>{{ pretty(e.eventType) }}</b>
                  <span class="muted">{{ e.occurredAtUtc | date:'medium' }} · #{{ e.sequence }}</span>
                </div>
                <button type="button" class="link" (click)="toggle(e.id)">
                  {{ expanded() === e.id ? i18n.t('trail.hidePayload') : i18n.t('trail.showPayload') }}
                </button>
                @if (expanded() === e.id) {
                  <pre class="payload">{{ prettyJson(e.payload) }}</pre>
                  <div class="hash muted" [title]="e.entryHash">{{ i18n.t('trail.hash') }}: {{ e.entryHash.slice(0, 16) }}…</div>
                }
              </div>
            </li>
          }
        </ol>
      }

      @if (perms.canViewCompliance() && changes().length > 0) {
        <h3 class="sub">{{ i18n.t('trail.fieldChanges') }}</h3>
        <table class="fc">
          <thead><tr>
            <th>{{ i18n.t('cmp.when') }}</th><th>{{ i18n.t('trail.action') }}</th>
            <th>{{ i18n.t('trail.property') }}</th><th>{{ i18n.t('trail.from') }}</th>
            <th>{{ i18n.t('trail.to') }}</th><th>{{ i18n.t('cmp.actor') }}</th>
          </tr></thead>
          <tbody>
            @for (f of changes(); track f.id) {
              <tr>
                <td>{{ f.occurredAtUtc | date:'short' }}</td>
                <td>{{ f.action }}</td>
                <td class="code">{{ f.property ?? '—' }}</td>
                <td class="val">{{ f.oldValue ?? '—' }}</td>
                <td class="val">{{ f.newValue ?? '—' }}</td>
                <td>{{ f.actor }}</td>
              </tr>
            }
          </tbody>
        </table>
      }
    </section>
  `,
    styles: [`
    .timeline { list-style: none; margin: 0; padding: 0; }
    .timeline li { display: flex; gap: 12px; position: relative; padding-bottom: 14px; }
    .timeline li:not(:last-child)::before {
      content: ""; position: absolute; inset-inline-start: 5px; top: 14px; bottom: 0;
      width: 2px; background: var(--nt-border);
    }
    .marker {
      width: 12px; height: 12px; border-radius: 50%; background: var(--nt-teal);
      border: 2px solid #fff; box-shadow: 0 0 0 1px var(--nt-border);
      flex-shrink: 0; margin-top: 3px; z-index: 1;
    }
    .entry { flex: 1; min-width: 0; }
    .head { display: flex; gap: 10px; align-items: baseline; flex-wrap: wrap; font-size: 12.5px; }
    .payload {
      background: var(--nt-bg-grey); border: 1px solid var(--nt-border); border-radius: 4px;
      padding: 8px 10px; font-size: 11px; overflow-x: auto; margin: 6px 0 2px;
      font-family: var(--nt-mono); white-space: pre-wrap; word-break: break-word;
    }
    .hash { font-size: 10.5px; font-family: var(--nt-mono); }
    .link { font-size: 11.5px; }
    .sub { margin-top: 16px; }
    .fc { font-size: 11.5px; }
    .fc .val { max-width: 200px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-family: var(--nt-mono); font-size: 10.5px; }
  `]
})
export class AuditTrailComponent implements OnInit {
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly api = inject(ComplianceApiService);

  /** Filter — typically the record's id; matched against payload/event type server-side. */
  readonly subject = input.required<string>();

  readonly entries = signal<AuditTrailEntry[]>([]);
  readonly changes = signal<FieldChange[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  /** Id of the entry whose payload is expanded ('' = none). */
  readonly expanded = signal('');

  async ngOnInit(): Promise<void> {
    if (!this.perms.canViewCompliance()) { return; }
    this.loading.set(true);
    try {
      const [entries, changes] = await Promise.all([
        firstValueFrom(this.api.auditTrail(this.subject())),
        firstValueFrom(this.api.fieldChanges(this.subject())),
      ]);
      this.entries.set(entries);
      this.changes.set(changes);
    } catch {
      this.error.set(this.i18n.t('trail.loadFailed'));
    } finally {
      this.loading.set(false);
    }
  }

  toggle(id: string): void { this.expanded.set(this.expanded() === id ? '' : id); }

  /** "NT.QAMS.Domain.Improvement.NcRaised, NT.QAMS.Domain" → "Nc Raised". */
  pretty(eventType: string): string {
    const typeName = eventType.split(',')[0] ?? eventType;
    const name = typeName.split('.').pop() ?? typeName;
    return name.replace(/([a-z])([A-Z])/g, '$1 $2');
  }

  prettyJson(payload: string): string {
    try {
      return JSON.stringify(JSON.parse(payload), null, 2);
    } catch {
      return payload;
    }
  }
}
