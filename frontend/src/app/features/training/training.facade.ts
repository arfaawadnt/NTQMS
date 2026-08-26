import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { TrainingApiService } from '../../core/api/training-api.service';
import {
  CourseDetail, CourseListItem, DefineCourseRequest, RecordSessionAttendanceRequest, RegisterAttendeeRequest,
  ScheduleSessionRequest, SessionDetail, SessionListItem, TrainingComplianceRow, UpdateCourseRequest,
} from '../../core/models';

/**
 * Signal-based facade for Training management (HQMS M12). Holds the course catalogue, the
 * compliance dashboard, the selected course (with its sessions) and the selected session;
 * refreshes after each write.
 */
@Injectable({ providedIn: 'root' })
export class TrainingFacade {
  private readonly api = inject(TrainingApiService);

  private readonly _courses = signal<CourseListItem[]>([]);
  private readonly _compliance = signal<TrainingComplianceRow[]>([]);
  private readonly _course = signal<CourseDetail | null>(null);
  private readonly _courseSessions = signal<SessionListItem[]>([]);
  private readonly _session = signal<SessionDetail | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal('');

  readonly courses = this._courses.asReadonly();
  readonly compliance = this._compliance.asReadonly();
  readonly course = this._course.asReadonly();
  readonly courseSessions = this._courseSessions.asReadonly();
  readonly session = this._session.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  private lastCategory?: string;
  private lastStatus?: string;

  readonly activeCourses = computed(() => this._courses().filter((c) => c.status === 'Active').length);
  readonly meanPassRate = computed(() => {
    const rows = this._compliance().filter((r) => r.distinctTrainees > 0);
    if (rows.length === 0) { return 0; }
    return Math.round((rows.reduce((s, r) => s + r.passRate, 0) / rows.length) * 10) / 10;
  });

  async loadList(category?: string, status?: string): Promise<void> {
    this.lastCategory = category;
    this.lastStatus = status;
    await this.run(async () => {
      this._courses.set(await firstValueFrom(this.api.listCourses(category, status)));
      this._compliance.set(await firstValueFrom(this.api.compliance()));
    });
  }

  async loadCourse(id: string): Promise<void> {
    await this.run(async () => {
      this._course.set(await firstValueFrom(this.api.getCourse(id)));
      this._courseSessions.set(await firstValueFrom(this.api.listSessions(id)));
    });
  }

  async loadSession(id: string): Promise<void> {
    await this.run(async () => this._session.set(await firstValueFrom(this.api.getSession(id))));
  }

  async defineCourse(r: DefineCourseRequest): Promise<string | null> {
    return this.run(async () => (await firstValueFrom(this.api.defineCourse(r))).id);
  }

  async updateCourse(id: string, r: UpdateCourseRequest): Promise<void> { await this.courseMutate(id, () => this.api.updateCourse(id, r)); }
  async activateCourse(id: string): Promise<void> { await this.courseMutate(id, () => this.api.activateCourse(id)); }
  async retireCourse(id: string): Promise<void> { await this.courseMutate(id, () => this.api.retireCourse(id)); }

  async scheduleSession(r: ScheduleSessionRequest): Promise<string | null> {
    const id = await this.run(async () => (await firstValueFrom(this.api.scheduleSession(r))).id);
    if (id && this._course()) { this._courseSessions.set(await firstValueFrom(this.api.listSessions(this._course()!.id))); }
    return id;
  }

  async register(id: string, r: RegisterAttendeeRequest): Promise<void> { await this.sessionMutate(id, () => this.api.register(id, r)); }
  async hold(id: string): Promise<void> { await this.sessionMutate(id, () => this.api.hold(id)); }
  async recordAttendance(id: string, r: RecordSessionAttendanceRequest): Promise<void> { await this.sessionMutate(id, () => this.api.recordAttendance(id, r)); }
  async closeSession(id: string): Promise<void> { await this.sessionMutate(id, () => this.api.closeSession(id)); }
  async cancelSession(id: string): Promise<void> { await this.sessionMutate(id, () => this.api.cancelSession(id)); }

  private async courseMutate<T>(id: string, call: () => Observable<T>): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(call());
      this._course.set(await firstValueFrom(this.api.getCourse(id)));
    });
    if (this._error() === '') { await this.loadList(this.lastCategory, this.lastStatus); }
  }

  private async sessionMutate<T>(id: string, call: () => Observable<T>): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(call());
      this._session.set(await firstValueFrom(this.api.getSession(id)));
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
