import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { SurveysFacade } from './surveys.facade';
import { I18nService } from '../../core/i18n.service';
import { OrgDataService } from '../../core/org-data.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

/**
 * Satisfaction survey workspace (HQMS M11): question builder, live results (overall, by
 * domain, by question, by department), and internal response capture while open. Editing
 * is privilege-gated.
 */
@Component({
    selector: 'qams-surveys-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DecimalPipe, RouterLink, PageHeaderComponent, StatusPillComponent, AuditTrailComponent],
    template: `
    @if (facade.selected(); as s) {
      <qams-page-header [title]="s.title">
        <a routerLink="/surveys" class="ghost-link">← {{ i18n.t('svy.backToList') }}</a>
      </qams-page-header>

      <div class="meta">
        <div><span class="muted">{{ i18n.t('svy.status') }}</span><qams-status-pill [status]="s.status" /></div>
        @if (s.status === 'Draft' && perms.can('surveys.approve')) {
          <button (click)="facade.open(s.id)">{{ i18n.t('svy.open') }}</button>
        }
        @if (s.status === 'Open' && perms.can('surveys.void')) {
          <button class="secondary" (click)="facade.close(s.id)">{{ i18n.t('svy.close') }}</button>
        }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      @if (s.description) { <p class="muted">{{ s.description }}</p> }

      <!-- Results -->
      @if (facade.results(); as r) {
        <section class="card">
          <div class="res-head">
            <div><span class="big">{{ r.overallScore !== null ? (r.overallScore | number:'1.1-2') : '—' }}</span><span class="muted"> / 5 · {{ i18n.t('svy.overall') }}</span></div>
            <span class="muted">{{ r.responseCount }} {{ i18n.t('svy.responses') }}</span>
          </div>
          @if (r.responseCount > 0) {
            <h4>{{ i18n.t('svy.byDomain') }}</h4>
            @for (d of r.byDomain; track d.domain) {
              <div class="score-row"><span class="lbl">{{ d.domain }}</span>
                <div class="bar"><span [style.width.%]="d.meanScore / 5 * 100" [class.ok]="d.meanScore >= 4" [class.warn]="d.meanScore < 4 && d.meanScore >= 3" [class.bad]="d.meanScore < 3"></span></div>
                <span class="sc">{{ d.meanScore | number:'1.1-2' }}</span></div>
            }
            <h4>{{ i18n.t('svy.byDepartment') }}</h4>
            @for (dp of r.byDepartment; track dp.departmentId) {
              <div class="score-row"><span class="lbl">{{ org.departmentName(dp.departmentId) || i18n.t('svy.unattributed') }}</span>
                <div class="bar"><span [style.width.%]="dp.meanScore / 5 * 100" [class.ok]="dp.meanScore >= 4" [class.warn]="dp.meanScore < 4 && dp.meanScore >= 3" [class.bad]="dp.meanScore < 3"></span></div>
                <span class="sc">{{ dp.meanScore | number:'1.1-2' }} ({{ dp.responses }})</span></div>
            }
          } @else { <p class="muted">{{ i18n.t('svy.noResponses') }}</p> }
        </section>
      }

      <!-- Questions -->
      <section class="card">
        <h3>{{ i18n.t('svy.questions') }} ({{ s.questions.length }})</h3>
        @if (s.questions.length === 0) { <p class="muted">{{ i18n.t('svy.noQuestions') }}</p> }
        @for (q of s.questions; track q.id) {
          <div class="row-item"><span>{{ q.displayOrder }}. {{ q.text }}</span><span class="tag">{{ q.domain }}</span></div>
        }
        @if (s.status === 'Draft' && perms.can('surveys.create')) {
          <form class="inline" [formGroup]="questionForm" (ngSubmit)="addQuestion(s.id)">
            <input formControlName="text" [placeholder]="i18n.t('svy.questionText')" />
            <input formControlName="domain" [placeholder]="i18n.t('svy.domain')" />
            <button type="submit" [disabled]="questionForm.invalid">{{ i18n.t('svy.addQuestion') }}</button>
          </form>
        }
      </section>

      <!-- Record response -->
      @if (s.status === 'Open' && perms.can('surveys.create')) {
        <section class="card">
          <h3>{{ i18n.t('svy.recordResponse') }}</h3>
          <div class="resp-meta">
            <label>{{ i18n.t('svy.department') }}</label>
            <select #deptSel>
              <option value="">{{ i18n.t('svy.unattributed') }}</option>
              @for (d of org.departments(); track d.id) { <option [value]="d.id">{{ d.name }}</option> }
            </select>
          </div>
          @for (q of s.questions; track q.id) {
            <div class="resp-q">
              <span>{{ q.text }}</span>
              <select (change)="setScore(q.id, $event)">
                <option value="" selected>{{ i18n.t('svy.notAnswered') }}</option>
                @for (n of [1,2,3,4,5]; track n) { <option [value]="n">{{ n }}</option> }
              </select>
            </div>
          }
          <button (click)="submitResponse(s.id, deptSel.value)" [disabled]="answeredCount() === 0">{{ i18n.t('svy.submitResponse') }}</button>
        </section>
      }

      <qams-audit-trail [subject]="s.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1rem; align-items: center; margin-bottom: .5rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .meta button { width: auto; }
    .res-head { display: flex; justify-content: space-between; align-items: baseline; flex-wrap: wrap; gap: .5rem; }
    .res-head .big { font-size: 2rem; font-weight: 700; color: var(--nt-ink-info); }
    .score-row { display: grid; grid-template-columns: 1fr 160px 90px; gap: .6rem; align-items: center; margin: .25rem 0; font-size: .85rem; }
    .bar { height: 9px; background: #e6ebf1; border-radius: 5px; overflow: hidden; }
    .bar > span { display: block; height: 100%; background: var(--nt-ink-info); }
    .bar > span.ok { background: var(--nt-ink-ok); } .bar > span.warn { background: var(--nt-ink-warn); } .bar > span.bad { background: var(--nt-ink-crit); }
    .sc { text-align: end; }
    .row-item { padding: .4rem 0; border-bottom: 1px solid var(--nt-border); display: flex; justify-content: space-between; gap: 1rem; }
    .tag { font-size: .72rem; background: var(--nt-ink-neutral); color: #fff; padding: .05rem .5rem; border-radius: 8px; }
    .inline { display: flex; gap: .5rem; flex-wrap: wrap; margin-top: .6rem; }
    .resp-meta { margin-bottom: .5rem; }
    .resp-q { display: flex; justify-content: space-between; align-items: center; gap: 1rem; padding: .3rem 0; border-bottom: 1px solid var(--nt-border); }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    select, button, input { width: auto; }
    h4 { margin-top: 1rem; }
  `]
})
export class SurveysDetailComponent implements OnInit {
  readonly facade = inject(SurveysFacade);
  readonly i18n = inject(I18nService);
  readonly org = inject(OrgDataService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);

  readonly id = input.required<string>();

  /** Chosen score per question for the response-capture form (defaults to 3). */
  private readonly scores = signal<Record<string, number>>({});

  readonly questionForm = this.fb.nonNullable.group({
    text: ['', [Validators.required, Validators.maxLength(500)]],
    domain: ['', [Validators.maxLength(100)]],
  });

  ngOnInit(): void {
    void this.facade.loadDetail(this.id());
    void this.org.ensureOrg();
  }

  async addQuestion(id: string): Promise<void> {
    if (this.questionForm.invalid) { return; }
    const raw = this.questionForm.getRawValue();
    await this.facade.addQuestion(id, { text: raw.text, domain: raw.domain || 'General' });
    if (this.facade.error() === '') { this.questionForm.reset(); }
  }

  setScore(questionId: string, event: Event): void {
    const raw = (event.target as HTMLSelectElement).value;
    this.scores.update((m) => {
      const next = { ...m };
      if (raw === '') { delete next[questionId]; } else { next[questionId] = Number(raw); }
      return next;
    });
  }

  /** Questions the respondent has actually scored (N-09: no fabricated answers). */
  readonly answeredCount = computed(() =>
    (this.facade.selected()?.questions ?? []).filter((q) => this.scores()[q.id] != null).length);

  async submitResponse(id: string, departmentId: string): Promise<void> {
    const questions = this.facade.selected()?.questions ?? [];
    // N-09: submit only the questions the respondent actually scored — never a
    // fabricated midpoint for the ones they left blank.
    const answers = questions
      .filter((q) => this.scores()[q.id] != null)
      .map((q) => ({ questionId: q.id, score: this.scores()[q.id] }));
    if (answers.length === 0) { return; }
    await this.facade.submitResponse(id, {
      departmentId: departmentId || null, serviceLine: null, answers,
    });
    if (this.facade.error() === '') { this.scores.set({}); }
  }
}
