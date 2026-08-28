import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { SurveysFacade } from './surveys.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';

/** Patient satisfaction survey register (HQMS M11). */
@Component({
    selector: 'qams-surveys-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DecimalPipe, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, ListStatsComponent],
    template: `
    <qams-page-header [title]="i18n.t('svy.title')">
      @if (perms.can('surveys.create')) {
        <button (click)="showForm.set(true)">{{ i18n.t('svy.new') }}</button>
      }
    </qams-page-header>

    <qams-list-stats [stats]="stats()" ratioFromFirst />

    <qams-drawer [open]="showForm()" [title]="i18n.t('svy.new')" (closed)="showForm.set(false)">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="create()">
        <label>{{ i18n.t('svy.surveyTitle') }}</label>
        <input formControlName="title" />
        <label>{{ i18n.t('svy.description') }}</label>
        <textarea rows="2" formControlName="description"></textarea>
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('svy.create') }}</button>
          <button type="button" class="secondary" (click)="showForm.set(false)">{{ i18n.t('common.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.list().length === 0) {
      <p class="muted">{{ i18n.t('svy.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('svy.surveyTitle') }}</th><th>{{ i18n.t('svy.questions') }}</th>
            <th>{{ i18n.t('svy.responses') }}</th><th>{{ i18n.t('svy.overall') }}</th><th>{{ i18n.t('svy.status') }}</th>
          </tr></thead>
          <tbody>
            @for (s of facade.list(); track s.id) {
              <tr class="clickable" (click)="open(s.id)">
                <td>{{ s.title }}</td>
                <td>{{ s.questionCount }}</td>
                <td>{{ s.responseCount }}</td>
                <td>{{ s.overallScore !== null ? (s.overallScore | number:'1.1-2') + ' / 5' : '—' }}</td>
                <td><qams-status-pill [status]="s.status" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <qams-drawer [open]="detailOpen()" [title]="i18n.t('svy.title')" width="960px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    button { width: auto; }
  `]
})
export class SurveysListComponent implements OnInit {
  readonly facade = inject(SurveysFacade);
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
      { label: this.i18n.t('svy.stat.open'), value: all.filter((s) => s.status === 'Open').length, tone: 'blue' },
      { label: this.i18n.t('svy.stat.responses'), value: all.reduce((n, s) => n + s.responseCount, 0), tone: 'teal' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.maxLength(2000)]],
  });

  ngOnInit(): void {
    void this.facade.loadList();
  }

  async create(): Promise<void> {
    if (this.form.invalid) { return; }
    const raw = this.form.getRawValue();
    const id = await this.facade.create({ title: raw.title, description: raw.description || null });
    if (id) {
      this.showForm.set(false);
      this.form.reset();
      void this.router.navigate(['/surveys', id]);
    }
  }

  open(id: string): void { void this.router.navigate(['/surveys', id]); }
  closeDetail(): void { void this.router.navigate(['/surveys']); }
}
