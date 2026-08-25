import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { CommitteesApiService } from '../../core/api/committees-api.service';
import {
  AddAgendaItemRequest, AddCommitteeMemberRequest, AddMeetingDecisionRequest, CommitteeDetail, CommitteeListItem,
  CreateCommitteeRequest, MeetingDetail, MeetingListItem, OpenAction, RecordAttendanceRequest, RecordMinutesRequest,
  ScheduleMeetingRequest,
} from '../../core/models';

/**
 * Signal-based facade for Committees & Governance (HQMS M17). Owns the committee register,
 * the loaded committee with its meetings and open actions, and the loaded meeting workspace,
 * refreshing the relevant view after each write.
 */
@Injectable({ providedIn: 'root' })
export class CommitteesFacade {
  private readonly api = inject(CommitteesApiService);

  private readonly _list = signal<CommitteeListItem[]>([]);
  private readonly _committee = signal<CommitteeDetail | null>(null);
  private readonly _meetings = signal<MeetingListItem[]>([]);
  private readonly _openActions = signal<OpenAction[]>([]);
  private readonly _meeting = signal<MeetingDetail | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly list = this._list.asReadonly();
  readonly committee = this._committee.asReadonly();
  readonly meetings = this._meetings.asReadonly();
  readonly openActions = this._openActions.asReadonly();
  readonly meeting = this._meeting.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  readonly activeCount = computed(() => this._list().filter((c) => c.status === 'Active').length);

  async loadList(status?: string): Promise<void> {
    await this.run(async () => this._list.set(await firstValueFrom(this.api.list(status))));
  }

  /** Loads a committee with its meetings and open actions together. */
  async loadCommittee(id: string): Promise<void> {
    await this.run(async () => {
      this._committee.set(await firstValueFrom(this.api.getById(id)));
      this._meetings.set(await firstValueFrom(this.api.committeeMeetings(id)));
      this._openActions.set(await firstValueFrom(this.api.openActions(id)));
    });
  }

  async loadMeeting(id: string): Promise<void> {
    await this.run(async () => this._meeting.set(await firstValueFrom(this.api.meeting(id))));
  }

  async create(request: CreateCommitteeRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.create(request))).id);
  }

  async addMember(id: string, r: AddCommitteeMemberRequest): Promise<void> { await this.refreshCommittee(id, () => this.api.addMember(id, r)); }
  async removeMember(id: string, memberId: string): Promise<void> { await this.refreshCommittee(id, () => this.api.removeMember(id, memberId)); }
  async updateQuorum(id: string, quorumSize: number): Promise<void> { await this.refreshCommittee(id, () => this.api.updateQuorum(id, quorumSize)); }
  async disband(id: string): Promise<void> { await this.refreshCommittee(id, () => this.api.disband(id)); }

  /** Schedules a meeting for the committee; returns the new meeting id. */
  async scheduleMeeting(request: ScheduleMeetingRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.schedule(request))).id);
  }

  async addAgenda(meetingId: string, r: AddAgendaItemRequest): Promise<void> { await this.refreshMeeting(meetingId, () => this.api.addAgenda(meetingId, r)); }
  async recordAttendance(meetingId: string, r: RecordAttendanceRequest): Promise<void> { await this.refreshMeeting(meetingId, () => this.api.recordAttendance(meetingId, r)); }
  async hold(meetingId: string): Promise<void> { await this.refreshMeeting(meetingId, () => this.api.hold(meetingId)); }
  async addDecision(meetingId: string, r: AddMeetingDecisionRequest): Promise<void> { await this.refreshMeeting(meetingId, () => this.api.addDecision(meetingId, r)); }
  async closeDecision(meetingId: string, decisionId: string, note: string | null): Promise<void> { await this.refreshMeeting(meetingId, () => this.api.closeDecision(meetingId, decisionId, note)); }
  async recordMinutes(meetingId: string, r: RecordMinutesRequest): Promise<void> { await this.refreshMeeting(meetingId, () => this.api.recordMinutes(meetingId, r)); }
  async approveMinutes(meetingId: string): Promise<void> { await this.refreshMeeting(meetingId, () => this.api.approveMinutes(meetingId)); }

  private async refreshCommittee<T>(id: string, call: () => Observable<T>): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(call());
      this._committee.set(await firstValueFrom(this.api.getById(id)));
      this._meetings.set(await firstValueFrom(this.api.committeeMeetings(id)));
      this._openActions.set(await firstValueFrom(this.api.openActions(id)));
    });
  }

  private async refreshMeeting<T>(meetingId: string, call: () => Observable<T>): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(call());
      this._meeting.set(await firstValueFrom(this.api.meeting(meetingId)));
    });
  }

  private async run<T>(operation: () => Promise<T>): Promise<T | null> {
    this._loading.set(true);
    this._error.set('');
    try {
      return await operation();
    } catch (err) {
      this._error.set(this.describe(err));
      return null;
    } finally {
      this._loading.set(false);
    }
  }

  private describe(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      return (err.error as { title?: string } | null)?.title ?? `Request failed (${err.status}).`;
    }
    return 'Unexpected error.';
  }
}
