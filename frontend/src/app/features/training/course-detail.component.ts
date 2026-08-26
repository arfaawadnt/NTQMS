import { ChangeDetectionStrategy, Component, OnInit, inject, input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { TrainingFacade } from './training.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';

/**
 * Course workspace (HQMS M12): the catalogue entry with its effectiveness roll-up (pass rate,
 * mean pre/post scores, mean gain), lifecycle actions, and the sessions delivered for it with a
 * schedule form. A session drills into its own attendance workspace.
 */
@Component({
    selector: 'qams-course-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, RouterLink, PageHeaderComponent, StatusPillComponent],
    template: `
    @if (course(); as c) {
      <qams-page-header [title]="c.courseRef + ' — ' + c.title">
        <a routerLink="/training-catalogue" class="ghost-link">← {{ i18n.t('trn.backToList') }}</a>
      </qams-page-header>

      <div class="grid">
        <section class="card">
          <div class="meta">
            <div><span class="muted">{{ i18n.t('trn.status') }}</span><qams-status-pill [status]="c.status" /></div>
            <div><span class="muted">{{ i18n.t('trn.category') }}</span> {{ i18n.t('trn.cat.' + c.category) }}</div>
            <div><span class="muted">{{ i18n.t('trn.durationHours') }}</span> {{ c.durationHours }}h</div>
            <div><span class="muted">{{ i18n.t('trn.passMark') }}</span> {{ c.passMark }}%</div>
            <div><span class="muted">{{ i18n.t('trn.validityMonths') }}</span> {{ c.validityMonths ? c.validityMonths + ' mo' : i18n.t('trn.noExpiry') }}</div>
          </div>
          @if (c.description) { <p>{{ c.description }}</p> }

          <h3>{{ i18n.t('trn.effectiveness') }}</h3>
          <div class="eff">
            <div class="k"><b>{{ c.effectiveness.passRate }}%</b><span>{{ i18n.t('trn.passRate') }}</span></div>
            <div class="k"><b>{{ c.effectiveness.attendedCount }}</b><span>{{ i18n.t('trn.attended') }}</span></div>
            <div class="k"><b>{{ c.effectiveness.meanPreScore ?? '—' }}</b><span>{{ i18n.t('trn.meanPre') }}</span></div>
            <div class="k"><b>{{ c.effectiveness.meanPostScore ?? '—' }}</b><span>{{ i18n.t('trn.meanPost') }}</span></div>
            <div class="k"><b [class.gain]="(c.effectiveness.meanGain ?? 0) > 0">{{ c.effectiveness.meanGain ?? '—' }}</b><span>{{ i18n.t('trn.meanGain') }}</span></div>
          </div>

          <h3>{{ i18n.t('trn.sessions') }}</h3>
          @if (facade.courseSessions().length === 0) { <p class="muted">{{ i18n.t('trn.noSessions') }}</p> }
          @for (s of facade.courseSessions(); track s.id) {
            <div class="row-item">
              <a class="ghost-link" [routerLink]="['/training-catalogue/sessions', s.id]">{{ s.sessionRef }}</a>
              <span class="muted">{{ s.scheduledAtUtc | date:'short' }} · {{ s.location }}</span>
              <span>{{ s.attendedCount }}/{{ s.registeredCount }} {{ i18n.t('trn.attended') }}</span>
              <qams-status-pill [status]="s.status" />
            </div>
          }
        </section>

        <section class="card actions">
          <h3>{{ i18n.t('trn.workflow') }}</h3>
          @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

          @if (c.status === 'Draft' && perms.can('training.approve')) {
            <button (click)="facade.activateCourse(c.id)" [disabled]="facade.loading()">{{ i18n.t('trn.activate') }}</button>
          }
          @if (c.status === 'Active' && perms.can('training.void')) {
            <button class="secondary" (click)="facade.retireCourse(c.id)" [disabled]="facade.loading()">{{ i18n.t('trn.retire') }}</button>
          }

          @if (c.status === 'Active' && perms.can('training.create')) {
            <form [formGroup]="sessionForm" (ngSubmit)="schedule(c.id)">
              <h3>{{ i18n.t('trn.scheduleSession') }}</h3>
              <label>{{ i18n.t('trn.scheduledAt') }}</label>
              <input type="datetime-local" formControlName="scheduledAt" />
              <label>{{ i18n.t('trn.location') }}</label>
              <input formControlName="location" />
              <label>{{ i18n.t('trn.trainer') }}</label>
              <input formControlName="trainerName" />
              <button type="submit" [disabled]="sessionForm.invalid || facade.loading()">{{ i18n.t('trn.schedule') }}</button>
            </form>
          }
          @if (c.status === 'Draft') { <p class="muted">{{ i18n.t('trn.activateFirst') }}</p> }
        </section>
      </div>
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .grid { display: grid; grid-template-columns: 2fr 1fr; gap: 1rem; align-items: start; }
    .meta { display: flex; flex-wrap: wrap; gap: 1.25rem; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .eff { display: flex; flex-wrap: wrap; gap: 1.5rem; }
    .eff .k { display: flex; flex-direction: column; }
    .eff .k b { font-size: 1.3rem; } .eff .k b.gain { color: var(--nt-ink-ok); }
    .eff .k span { font-size: .72rem; color: var(--nt-slate); }
    .row-item { display: flex; gap: 1rem; align-items: center; padding: .5rem 0; border-bottom: 1px solid var(--nt-border); flex-wrap: wrap; }
    .actions form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    .actions button { margin-top: .5rem; margin-inline-end: .5rem; width: auto; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    h3 { margin-top: 1rem; }
    @media (max-width: 800px) { .grid { grid-template-columns: 1fr; } }
  `]
})
export class CourseDetailComponent implements OnInit {
  readonly facade = inject(TrainingFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  /** Route-bound course id (provided via withComponentInputBinding). */
  readonly id = input.required<string>();
  readonly course = this.facade.course;

  readonly sessionForm = this.fb.nonNullable.group({
    scheduledAt: ['', [Validators.required]],
    location: ['', [Validators.maxLength(200)]],
    trainerName: ['', [Validators.maxLength(200)]],
  });

  ngOnInit(): void {
    void this.facade.loadCourse(this.id());
  }

  async schedule(courseId: string): Promise<void> {
    if (this.sessionForm.invalid) { return; }
    const raw = this.sessionForm.getRawValue();
    const id = await this.facade.scheduleSession({
      courseId, scheduledAtUtc: new Date(raw.scheduledAt).toISOString(),
      location: raw.location, trainerName: raw.trainerName,
    });
    if (id) {
      this.sessionForm.reset({ scheduledAt: '', location: '', trainerName: '' });
      void this.router.navigate(['/training-catalogue/sessions', id]);
    }
  }
}
