import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { MortalityReviewFacade } from './mortality-review.facade';
import { I18nService } from '../../core/i18n.service';
import {
  COMPLICATION_SEVERITIES, COMPLICATION_TYPES, ComplicationListItem, ComplicationSeverity, ComplicationType,
  DEATH_CLASSIFICATIONS, MORTALITY_STATUSES,
} from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';

/**
 * Mortality & Morbidity register (HQMS M10): the mortality rate per 1,000 patient-days and the
 * classification breakdown at the top; the mortality-review register with a report drawer and
 * drawer-detail (classify → second review → committee → close); and the complication register with
 * a report drawer and an inline review/close drawer.
 */
@Component({
    selector: 'qams-mortality-review-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, ListStatsComponent],
    template: `
    <qams-page-header [title]="i18n.t('mm.title')">
      <button (click)="deathForm.reset(deathDefaults); showDeath.set(true)">{{ i18n.t('mm.reportDeath') }}</button>
      <button class="secondary" (click)="compForm.reset(compDefaults); showComp.set(true)">{{ i18n.t('mm.reportComplication') }}</button>
    </qams-page-header>

    <qams-list-stats [stats]="stats()" />

    <div class="filterbar card">
      <select [value]="classFilter()" (change)="onFilter('class', $event)" aria-label="Classification filter">
        <option value="">{{ i18n.t('mm.allClasses') }}</option>
        @for (c of classifications; track c) { <option [value]="c">{{ i18n.t('mm.cl.' + c) }}</option> }
      </select>
      <select [value]="statusFilter()" (change)="onFilter('status', $event)" aria-label="Status filter">
        <option value="">{{ i18n.t('mm.allStatuses') }}</option>
        @for (s of statuses; track s) { <option [value]="s">{{ i18n.t('mm.st.' + s) }}</option> }
      </select>
    </div>

    <!-- Report death drawer -->
    <qams-drawer [open]="showDeath()" [title]="i18n.t('mm.reportDeath')" (closed)="showDeath.set(false)">
      <form class="drawer-form" [formGroup]="deathForm" (ngSubmit)="createDeath()">
        <div class="grid">
          <div><label>{{ i18n.t('mm.patientRef') }}</label><input formControlName="patientRef" /></div>
          <div><label>{{ i18n.t('mm.unit') }}</label><input formControlName="unit" /></div>
          <div><label>{{ i18n.t('mm.deathDate') }}</label><input type="datetime-local" formControlName="deathDate" /></div>
          <div><label>{{ i18n.t('mm.primaryDiagnosis') }}</label><input formControlName="primaryDiagnosis" /></div>
        </div>
        <div class="row">
          <button type="submit" [disabled]="deathForm.invalid || facade.loading()">{{ i18n.t('mm.submit') }}</button>
          <button type="button" class="secondary" (click)="showDeath.set(false)">{{ i18n.t('common.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    <!-- Report complication drawer -->
    <qams-drawer [open]="showComp()" [title]="i18n.t('mm.reportComplication')" (closed)="showComp.set(false)">
      <form class="drawer-form" [formGroup]="compForm" (ngSubmit)="createComplication()">
        <div class="grid">
          <div><label>{{ i18n.t('mm.type') }}</label><select formControlName="type">@for (t of compTypes; track t) { <option [value]="t">{{ i18n.t('mm.ct.' + t) }}</option> }</select></div>
          <div><label>{{ i18n.t('mm.severity') }}</label><select formControlName="severity">@for (s of severities; track s) { <option [value]="s">{{ i18n.t('mm.sev.' + s) }}</option> }</select></div>
          <div><label>{{ i18n.t('mm.patientRef') }}</label><input formControlName="patientRef" /></div>
          <div><label>{{ i18n.t('mm.unit') }}</label><input formControlName="unit" /></div>
          <div><label>{{ i18n.t('mm.occurredDate') }}</label><input type="datetime-local" formControlName="occurredDate" /></div>
        </div>
        <label>{{ i18n.t('mm.description') }}</label>
        <textarea rows="2" formControlName="description"></textarea>
        <div class="row">
          <button type="submit" [disabled]="compForm.invalid || facade.loading()">{{ i18n.t('mm.submit') }}</button>
          <button type="button" class="secondary" (click)="showComp.set(false)">{{ i18n.t('common.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    <!-- Mortality reviews -->
    <h3>{{ i18n.t('mm.reviewsHeading') }}</h3>
    @if (facade.loading() && facade.reviews().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.reviews().length === 0) {
      <p class="muted">{{ i18n.t('mm.noReviews') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('mm.ref') }}</th><th>{{ i18n.t('mm.patientRef') }}</th><th>{{ i18n.t('mm.unit') }}</th>
            <th>{{ i18n.t('mm.deathDate') }}</th><th>{{ i18n.t('mm.classification') }}</th><th>{{ i18n.t('mm.status') }}</th>
          </tr></thead>
          <tbody>
            @for (r of facade.reviews(); track r.id) {
              <tr class="clickable" (click)="openReview(r.id)">
                <td class="code">{{ r.reviewRef }}</td>
                <td>{{ r.patientRef }}</td>
                <td>{{ r.unit }}</td>
                <td>{{ r.deathDateUtc | date:'short' }}</td>
                <td>{{ r.classification ? i18n.t('mm.cl.' + r.classification) : '—' }}</td>
                <td><qams-status-pill [status]="r.status" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <!-- Complication register -->
    <h3>{{ i18n.t('mm.complicationsHeading') }}</h3>
    @if (facade.complications().length === 0) {
      <p class="muted">{{ i18n.t('mm.noComplications') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('mm.ref') }}</th><th>{{ i18n.t('mm.type') }}</th><th>{{ i18n.t('mm.severity') }}</th>
            <th>{{ i18n.t('mm.occurredDate') }}</th><th>{{ i18n.t('mm.preventable') }}</th><th>{{ i18n.t('mm.status') }}</th>
          </tr></thead>
          <tbody>
            @for (c of facade.complications(); track c.id) {
              <tr class="clickable" (click)="openComplication(c)">
                <td class="code">{{ c.caseRef }}</td>
                <td>{{ i18n.t('mm.ct.' + c.type) }}</td>
                <td>{{ i18n.t('mm.sev.' + c.severity) }}</td>
                <td>{{ c.occurredDateUtc | date:'short' }}</td>
                <td>{{ c.preventable === null ? '—' : (c.preventable ? i18n.t('mm.yes') : i18n.t('mm.no')) }}</td>
                <td><qams-status-pill [status]="c.status" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <!-- Complication review/close drawer -->
    <qams-drawer [open]="!!selectedComp()" [title]="selectedComp()?.caseRef ?? ''" (closed)="selectedComp.set(null)">
      @if (selectedComp(); as c) {
        <p><b>{{ i18n.t('mm.type') }}:</b> {{ i18n.t('mm.ct.' + c.type) }} · <b>{{ i18n.t('mm.severity') }}:</b> {{ i18n.t('mm.sev.' + c.severity) }}</p>
        <p class="muted">{{ c.patientRef }} · {{ c.unit }} · {{ c.occurredDateUtc | date:'medium' }}</p>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
        @if (c.status === 'Reported') {
          <form class="drawer-form" [formGroup]="reviewForm" (ngSubmit)="reviewComplication(c.id)">
            <label>{{ i18n.t('mm.reviewNotes') }}</label>
            <textarea rows="3" formControlName="notes"></textarea>
            <label class="chk"><input type="checkbox" formControlName="preventable" /> {{ i18n.t('mm.preventable') }}</label>
            <button type="submit" [disabled]="reviewForm.invalid || facade.loading()">{{ i18n.t('mm.recordReview') }}</button>
          </form>
        } @else if (c.status === 'Reviewed') {
          <p>{{ i18n.t('mm.preventable') }}: <b>{{ c.preventable ? i18n.t('mm.yes') : i18n.t('mm.no') }}</b></p>
          <button (click)="closeComplication(c.id)" [disabled]="facade.loading()">{{ i18n.t('mm.close') }}</button>
        } @else { <p class="muted">{{ i18n.t('mm.complicationClosed') }}</p> }
      }
    </qams-drawer>

    <qams-drawer [open]="detailOpen()" [title]="i18n.t('mm.title')" width="840px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .filterbar { display: flex; gap: 10px; padding: 10px 14px; margin-bottom: 14px; flex-wrap: wrap; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: .5rem 1rem; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .chk { display: flex; align-items: center; gap: .4rem; margin: .5rem 0; } .chk input { width: auto; }
    .clickable { cursor: pointer; }
    h3 { margin: 1.4rem 0 .6rem; }
    select, button { width: auto; }
  `]
})
export class MortalityReviewListComponent implements OnInit {
  readonly facade = inject(MortalityReviewFacade);
  readonly i18n = inject(I18nService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly classifications = DEATH_CLASSIFICATIONS;
  readonly statuses = MORTALITY_STATUSES;
  readonly compTypes = COMPLICATION_TYPES;
  readonly severities = COMPLICATION_SEVERITIES;

  readonly showDeath = signal(false);
  readonly showComp = signal(false);
  readonly detailOpen = signal(false);
  readonly selectedComp = signal<ComplicationListItem | null>(null);
  readonly classFilter = signal('');
  readonly statusFilter = signal('');

  readonly stats = computed<ListStat[]>(() => {
    const r = this.facade.rates();
    return [
      { label: this.i18n.t('mm.stat.rate'), value: r?.mortalityRatePer1000 ?? 0, tone: 'red' },
      { label: this.i18n.t('mm.stat.deaths'), value: r?.deaths ?? 0, tone: 'slate' },
      { label: this.i18n.t('mm.stat.preventable'), value: (r?.potentiallyPreventable ?? 0) + (r?.preventable ?? 0), tone: 'orange' },
      { label: this.i18n.t('mm.stat.complications'), value: r?.complications ?? 0, tone: 'gold' },
      { label: this.i18n.t('mm.stat.patientDays'), value: r?.patientDays ?? 0, tone: 'teal' },
    ];
  });

  readonly deathDefaults = { patientRef: '', unit: '', deathDate: '', primaryDiagnosis: '' };
  readonly deathForm = this.fb.nonNullable.group({
    patientRef: ['', [Validators.required, Validators.maxLength(100)]],
    unit: ['', [Validators.maxLength(100)]],
    deathDate: ['', [Validators.required]],
    primaryDiagnosis: ['', [Validators.maxLength(300)]],
  });

  readonly compDefaults = { type: 'ReturnToTheatre' as ComplicationType, severity: 'Moderate' as ComplicationSeverity, patientRef: '', unit: '', occurredDate: '', description: '' };
  readonly compForm = this.fb.nonNullable.group({
    type: ['ReturnToTheatre' as ComplicationType, [Validators.required]],
    severity: ['Moderate' as ComplicationSeverity, [Validators.required]],
    patientRef: ['', [Validators.required, Validators.maxLength(100)]],
    unit: ['', [Validators.maxLength(100)]],
    occurredDate: ['', [Validators.required]],
    description: ['', [Validators.maxLength(4000)]],
  });

  readonly reviewForm = this.fb.nonNullable.group({
    notes: ['', [Validators.required, Validators.maxLength(4000)]],
    preventable: [false],
  });

  ngOnInit(): void {
    void this.facade.loadAll();
  }

  onFilter(which: 'class' | 'status', event: Event): void {
    const val = (event.target as HTMLSelectElement).value;
    if (which === 'class') { this.classFilter.set(val); } else { this.statusFilter.set(val); }
    void this.facade.loadAll(this.classFilter() || undefined, this.statusFilter() || undefined);
  }

  async createDeath(): Promise<void> {
    if (this.deathForm.invalid) { return; }
    const raw = this.deathForm.getRawValue();
    const id = await this.facade.reportReview({
      patientRef: raw.patientRef, unit: raw.unit, deathDateUtc: new Date(raw.deathDate).toISOString(),
      primaryDiagnosis: raw.primaryDiagnosis || null, departmentId: null,
    });
    if (id) {
      this.showDeath.set(false);
      void this.facade.loadAll(this.classFilter() || undefined, this.statusFilter() || undefined);
      void this.router.navigate(['/mortality-review', id]);
    }
  }

  async createComplication(): Promise<void> {
    if (this.compForm.invalid) { return; }
    const raw = this.compForm.getRawValue();
    const id = await this.facade.reportComplication({
      type: raw.type, severity: raw.severity, patientRef: raw.patientRef, unit: raw.unit,
      occurredDateUtc: new Date(raw.occurredDate).toISOString(), description: raw.description, departmentId: null,
    });
    if (id) { this.showComp.set(false); }
  }

  openComplication(c: ComplicationListItem): void {
    this.reviewForm.reset({ notes: '', preventable: false });
    this.selectedComp.set(c);
  }

  async reviewComplication(id: string): Promise<void> {
    if (this.reviewForm.invalid) { return; }
    await this.facade.reviewComplication(id, this.reviewForm.getRawValue());
    if (this.facade.error() === '') { this.selectedComp.set(null); }
  }

  async closeComplication(id: string): Promise<void> {
    await this.facade.closeComplication(id);
    if (this.facade.error() === '') { this.selectedComp.set(null); }
  }

  openReview(id: string): void { void this.router.navigate(['/mortality-review', id]); }
  closeDetail(): void { void this.router.navigate(['/mortality-review']); }
}
