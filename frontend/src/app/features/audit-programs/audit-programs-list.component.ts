import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { AuditProgramsFacade } from './audit-programs.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';

/**
 * Annual audit-programme register (HQMS M05): each programme with its live coverage;
 * the plan workspace opens in a wide drawer.
 */
@Component({
    selector: 'qams-audit-programs-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DecimalPipe, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, ListStatsComponent],
    template: `
    <qams-page-header [title]="i18n.t('apg.title')">
      @if (perms.can('audits.create')) {
        <button (click)="showForm.set(true)">{{ i18n.t('apg.new') }}</button>
      }
    </qams-page-header>

    <qams-list-stats [stats]="stats()" ratioFromFirst />

    <qams-drawer [open]="showForm()" [title]="i18n.t('apg.new')" (closed)="showForm.set(false)">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="create()">
        <label>{{ i18n.t('apg.year') }}</label>
        <input type="number" formControlName="year" />
        <label>{{ i18n.t('apg.programTitle') }}</label>
        <input formControlName="title" />
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('apg.create') }}</button>
          <button type="button" class="secondary" (click)="showForm.set(false)">{{ i18n.t('common.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.list().length === 0) {
      <p class="muted">{{ i18n.t('apg.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead>
            <tr>
              <th>{{ i18n.t('apg.year') }}</th><th>{{ i18n.t('apg.programTitle') }}</th>
              <th>{{ i18n.t('apg.planned') }}</th><th>{{ i18n.t('apg.coverage') }}</th><th>{{ i18n.t('apg.status') }}</th>
            </tr>
          </thead>
          <tbody>
            @for (p of facade.list(); track p.id) {
              <tr class="clickable" (click)="open(p.id)">
                <td>{{ p.year }}</td>
                <td>{{ p.title }}</td>
                <td>{{ p.plannedCount }}</td>
                <td>
                  <div class="bar"><span [style.width.%]="p.coveragePercent"
                    [class.ok]="p.coveragePercent >= 90" [class.warn]="p.coveragePercent < 90 && p.coveragePercent >= 50" [class.bad]="p.coveragePercent < 50"></span></div>
                  <span class="pct">{{ p.coveragePercent | number:'1.0-0' }}%</span>
                </td>
                <td><qams-status-pill [status]="p.status" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <qams-drawer [open]="detailOpen()" [title]="i18n.t('apg.title')" width="960px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    button { width: auto; }
    .bar { display: inline-block; width: 90px; height: 8px; background: #e6ebf1; border-radius: 5px; overflow: hidden; vertical-align: middle; }
    .bar > span { display: block; height: 100%; background: var(--nt-ink-info); }
    .bar > span.ok { background: var(--nt-ink-ok); } .bar > span.warn { background: var(--nt-ink-warn); } .bar > span.bad { background: var(--nt-ink-crit); }
    .pct { margin-inline-start: .4rem; font-size: .85rem; }
  `]
})
export class AuditProgramsListComponent implements OnInit {
  readonly facade = inject(AuditProgramsFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly showForm = signal(false);
  readonly detailOpen = signal(false);

  readonly stats = computed<ListStat[]>(() => {
    const all = this.facade.list();
    return [
      { label: this.i18n.t('stat.total'), value: all.length, tone: 'slate' },
      { label: this.i18n.t('apg.stat.active'), value: all.filter((p) => p.status === 'Active').length, tone: 'blue' },
      { label: this.i18n.t('apg.stat.draft'), value: all.filter((p) => p.status === 'Draft').length, tone: 'gold' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    year: [2026, [Validators.required, Validators.min(2000), Validators.max(2100)]],
    title: ['', [Validators.required, Validators.maxLength(200)]],
  });

  ngOnInit(): void {
    void this.facade.loadList();
  }

  async create(): Promise<void> {
    if (this.form.invalid) { return; }
    const id = await this.facade.create(this.form.getRawValue());
    if (id) {
      this.showForm.set(false);
      this.form.reset({ year: 2026 });
      void this.router.navigate(['/audit-programs', id]);
    }
  }

  open(id: string): void { void this.router.navigate(['/audit-programs', id]); }
  closeDetail(): void { void this.router.navigate(['/audit-programs']); }
}
