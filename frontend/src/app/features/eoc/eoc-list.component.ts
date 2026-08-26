import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { EocFacade } from './eoc.facade';
import { I18nService } from '../../core/i18n.service';
import { DRILL_TYPES, DrillType, ROUND_TYPES, RoundType } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';

/**
 * Environment of Care register (HQMS M15): the EOC summary at the top; the safety-round register
 * with a schedule drawer and drawer-detail; and the drill register with its own schedule drawer
 * and drawer-detail. Both detail types share one routed drawer.
 */
@Component({
    selector: 'qams-eoc-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, ListStatsComponent],
    template: `
    <qams-page-header [title]="i18n.t('eoc.title')">
      <button (click)="roundForm.reset(roundDefaults); showRound.set(true)">{{ i18n.t('eoc.scheduleRound') }}</button>
      <button class="secondary" (click)="drillForm.reset(drillDefaults); showDrill.set(true)">{{ i18n.t('eoc.scheduleDrill') }}</button>
    </qams-page-header>

    <qams-list-stats [stats]="stats()" />

    <qams-drawer [open]="showRound()" [title]="i18n.t('eoc.scheduleRound')" (closed)="showRound.set(false)">
      <form class="drawer-form" [formGroup]="roundForm" (ngSubmit)="createRound()">
        <label>{{ i18n.t('eoc.area') }}</label>
        <input formControlName="area" />
        <div class="grid">
          <div><label>{{ i18n.t('eoc.type') }}</label><select formControlName="type">@for (t of roundTypes; track t) { <option [value]="t">{{ i18n.t('eoc.rt.' + t) }}</option> }</select></div>
          <div><label>{{ i18n.t('eoc.scheduledDate') }}</label><input type="date" formControlName="scheduledDate" /></div>
        </div>
        <div class="row">
          <button type="submit" [disabled]="roundForm.invalid || facade.loading()">{{ i18n.t('eoc.schedule') }}</button>
          <button type="button" class="secondary" (click)="showRound.set(false)">{{ i18n.t('common.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    <qams-drawer [open]="showDrill()" [title]="i18n.t('eoc.scheduleDrill')" (closed)="showDrill.set(false)">
      <form class="drawer-form" [formGroup]="drillForm" (ngSubmit)="createDrill()">
        <div class="grid">
          <div><label>{{ i18n.t('eoc.type') }}</label><select formControlName="type">@for (t of drillTypes; track t) { <option [value]="t">{{ i18n.t('eoc.dt.' + t) }}</option> }</select></div>
          <div><label>{{ i18n.t('eoc.location') }}</label><input formControlName="location" /></div>
          <div><label>{{ i18n.t('eoc.scheduledDate') }}</label><input type="date" formControlName="scheduledDate" /></div>
        </div>
        <div class="row">
          <button type="submit" [disabled]="drillForm.invalid || facade.loading()">{{ i18n.t('eoc.schedule') }}</button>
          <button type="button" class="secondary" (click)="showDrill.set(false)">{{ i18n.t('common.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    <h3>{{ i18n.t('eoc.roundsHeading') }}</h3>
    @if (facade.rounds().length === 0) {
      <p class="muted">{{ i18n.t('eoc.noRounds') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('eoc.ref') }}</th><th>{{ i18n.t('eoc.area') }}</th><th>{{ i18n.t('eoc.type') }}</th>
            <th>{{ i18n.t('eoc.scheduledDate') }}</th><th>{{ i18n.t('eoc.openFindings') }}</th><th>{{ i18n.t('eoc.status') }}</th>
          </tr></thead>
          <tbody>
            @for (r of facade.rounds(); track r.id) {
              <tr class="clickable" (click)="openRound(r.id)">
                <td class="code">{{ r.roundRef }}</td>
                <td>{{ r.area }}</td>
                <td>{{ i18n.t('eoc.rt.' + r.type) }}</td>
                <td>{{ r.scheduledDate | date:'mediumDate' }}</td>
                <td [class.danger-text]="r.openFindings > 0">{{ r.openFindings }} / {{ r.totalFindings }}</td>
                <td><qams-status-pill [status]="r.status" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <h3>{{ i18n.t('eoc.drillsHeading') }}</h3>
    @if (facade.drills().length === 0) {
      <p class="muted">{{ i18n.t('eoc.noDrills') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('eoc.ref') }}</th><th>{{ i18n.t('eoc.type') }}</th><th>{{ i18n.t('eoc.location') }}</th>
            <th>{{ i18n.t('eoc.scheduledDate') }}</th><th>{{ i18n.t('eoc.score') }}</th><th>{{ i18n.t('eoc.status') }}</th>
          </tr></thead>
          <tbody>
            @for (d of facade.drills(); track d.id) {
              <tr class="clickable" (click)="openDrill(d.id)">
                <td class="code">{{ d.drillRef }}</td>
                <td>{{ i18n.t('eoc.dt.' + d.type) }}</td>
                <td>{{ d.location }}</td>
                <td>{{ d.scheduledDate | date:'mediumDate' }}</td>
                <td>{{ d.evaluationScore ?? '—' }}@if (d.effectiveness) { <span class="eff" [class]="'eff ' + d.effectiveness.toLowerCase()">{{ i18n.t('eoc.eff.' + d.effectiveness) }}</span> }</td>
                <td><qams-status-pill [status]="d.status" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <qams-drawer [open]="detailOpen()" [title]="i18n.t('eoc.title')" width="840px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: .5rem 1rem; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; } .danger-text { color: var(--nt-ink-crit); font-weight: 700; }
    h3 { margin: 1.4rem 0 .6rem; }
    select, button { width: auto; }
    .eff { margin-inline-start: 6px; padding: 1px 7px; border-radius: 999px; font-size: 11px; font-weight: 700; }
    .eff.effective { background: color-mix(in srgb, var(--nt-ink-ok) 18%, transparent); color: var(--nt-ink-ok); }
    .eff.partiallyeffective { background: color-mix(in srgb, var(--nt-ink-warn) 22%, transparent); color: #3a2d00; }
    .eff.ineffective { background: var(--nt-ink-crit); color: #fff; }
  `]
})
export class EocListComponent implements OnInit {
  readonly facade = inject(EocFacade);
  readonly i18n = inject(I18nService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly roundTypes = ROUND_TYPES;
  readonly drillTypes = DRILL_TYPES;

  readonly showRound = signal(false);
  readonly showDrill = signal(false);
  readonly detailOpen = signal(false);

  readonly stats = computed<ListStat[]>(() => {
    const s = this.facade.summary();
    return [
      { label: this.i18n.t('eoc.stat.roundsCompleted'), value: s?.roundsCompleted ?? 0, tone: 'teal' },
      { label: this.i18n.t('eoc.stat.openFindings'), value: s?.openFindings ?? 0, tone: 'orange' },
      { label: this.i18n.t('eoc.stat.critical'), value: s?.criticalOpenFindings ?? 0, tone: 'red' },
      { label: this.i18n.t('eoc.stat.drillsEvaluated'), value: s?.drillsEvaluated ?? 0, tone: 'slate' },
      { label: this.i18n.t('eoc.stat.meanScore'), value: s?.meanDrillScore ?? 0, tone: 'green' },
    ];
  });

  readonly roundDefaults = { area: '', type: 'FireSafety' as RoundType, scheduledDate: '' };
  readonly roundForm = this.fb.nonNullable.group({
    area: ['', [Validators.required, Validators.maxLength(150)]],
    type: ['FireSafety' as RoundType, [Validators.required]],
    scheduledDate: ['', [Validators.required]],
  });

  readonly drillDefaults = { type: 'Fire' as DrillType, location: '', scheduledDate: '' };
  readonly drillForm = this.fb.nonNullable.group({
    type: ['Fire' as DrillType, [Validators.required]],
    location: ['', [Validators.required, Validators.maxLength(150)]],
    scheduledDate: ['', [Validators.required]],
  });

  ngOnInit(): void {
    void this.facade.loadAll();
  }

  async createRound(): Promise<void> {
    if (this.roundForm.invalid) { return; }
    const id = await this.facade.scheduleRound(this.roundForm.getRawValue());
    if (id) {
      this.showRound.set(false);
      void this.facade.loadAll();
      void this.router.navigate(['/eoc/rounds', id]);
    }
  }

  async createDrill(): Promise<void> {
    if (this.drillForm.invalid) { return; }
    const id = await this.facade.scheduleDrill(this.drillForm.getRawValue());
    if (id) {
      this.showDrill.set(false);
      void this.facade.loadAll();
      void this.router.navigate(['/eoc/drills', id]);
    }
  }

  openRound(id: string): void { void this.router.navigate(['/eoc/rounds', id]); }
  openDrill(id: string): void { void this.router.navigate(['/eoc/drills', id]); }
  closeDetail(): void { void this.router.navigate(['/eoc']); }
}
