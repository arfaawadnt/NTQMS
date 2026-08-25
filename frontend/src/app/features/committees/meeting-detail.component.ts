import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { CommitteesFacade } from './committees.facade';
import { I18nService } from '../../core/i18n.service';
import { OrgDataService } from '../../core/org-data.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { UserSelectComponent } from '../../shared/ui/user-select.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Committee meeting workspace (HQMS M17): agenda, attendance (and quorum), holding the
 * meeting, decisions/action items, and minutes with approval. Editing is privilege-gated.
 */
@Component({
    selector: 'qams-meeting-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, RouterLink, PageHeaderComponent, StatusPillComponent, UserSelectComponent, AuditTrailComponent],
    template: `
    @if (facade.meeting(); as m) {
      <qams-page-header [title]="m.meetingRef">
        <a [routerLink]="['/committees', m.committeeId]" class="ghost-link">← {{ i18n.t('mtg.backToCommittee') }}</a>
      </qams-page-header>

      <div class="meta">
        <div><span class="muted">{{ i18n.t('mtg.status') }}</span><qams-status-pill [status]="m.status" /></div>
        <div><span class="muted">{{ i18n.t('mtg.scheduled') }}</span> {{ m.scheduledAtUtc | date:'medium' }}</div>
        <div><span class="muted">{{ i18n.t('mtg.present') }}</span> {{ m.presentCount }}</div>
        @if (m.status === 'Scheduled' && perms.can('committees.approve')) {
          <button (click)="facade.hold(m.id)">{{ i18n.t('mtg.hold') }}</button>
        }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      <div class="grid">
        <!-- Agenda -->
        <section class="card">
          <h3>{{ i18n.t('mtg.agenda') }}</h3>
          @if (m.agenda.length === 0) { <p class="muted">{{ i18n.t('mtg.noAgenda') }}</p> }
          @for (a of m.agenda; track a.id) {
            <div class="row-item"><span>{{ a.title }} @if (a.carriedForward) { <span class="tag">{{ i18n.t('mtg.carriedForward') }}</span> } @if (a.sourceRef) { <span class="muted small">· {{ a.sourceRef }}</span> }<br>@if (a.detail) { <span class="muted small">{{ a.detail }}</span> }</span></div>
          }
          @if (m.status === 'Scheduled' && perms.can('committees.edit')) {
            <form class="inline" [formGroup]="agendaForm" (ngSubmit)="addAgenda(m.id)">
              <input formControlName="title" [placeholder]="i18n.t('mtg.agendaTitle')" />
              <input formControlName="sourceRef" [placeholder]="i18n.t('mtg.sourceRef')" />
              <button type="submit" [disabled]="agendaForm.invalid">{{ i18n.t('mtg.addAgenda') }}</button>
            </form>
          }
        </section>

        <!-- Attendance -->
        <section class="card">
          <h3>{{ i18n.t('mtg.attendance') }}</h3>
          @if (m.attendance.length === 0) { <p class="muted">{{ i18n.t('mtg.noAttendance') }}</p> }
          @for (at of m.attendance; track at.id) {
            <div class="row-item"><span>{{ org.userName(at.userId) || at.userId }}</span>
              <span [class.ok-text]="at.present" class="muted">{{ at.present ? i18n.t('mtg.present') : i18n.t('mtg.absent') }}</span></div>
          }
          @if (m.status === 'Scheduled' && perms.can('committees.edit')) {
            <form class="inline" [formGroup]="attendanceForm" (ngSubmit)="recordAttendance(m.id)">
              <qams-user-select formControlName="userId" />
              <label class="check"><input type="checkbox" formControlName="present" /> {{ i18n.t('mtg.present') }}</label>
              <button type="submit" [disabled]="attendanceForm.invalid">{{ i18n.t('mtg.record') }}</button>
            </form>
          }
        </section>
      </div>

      <!-- Decisions / action items -->
      <section class="card">
        <h3>{{ i18n.t('mtg.decisions') }}</h3>
        @if (m.decisions.length === 0) { <p class="muted">{{ i18n.t('mtg.noDecisions') }}</p> }
        @for (d of m.decisions; track d.id) {
          <div class="row-item">
            <span>{{ d.description }}<br><span class="muted small">{{ org.userName(d.ownerId) || '—' }} @if (d.dueDate) { · {{ d.dueDate | date:'mediumDate' }} } · <qams-status-pill [status]="d.status" /></span></span>
            @if (d.status === 'Open' && perms.can('committees.edit')) {
              <button class="link" (click)="facade.closeDecision(m.id, d.id, null)">{{ i18n.t('mtg.closeDecision') }}</button>
            }
          </div>
        }
        @if (m.status === 'Held' && perms.can('committees.edit')) {
          <form class="inline" [formGroup]="decisionForm" (ngSubmit)="addDecision(m.id)">
            <input formControlName="description" [placeholder]="i18n.t('mtg.decisionDesc')" />
            <qams-user-select formControlName="ownerId" />
            <input type="date" formControlName="dueDate" />
            <button type="submit" [disabled]="decisionForm.invalid">{{ i18n.t('mtg.addDecision') }}</button>
          </form>
        }
      </section>

      <!-- Minutes -->
      <section class="card">
        <h3>{{ i18n.t('mtg.minutes') }}</h3>
        @if (m.minutes) { <p>{{ m.minutes }}</p> } @else { <p class="muted">{{ i18n.t('mtg.noMinutes') }}</p> }
        @if (m.status === 'Held' && perms.can('committees.edit')) {
          <form [formGroup]="minutesForm" (ngSubmit)="recordMinutes(m.id)">
            <textarea rows="3" formControlName="minutes" [placeholder]="i18n.t('mtg.minutes')"></textarea>
            <button type="submit" [disabled]="minutesForm.invalid">{{ i18n.t('mtg.saveMinutes') }}</button>
          </form>
        }
        @if (m.status === 'Held' && m.minutes && perms.can('committees.approve')) {
          <button (click)="facade.approveMinutes(m.id)">{{ i18n.t('mtg.approveMinutes') }}</button>
        }
        @if (m.status === 'MinutesApproved') {
          <p class="ok-text">✓ {{ i18n.t('mtg.minutesApproved') }} — {{ org.userName(m.minutesApprovedBy) || '' }}</p>
        }
      </section>

      <qams-audit-trail [subject]="m.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1rem; align-items: center; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .meta button { width: auto; }
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; align-items: start; }
    .row-item { padding: .45rem 0; border-bottom: 1px solid var(--nt-border); display: flex; justify-content: space-between; gap: 1rem; }
    .inline { display: flex; gap: .5rem; flex-wrap: wrap; align-items: center; margin-top: .6rem; }
    .check { display: inline-flex; align-items: center; gap: .3rem; } .check input { width: auto; }
    .tag { font-size: .72rem; background: var(--nt-ink-neutral); color: #fff; padding: .05rem .4rem; border-radius: 8px; }
    .ok-text { color: var(--nt-ink-ok); }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    button, input, select { width: auto; }
    h3 { margin-top: 1rem; }
    @media (max-width: 800px) { .grid { grid-template-columns: 1fr; } }
  `]
})
export class MeetingDetailComponent implements OnInit {
  readonly facade = inject(CommitteesFacade);
  readonly i18n = inject(I18nService);
  readonly org = inject(OrgDataService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  readonly id = input.required<string>();

  readonly agendaForm = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(300)]],
    sourceRef: ['', [Validators.maxLength(120)]],
  });

  readonly attendanceForm = this.fb.nonNullable.group({
    userId: ['', [Validators.required]],
    present: [true],
  });

  readonly decisionForm = this.fb.nonNullable.group({
    description: ['', [Validators.required, Validators.maxLength(2000)]],
    ownerId: [''],
    dueDate: [''],
  });

  readonly minutesForm = this.fb.nonNullable.group({
    minutes: ['', [Validators.required, Validators.maxLength(20000)]],
  });

  ngOnInit(): void {
    void this.facade.loadMeeting(this.id());
    void this.org.ensureDirectory();
  }

  async addAgenda(id: string): Promise<void> {
    if (this.agendaForm.invalid) { return; }
    const raw = this.agendaForm.getRawValue();
    await this.facade.addAgenda(id, { title: raw.title, detail: null, sourceRef: raw.sourceRef || null, carriedForward: false });
    if (this.facade.error() === '') { this.agendaForm.reset(); }
  }

  async recordAttendance(id: string): Promise<void> {
    if (this.attendanceForm.invalid) { return; }
    const raw = this.attendanceForm.getRawValue();
    await this.facade.recordAttendance(id, { userId: raw.userId, present: raw.present });
    if (this.facade.error() === '') { this.attendanceForm.reset({ present: true }); }
  }

  async addDecision(id: string): Promise<void> {
    if (this.decisionForm.invalid) { return; }
    const raw = this.decisionForm.getRawValue();
    await this.facade.addDecision(id, {
      description: raw.description, ownerId: raw.ownerId || null, dueDate: raw.dueDate || null,
    });
    if (this.facade.error() === '') { this.decisionForm.reset(); }
  }

  async recordMinutes(id: string): Promise<void> {
    if (this.minutesForm.invalid) { return; }
    await this.facade.recordMinutes(id, { minutes: this.minutesForm.getRawValue().minutes });
    if (this.facade.error() === '') { this.minutesForm.reset(); }
  }
}
