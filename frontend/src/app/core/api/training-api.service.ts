import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CourseDetail, CourseListItem, CreatedResource, DefineCourseRequest, RecordSessionAttendanceRequest,
  RegisterAttendeeRequest, ScheduleSessionRequest, SessionDetail, SessionListItem,
  TrainingComplianceRow, UpdateCourseRequest,
} from '../models';

/** Typed client for the Training management API (HQMS M12). */
@Injectable({ providedIn: 'root' })
export class TrainingApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/training`;

  listCourses(category?: string, status?: string): Observable<CourseListItem[]> {
    const params = new URLSearchParams();
    if (category) { params.set('category', category); }
    if (status) { params.set('status', status); }
    return this.http.get<CourseListItem[]>(`${this.base}/courses?${params.toString()}`);
  }

  getCourse(id: string): Observable<CourseDetail> { return this.http.get<CourseDetail>(`${this.base}/courses/${id}`); }
  compliance(): Observable<TrainingComplianceRow[]> { return this.http.get<TrainingComplianceRow[]>(`${this.base}/compliance`); }
  defineCourse(body: DefineCourseRequest): Observable<CreatedResource> { return this.http.post<CreatedResource>(`${this.base}/courses`, body); }
  updateCourse(id: string, body: UpdateCourseRequest): Observable<void> { return this.http.put<void>(`${this.base}/courses/${id}`, body); }
  activateCourse(id: string): Observable<void> { return this.http.post<void>(`${this.base}/courses/${id}/activate`, {}); }
  retireCourse(id: string): Observable<void> { return this.http.post<void>(`${this.base}/courses/${id}/retire`, {}); }

  listSessions(courseId?: string, status?: string): Observable<SessionListItem[]> {
    const params = new URLSearchParams();
    if (courseId) { params.set('courseId', courseId); }
    if (status) { params.set('status', status); }
    return this.http.get<SessionListItem[]>(`${this.base}/sessions?${params.toString()}`);
  }

  getSession(id: string): Observable<SessionDetail> { return this.http.get<SessionDetail>(`${this.base}/sessions/${id}`); }
  scheduleSession(body: ScheduleSessionRequest): Observable<CreatedResource> { return this.http.post<CreatedResource>(`${this.base}/sessions`, body); }
  register(id: string, body: RegisterAttendeeRequest): Observable<void> { return this.http.post<void>(`${this.base}/sessions/${id}/attendees`, body); }
  hold(id: string): Observable<void> { return this.http.post<void>(`${this.base}/sessions/${id}/hold`, {}); }
  recordAttendance(id: string, body: RecordSessionAttendanceRequest): Observable<void> { return this.http.post<void>(`${this.base}/sessions/${id}/attendance`, body); }
  closeSession(id: string): Observable<void> { return this.http.post<void>(`${this.base}/sessions/${id}/close`, {}); }
  cancelSession(id: string): Observable<void> { return this.http.post<void>(`${this.base}/sessions/${id}/cancel`, {}); }
}
