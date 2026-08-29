import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { IntegrationFacade } from './integration.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { INTERFACE_PROTOCOLS, INTERFACE_SYSTEMS, InterfaceProtocol, InterfaceSystem } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';

/**
 * Integration monitoring (HQMS M24): interface health dashboard, the ADT-derived patient
 * census, and per-endpoint message inspection (including failures). Endpoint configuration
 * and message replay are gated on the Integration module permissions.
 */
@Component({
    selector: 'qams-integration',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, PageHeaderComponent, StatusPillComponent, ListStatsComponent],
    template: `
    <qams-page-header [title]="i18n.t('intg.title')">
      @if (perms.can('integration.manage')) { <button (click)="showForm.set(!showForm())">{{ i18n.t('intg.register') }}</button> }
    </qams-page-header>

    <qams-list-stats [stats]="stats()" ratioFromFirst />
    @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

    @if (showForm() && perms.can('integration.manage')) {
      <section class="card">
        <h3>{{ i18n.t('intg.register') }}</h3>
        <form class="inline" [formGroup]="form" (ngSubmit)="register()">
          <input formControlName="name" [placeholder]="i18n.t('intg.name')" />
          <select formControlName="system">@for (s of systems; track s) { <option [value]="s">{{ i18n.t('intg.sys.' + s) }}</option> }</select>
          <select formControlName="protocol">@for (p of protocols; track p) { <option [value]="p">{{ i18n.t('intg.proto.' + p) }}</option> }</select>
          <button type="submit" [disabled]="form.invalid">{{ i18n.t('intg.register') }}</button>
        </form>
      </section>
    }

    <section class="card">
      <h3>{{ i18n.t('intg.endpoints') }}</h3>
      @if (facade.endpoints().length === 0) { <p class="muted">{{ i18n.t('intg.noEndpoints') }}</p> }
      @else {
        <table>
          <thead><tr>
            <th>{{ i18n.t('intg.health') }}</th><th>{{ i18n.t('intg.name') }}</th><th>{{ i18n.t('intg.system') }}</th>
            <th>{{ i18n.t('intg.protocol') }}</th><th>{{ i18n.t('intg.lastMessage') }}</th>
            <th>R / P / F</th><th></th>
          </tr></thead>
          <tbody>
            @for (e of facade.endpoints(); track e.id) {
              <tr>
                <td><span class="dot" [class.ok]="e.healthy" [class.bad]="!e.healthy"></span>
                  <qams-status-pill [status]="e.status" /></td>
                <td>{{ e.name }}</td>
                <td>{{ i18n.t('intg.sys.' + e.system) }}</td>
                <td>{{ i18n.t('intg.proto.' + e.protocol) }}</td>
                <td>{{ e.lastMessageAtUtc ? (e.lastMessageAtUtc | date:'short') : '—' }}
                  @if (e.consecutiveFailures > 0) { <span class="danger-text">· {{ e.consecutiveFailures }} {{ i18n.t('intg.fails') }}</span> }</td>
                <td>{{ e.received }} / {{ e.processed }} / <span [class.danger-text]="e.failed > 0">{{ e.failed }}</span></td>
                <td class="actions">
                  <button class="link" (click)="facade.toggleMessages(e.id)">{{ i18n.t('intg.messages') }}</button>
                  @if (perms.can('integration.manage')) {
                    @if (e.status === 'Active') { <button class="link danger-link" (click)="facade.suspend(e.id)">{{ i18n.t('intg.suspend') }}</button> }
                    @else { <button class="link" (click)="facade.resume(e.id)">{{ i18n.t('intg.resume') }}</button> }
                  }
                </td>
              </tr>
              @if (facade.expandedId() === e.id) {
                <tr class="sub"><td colspan="7">
                  @if (facade.messages().length === 0) { <p class="muted">{{ i18n.t('intg.noMessages') }}</p> }
                  @else {
                    <table class="msgs">
                      <thead><tr><th>{{ i18n.t('intg.received') }}</th><th>{{ i18n.t('intg.type') }}</th><th>{{ i18n.t('intg.dedup') }}</th><th>{{ i18n.t('intg.msgStatus') }}</th><th>{{ i18n.t('intg.error') }}</th></tr></thead>
                      <tbody>
                        @for (m of facade.messages(); track m.id) {
                          <tr>
                            <td>{{ m.receivedAtUtc | date:'short' }}</td>
                            <td class="code">{{ m.messageType }}</td>
                            <td class="code">{{ m.dedupKey }}</td>
                            <td><qams-status-pill [status]="m.status" /></td>
                            <td class="muted">{{ m.errorDetail || '—' }}</td>
                          </tr>
                        }
                      </tbody>
                    </table>
                  }
                </td></tr>
              }
            }
          </tbody>
        </table>
      }
    </section>
  `,
    styles: [`
    button, select, input { width: auto; }
    .inline { display: flex; gap: .5rem; flex-wrap: wrap; align-items: center; }
    .dot { display: inline-block; width: 9px; height: 9px; border-radius: 50%; margin-inline-end: .4rem; vertical-align: middle; }
    .dot.ok { background: var(--nt-ink-ok); } .dot.bad { background: var(--nt-ink-crit); }
    .danger-text { color: var(--nt-ink-crit); font-weight: 600; }
    .sub td { background: var(--nt-surface-alt, #f4f7fa); }
    table.msgs { font-size: .82rem; }
    .actions { display: flex; gap: .5rem; flex-wrap: wrap; }
  `]
})
export class IntegrationComponent implements OnInit {
  readonly facade = inject(IntegrationFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  readonly systems = INTERFACE_SYSTEMS;
  readonly protocols = INTERFACE_PROTOCOLS;
  readonly showForm = signal(false);

  readonly stats = computed<ListStat[]>(() => {
    const eps = this.facade.endpoints();
    const c = this.facade.census();
    return [
      { label: this.i18n.t('intg.stat.endpoints'), value: eps.length, tone: 'slate' },
      { label: this.i18n.t('intg.stat.unhealthy'), value: eps.filter((e) => !e.healthy).length, tone: 'red' },
      { label: this.i18n.t('intg.stat.failed'), value: eps.reduce((n, e) => n + e.failed, 0), tone: 'orange' },
      { label: this.i18n.t('intg.stat.activeStays'), value: c?.activeStays ?? 0, tone: 'blue' },
      { label: this.i18n.t('intg.stat.patientDays'), value: c?.patientDaysWindow ?? 0, tone: 'teal' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(150)]],
    system: ['His' as InterfaceSystem, [Validators.required]],
    protocol: ['Hl7V2' as InterfaceProtocol, [Validators.required]],
  });

  ngOnInit(): void {
    void this.facade.load();
  }

  async register(): Promise<void> {
    if (this.form.invalid) { return; }
    const id = await this.facade.register(this.form.getRawValue());
    if (id) { this.showForm.set(false); this.form.reset({ system: 'His', protocol: 'Hl7V2' }); }
  }
}
