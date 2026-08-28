import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterOutlet } from '@angular/router';
import { FmeaFacade } from './fmea.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { FMEA_TYPES, FmeaType } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';

/**
 * FMEA / HFMEA register (HQMS M04): prospective failure-mode analyses with their highest
 * RPN and priority-mode count at a glance; the worksheet opens in a wide drawer.
 */
@Component({
    selector: 'qams-fmea-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, ListStatsComponent],
    template: `
    <qams-page-header [title]="i18n.t('fme.title')">
      @if (perms.can('risks.create')) {
        <button (click)="showForm.set(true)">{{ i18n.t('fme.new') }}</button>
      }
    </qams-page-header>

    <qams-list-stats [stats]="stats()" ratioFromFirst />

    <qams-drawer [open]="showForm()" [title]="i18n.t('fme.new')" (closed)="showForm.set(false)">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="create()">
        <label>{{ i18n.t('fme.fmeaTitle') }}</label>
        <input formControlName="title" />
        <label>{{ i18n.t('fme.process') }}</label>
        <input formControlName="processName" />
        <label>{{ i18n.t('fme.type') }}</label>
        <select formControlName="type">@for (t of types; track t) { <option [value]="t">{{ i18n.t('fme.ty.' + t) }}</option> }</select>
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('fme.create') }}</button>
          <button type="button" class="secondary" (click)="showForm.set(false)">{{ i18n.t('common.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.list().length === 0) {
      <p class="muted">{{ i18n.t('fme.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead>
            <tr>
              <th>{{ i18n.t('fme.ref') }}</th><th>{{ i18n.t('fme.fmeaTitle') }}</th>
              <th>{{ i18n.t('fme.process') }}</th><th>{{ i18n.t('fme.type') }}</th>
              <th>{{ i18n.t('fme.modes') }}</th><th>{{ i18n.t('fme.highRpn') }}</th>
              <th>{{ i18n.t('fme.maxRpn') }}</th><th>{{ i18n.t('fme.status') }}</th>
            </tr>
          </thead>
          <tbody>
            @for (f of facade.list(); track f.id) {
              <tr class="clickable" (click)="open(f.id)">
                <td class="code">{{ f.fmeaRef }}</td>
                <td>{{ f.title }}</td>
                <td>{{ f.processName }}</td>
                <td>{{ i18n.t('fme.ty.' + f.type) }}</td>
                <td>{{ f.failureModeCount }}</td>
                <td [class.danger-text]="f.highRpnCount > 0">{{ f.highRpnCount }}</td>
                <td [class.danger-text]="f.maxRpn >= 100">{{ f.maxRpn }}</td>
                <td><qams-status-pill [status]="f.status" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <qams-drawer [open]="detailOpen()" [title]="i18n.t('fme.title')" width="1040px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    select, button { width: auto; }
    .danger-text { color: var(--nt-ink-crit); font-weight: 700; }
  `]
})
export class FmeaListComponent implements OnInit {
  readonly facade = inject(FmeaFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly types = FMEA_TYPES;
  readonly showForm = signal(false);
  readonly detailOpen = signal(false);

  readonly stats = computed<ListStat[]>(() => {
    const all = this.facade.list();
    return [
      { label: this.i18n.t('stat.total'), value: all.length, tone: 'slate' },
      { label: this.i18n.t('fme.stat.active'), value: all.filter((f) => f.status === 'Active').length, tone: 'blue' },
      { label: this.i18n.t('fme.stat.highRpn'), value: all.reduce((n, f) => n + f.highRpnCount, 0), tone: 'red' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    processName: ['', [Validators.required, Validators.maxLength(200)]],
    type: ['Hfmea' as FmeaType, [Validators.required]],
  });

  ngOnInit(): void {
    void this.facade.loadList();
  }

  async create(): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    const id = await this.facade.create({ ...raw, branchId: null, departmentId: null });
    if (id) {
      this.showForm.set(false);
      this.form.reset({ type: 'Hfmea' });
      void this.router.navigate(['/fmea', id]);
    }
  }

  open(id: string): void { void this.router.navigate(['/fmea', id]); }
  closeDetail(): void { void this.router.navigate(['/fmea']); }
}
