import { ChangeDetectionStrategy, Component, OnInit, inject, input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TrainingFacade } from './training.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { WorkflowStepperComponent } from '../../shared/ui/workflow-stepper.component';
import { UserSelectComponent } from '../../shared/ui/user-select.component';

/**
 * Session attendance workspace (HQMS M12): register trainees, hold the session, then record each
 * trainee's attendance and pre/post assessment scores (the effectiveness capture), and close it.
 * A standalone route so it can be linked to and deep-linked from the course workspace.
 */
@Component({
    selector: 'qams-session-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, RouterLink, PageHeaderComponent, StatusPillComponent, WorkflowStepperComponent, UserSelectComponent],
    template: `
    @if (session(); as s) {
      <qams-page-header [title]="s.sessionRef + ' — ' + s.courseTitle">
        <a [routerLink]="['/training-catalogue', s.courseId]" class="ghost-link">← {{ i18n.t('trn.backToCourse') }}</a>
      </qams-page-header>

      <qams-workflow-stepper [steps]="flowSteps" [current]="s.status" />

      <div class="grid">
        <section class="card">
          <div class="meta">
            <div><span class="muted">{{ i18n.t('trn.status') }}</span><qams-status-pill [status]="s.status" /></div>
            <div><span class="muted">{{ i18n.t('trn.scheduledAt') }}</span> {{ s.scheduledAtUtc | date:'medium' }}</div>
            <div><span class="muted">{{ i18n.t('trn.location') }}</span> {{ s.location }}</div>
            <div><span class="muted">{{ i18n.t('trn.trainer') }}</span> {{ s.trainerName }}</div>
            <div><span class="muted">{{ i18n.t('trn.passMark') }}</span> {{ s.passMark }}%</div>
          </div>

          <h3>{{ i18n.t('trn.attendance') }}</h3>
          @if (s.attendance.length === 0) { <p class="muted">{{ i18n.t('trn.noAttendees') }}</p> }
          @if (s.attendance.length > 0) {
            <table>
              <thead><tr>
                <th>{{ i18n.t('trn.trainee') }}</th><th>{{ i18n.t('trn.present') }}</th>
                <th>{{ i18n.t('trn.pre') }}</th><th>{{ i18n.t('trn.post') }}</th><th>{{ i18n.t('trn.gain') }}</th><th>{{ i18n.t('trn.result') }}</th>
              </tr></thead>
              <tbody>
                @for (a of s.attendance; track a.id) {
                  <tr>
                    <td class="code">{{ a.traineeId.slice(0, 8) }}</td>
                    <td>{{ a.attended ? '✓' : '—' }}</td>
                    <td>{{ a.preScore ?? '—' }}</td>
                    <td>{{ a.postScore ?? '—' }}</td>
                    <td [class.gain]="(a.scoreGain ?? 0) > 0">{{ a.scoreGain ?? '—' }}</td>
                    <td>@if (a.attended) { <span [class.pass]="a.passed" [class.fail]="!a.passed">{{ a.passed ? i18n.t('trn.passed') : i18n.t('trn.failed') }}</span> } @else { — }</td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </section>

        <section class="card actions">
          <h3>{{ i18n.t('trn.workflow') }}</h3>
          @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

          @if (s.status === 'Scheduled' || s.status === 'Held') {
            @if (perms.can('training.edit')) {
              <form [formGroup]="registerForm" (ngSubmit)="register(s.id)">
                <label>{{ i18n.t('trn.registerTrainee') }}</label>
                <qams-user-select formControlName="traineeId" />
                <button type="submit" [disabled]="registerForm.invalid">{{ i18n.t('trn.register') }}</button>
              </form>
            }
          }
          @if (s.status === 'Scheduled' && perms.can('training.edit')) {
            <button (click)="facade.hold(s.id)" [disabled]="facade.loading()">{{ i18n.t('trn.hold') }}</button>
          }

          @if (s.status === 'Held' && perms.can('training.edit')) {
            <form [formGroup]="attendanceForm" (ngSubmit)="record(s.id)">
              <h3>{{ i18n.t('trn.recordAttendance') }}</h3>
              <label>{{ i18n.t('trn.trainee') }}</label>
              <qams-user-select formControlName="traineeId" />
              <label class="chk"><input type="checkbox" formControlName="attended" /> {{ i18n.t('trn.present') }}</label>
              <label>{{ i18n.t('trn.pre') }}</label>
              <input type="number" min="0" max="100" formControlName="preScore" />
              <label>{{ i18n.t('trn.post') }}</label>
              <input type="number" min="0" max="100" formControlName="postScore" />
              <button type="submit" [disabled]="attendanceForm.invalid">{{ i18n.t('trn.record') }}</button>
            </form>
            <button class="secondary" (click)="facade.closeSession(s.id)" [disabled]="facade.loading()">{{ i18n.t('trn.closeSession') }}</button>
          }
          @if (s.status === 'Scheduled' || s.status === 'Held') {
            @if (perms.can('training.void')) {
              <button class="danger" (click)="facade.cancelSession(s.id)" [disabled]="facade.loading()">{{ i18n.t('trn.cancelSession') }}</button>
            }
          }
          @if (s.status === 'Closed' || s.status === 'Cancelled') { <p class="muted">{{ i18n.t('trn.sessionTerminal') }}</p> }
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
    .actions form { border-top: 1px solid var(--nt-border); padding-top: .75rem; margin-top: .75rem; }
    .actions button { margin-top: .5rem; margin-inline-end: .5rem; width: auto; }
    .chk { display: flex; align-items: center; gap: .4rem; margin: .5rem 0; }
    .chk input { width: auto; }
    .gain { color: var(--nt-ink-ok); font-weight: 700; }
    .pass { color: var(--nt-ink-ok); font-weight: 700; } .fail { color: var(--nt-ink-crit); font-weight: 700; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    h3 { margin-top: 1rem; }
    @media (max-width: 800px) { .grid { grid-template-columns: 1fr; } }
  `]
})
export class SessionDetailComponent implements OnInit {
  readonly facade = inject(TrainingFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  /** Route-bound session id (provided via withComponentInputBinding). */
  readonly id = input.required<string>();
  readonly session = this.facade.session;

  readonly flowSteps = ['Scheduled', 'Held', 'Closed'] as const;

  readonly registerForm = this.fb.nonNullable.group({
    traineeId: ['', [Validators.required]],
  });

  readonly attendanceForm = this.fb.nonNullable.group({
    traineeId: ['', [Validators.required]],
    attended: [true],
    preScore: this.fb.control<number | null>(null),
    postScore: this.fb.control<number | null>(null),
  });

  ngOnInit(): void {
    void this.facade.loadSession(this.id());
  }

  async register(id: string): Promise<void> {
    if (this.registerForm.invalid) { return; }
    await this.facade.register(id, { traineeId: this.registerForm.getRawValue().traineeId });
    if (this.facade.error() === '') { this.registerForm.reset({ traineeId: '' }); }
  }

  async record(id: string): Promise<void> {
    if (this.attendanceForm.invalid) { return; }
    const raw = this.attendanceForm.getRawValue();
    await this.facade.recordAttendance(id, {
      traineeId: raw.traineeId, attended: raw.attended,
      preScore: raw.preScore != null ? Number(raw.preScore) : null,
      postScore: raw.postScore != null ? Number(raw.postScore) : null,
    });
    if (this.facade.error() === '') { this.attendanceForm.reset({ traineeId: '', attended: true, preScore: null, postScore: null }); }
  }
}
