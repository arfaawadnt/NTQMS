import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { StandardsFacade } from './standards.facade';
import { I18nService } from '../../core/i18n.service';
import { ACCREDITATION_FRAMEWORKS, AccreditationFramework } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';

/**
 * Standard-set register (HQMS M07): the frameworks the hospital is accredited against,
 * each with its live readiness figure; the record workspace opens in a wide drawer.
 */
@Component({
    selector: 'qams-standards-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DecimalPipe, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, ListStatsComponent],
    template: `
    <qams-page-header [title]="i18n.t('acr.title')">
      <button (click)="showForm.set(true)">{{ i18n.t('acr.new') }}</button>
    </qams-page-header>

    <qams-list-stats [stats]="stats()" ratioFromFirst />

    <qams-drawer [open]="showForm()" [title]="i18n.t('acr.new')" (closed)="showForm.set(false)">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="create()">
        <label>{{ i18n.t('acr.framework') }}</label>
        <select formControlName="framework">@for (f of frameworks; track f) { <option [value]="f">{{ i18n.t('acr.fw.' + f) }}</option> }</select>
        <label>{{ i18n.t('acr.name') }}</label>
        <input formControlName="name" />
        <label>{{ i18n.t('acr.version') }}</label>
        <input formControlName="version" />
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('acr.create') }}</button>
          <button type="button" class="secondary" (click)="showForm.set(false)">{{ i18n.t('common.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.list().length === 0) {
      <p class="muted">{{ i18n.t('acr.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead>
            <tr>
              <th>{{ i18n.t('acr.framework') }}</th><th>{{ i18n.t('acr.name') }}</th>
              <th>{{ i18n.t('acr.version') }}</th><th>{{ i18n.t('acr.elements') }}</th>
              <th>{{ i18n.t('acr.readiness') }}</th><th>{{ i18n.t('acr.status') }}</th>
            </tr>
          </thead>
          <tbody>
            @for (s of facade.list(); track s.id) {
              <tr class="clickable" (click)="open(s.id)">
                <td>{{ i18n.t('acr.fw.' + s.framework) }}</td>
                <td>{{ s.name }}</td>
                <td>{{ s.version }}</td>
                <td>{{ s.elementCount }}</td>
                <td>
                  <div class="bar"><span [style.width.%]="s.compliancePercent"
                    [class.ok]="s.compliancePercent >= 90" [class.warn]="s.compliancePercent < 90 && s.compliancePercent >= 60"
                    [class.bad]="s.compliancePercent < 60"></span></div>
                  <span class="pct">{{ s.compliancePercent | number:'1.0-1' }}%</span>
                </td>
                <td><qams-status-pill [status]="s.status" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <qams-drawer [open]="detailOpen()" [title]="i18n.t('acr.title')" width="1000px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    select, button { width: auto; }
    .bar { display: inline-block; width: 90px; height: 8px; background: #e6ebf1; border-radius: 5px; overflow: hidden; vertical-align: middle; }
    .bar > span { display: block; height: 100%; background: var(--nt-ink-info); }
    .bar > span.ok { background: var(--nt-ink-ok); } .bar > span.warn { background: var(--nt-ink-warn); } .bar > span.bad { background: var(--nt-ink-crit); }
    .pct { margin-inline-start: .4rem; font-size: .85rem; }
  `]
})
export class StandardsListComponent implements OnInit {
  readonly facade = inject(StandardsFacade);
  readonly i18n = inject(I18nService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly frameworks = ACCREDITATION_FRAMEWORKS;
  readonly showForm = signal(false);
  readonly detailOpen = signal(false);

  readonly stats = computed<ListStat[]>(() => {
    const all = this.facade.list();
    return [
      { label: this.i18n.t('stat.total'), value: all.length, tone: 'slate' },
      { label: this.i18n.t('acr.stat.active'), value: all.filter((s) => s.status === 'Active').length, tone: 'blue' },
      { label: this.i18n.t('acr.stat.draft'), value: all.filter((s) => s.status === 'Draft').length, tone: 'gold' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    framework: ['GAHAR' as AccreditationFramework, [Validators.required]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    version: ['', [Validators.required, Validators.maxLength(40)]],
  });

  ngOnInit(): void {
    void this.facade.loadList();
  }

  async create(): Promise<void> {
    if (this.form.invalid) { return; }
    const id = await this.facade.define(this.form.getRawValue());
    if (id) {
      this.showForm.set(false);
      this.form.reset({ framework: 'GAHAR' });
      void this.router.navigate(['/standards', id]);
    }
  }

  open(id: string): void { void this.router.navigate(['/standards', id]); }
  closeDetail(): void { void this.router.navigate(['/standards']); }
}
