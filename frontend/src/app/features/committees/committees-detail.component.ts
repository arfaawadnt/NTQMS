import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { CommitteesFacade } from './committees.facade';
import { I18nService } from '../../core/i18n.service';
import { OrgDataService } from '../../core/org-data.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { UserSelectComponent } from '../../shared/ui/user-select.component';

/**
 * Committee workspace (HQMS M17): terms of reference, membership and quorum, the open-action
 * follow-through register, and the committee's meetings. Editing is privilege-gated.
 */
@Component({
    selector: 'qams-committees-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, RouterLink, PageHeaderComponent, StatusPillComponent, UserSelectComponent],
    template: `
    @if (facade.committee(); as c) {
      <qams-page-header [title]="c.name">
        <a routerLink="/committees" class="ghost-link">← {{ i18n.t('cte.backToList') }}</a>
      </qams-page-header>

      <div class="meta">
        <div><span class="muted">{{ i18n.t('cte.status') }}</span><qams-status-pill [status]="c.status" /></div>
        <div><span class="muted">{{ i18n.t('cte.frequency') }}</span> {{ i18n.t('cte.fq.' + c.frequency) }}</div>
        <div><span class="muted">{{ i18n.t('cte.quorum') }}</span> {{ c.quorumSize }}</div>
        @if (c.status === 'Active' && perms.can('committees.void')) {
          <button class="secondary" (click)="facade.disband(c.id)">{{ i18n.t('cte.disband') }}</button>
        }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      <div class="grid">
        <section class="card">
          <h3>{{ i18n.t('cte.tor') }}</h3>
          <p>{{ c.termsOfReference }}</p>

          <h3>{{ i18n.t('cte.members') }}</h3>
          @if (c.members.length === 0) { <p class="muted">{{ i18n.t('cte.noMembers') }}</p> }
          @for (m of c.members; track m.id) {
            <div class="row-item">
              <span>{{ org.userName(m.userId) || m.userId }} <span class="muted">· {{ m.roleTitle }}</span></span>
              @if (c.status === 'Active' && perms.can('committees.edit')) {
                <button class="link danger-link" (click)="facade.removeMember(c.id, m.id)">{{ i18n.t('cte.remove') }}</button>
              }
            </div>
          }
          @if (c.status === 'Active' && perms.can('committees.edit')) {
            <form class="inline" [formGroup]="memberForm" (ngSubmit)="addMember(c.id)">
              <qams-user-select formControlName="userId" />
              <input formControlName="roleTitle" [placeholder]="i18n.t('cte.memberRole')" />
              <button type="submit" [disabled]="memberForm.invalid">{{ i18n.t('cte.addMember') }}</button>
            </form>
          }
        </section>

        <section class="card">
          <h3>{{ i18n.t('cte.openActions') }} ({{ facade.openActions().length }})</h3>
          @if (facade.openActions().length === 0) { <p class="muted">{{ i18n.t('cte.noOpenActions') }}</p> }
          @for (a of facade.openActions(); track a.decisionId) {
            <div class="row-item">
              <span>{{ a.description }}<br><span class="muted small">{{ a.meetingRef }} · {{ org.userName(a.ownerId) || '—' }} @if (a.dueDate) { · {{ a.dueDate | date:'mediumDate' }} }</span></span>
            </div>
          }
        </section>
      </div>

      <section class="card">
        <div class="mtg-head">
          <h3>{{ i18n.t('cte.meetings') }}</h3>
          @if (c.status === 'Active' && perms.can('committees.create')) {
            <form class="inline" [formGroup]="meetingForm" (ngSubmit)="schedule(c.id)">
              <input type="datetime-local" formControlName="scheduledAt" />
              <button type="submit" [disabled]="meetingForm.invalid">{{ i18n.t('cte.scheduleMeeting') }}</button>
            </form>
          }
        </div>
        @if (facade.meetings().length === 0) { <p class="muted">{{ i18n.t('cte.noMeetings') }}</p> }
        @else {
          <table>
            <thead><tr>
              <th>{{ i18n.t('cte.meetingRef') }}</th><th>{{ i18n.t('cte.scheduled') }}</th>
              <th>{{ i18n.t('cte.present') }}</th><th>{{ i18n.t('cte.openDecisions') }}</th><th>{{ i18n.t('cte.status') }}</th>
            </tr></thead>
            <tbody>
              @for (m of facade.meetings(); track m.id) {
                <tr class="clickable" (click)="openMeeting(m.id)">
                  <td class="code">{{ m.meetingRef }}</td>
                  <td>{{ m.scheduledAtUtc | date:'medium' }}</td>
                  <td>{{ m.presentCount }}</td>
                  <td [class.danger-text]="m.openDecisions > 0">{{ m.openDecisions }}</td>
                  <td><qams-status-pill [status]="m.status" /></td>
                </tr>
              }
            </tbody>
          </table>
        }
      </section>
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
    .mtg-head { display: flex; justify-content: space-between; flex-wrap: wrap; gap: .6rem; align-items: center; }
    .clickable { cursor: pointer; } .danger-text { color: var(--nt-ink-crit); font-weight: 700; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    select, button, input { width: auto; }
    @media (max-width: 800px) { .grid { grid-template-columns: 1fr; } }
  `]
})
export class CommitteesDetailComponent implements OnInit {
  readonly facade = inject(CommitteesFacade);
  readonly i18n = inject(I18nService);
  readonly org = inject(OrgDataService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly id = input.required<string>();

  readonly memberForm = this.fb.nonNullable.group({
    userId: ['', [Validators.required]],
    roleTitle: ['', [Validators.required, Validators.maxLength(100)]],
  });

  readonly meetingForm = this.fb.nonNullable.group({
    scheduledAt: ['', [Validators.required]],
  });

  ngOnInit(): void {
    void this.facade.loadCommittee(this.id());
    void this.org.ensureDirectory();
  }

  async addMember(id: string): Promise<void> {
    if (this.memberForm.invalid) { return; }
    await this.facade.addMember(id, this.memberForm.getRawValue());
    if (this.facade.error() === '') { this.memberForm.reset(); }
  }

  async schedule(id: string): Promise<void> {
    if (this.meetingForm.invalid) { return; }
    const at = new Date(this.meetingForm.getRawValue().scheduledAt).toISOString();
    const meetingId = await this.facade.scheduleMeeting({ committeeId: id, scheduledAtUtc: at });
    if (meetingId) {
      this.meetingForm.reset();
      void this.router.navigate(['/meetings', meetingId]);
    }
  }

  openMeeting(meetingId: string): void { void this.router.navigate(['/meetings', meetingId]); }
}
