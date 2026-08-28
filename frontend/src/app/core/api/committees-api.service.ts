import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AddAgendaItemRequest, AddCommitteeMemberRequest, AddMeetingDecisionRequest, CommitteeDetail, CommitteeListItem,
  CreateCommitteeRequest, CreatedResource, MeetingDetail, MeetingListItem, OpenAction, RecordAttendanceRequest,
  RecordMinutesRequest, ScheduleMeetingRequest,
} from '../models';

/** Typed client for the Committees & Governance API (HQMS M17). */
@Injectable({ providedIn: 'root' })
export class CommitteesApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/committees`;
  private readonly meetings = `${environment.apiBaseUrl}/meetings`;

  list(status?: string): Observable<CommitteeListItem[]> {
    let params = new HttpParams();
    if (status) { params = params.set('status', status); }
    return this.http.get<CommitteeListItem[]>(this.base, { params });
  }

  getById(id: string): Observable<CommitteeDetail> { return this.http.get<CommitteeDetail>(`${this.base}/${id}`); }
  committeeMeetings(id: string): Observable<MeetingListItem[]> { return this.http.get<MeetingListItem[]>(`${this.base}/${id}/meetings`); }
  openActions(id: string): Observable<OpenAction[]> { return this.http.get<OpenAction[]>(`${this.base}/${id}/open-actions`); }
  create(body: CreateCommitteeRequest): Observable<CreatedResource> { return this.http.post<CreatedResource>(this.base, body); }
  addMember(id: string, body: AddCommitteeMemberRequest): Observable<{ memberId: string }> { return this.http.post<{ memberId: string }>(`${this.base}/${id}/members`, body); }
  removeMember(id: string, memberId: string): Observable<void> { return this.http.delete<void>(`${this.base}/${id}/members/${memberId}`); }
  updateQuorum(id: string, quorumSize: number): Observable<void> { return this.http.post<void>(`${this.base}/${id}/quorum`, { quorumSize }); }
  disband(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/disband`, {}); }

  // ── Meetings ──
  meeting(id: string): Observable<MeetingDetail> { return this.http.get<MeetingDetail>(`${this.meetings}/${id}`); }
  schedule(body: ScheduleMeetingRequest): Observable<CreatedResource> { return this.http.post<CreatedResource>(this.meetings, body); }
  addAgenda(id: string, body: AddAgendaItemRequest): Observable<{ itemId: string }> { return this.http.post<{ itemId: string }>(`${this.meetings}/${id}/agenda`, body); }
  recordAttendance(id: string, body: RecordAttendanceRequest): Observable<void> { return this.http.post<void>(`${this.meetings}/${id}/attendance`, body); }
  hold(id: string): Observable<void> { return this.http.post<void>(`${this.meetings}/${id}/hold`, {}); }
  addDecision(id: string, body: AddMeetingDecisionRequest): Observable<{ decisionId: string }> { return this.http.post<{ decisionId: string }>(`${this.meetings}/${id}/decisions`, body); }
  closeDecision(id: string, decisionId: string, note: string | null): Observable<void> { return this.http.post<void>(`${this.meetings}/${id}/decisions/${decisionId}/close`, { note }); }
  recordMinutes(id: string, body: RecordMinutesRequest): Observable<void> { return this.http.post<void>(`${this.meetings}/${id}/minutes`, body); }
  approveMinutes(id: string): Observable<void> { return this.http.post<void>(`${this.meetings}/${id}/approve-minutes`, {}); }
}
