import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { StandardsFacade } from './standards.facade';
import { StandardsApiService } from '../../core/api/standards-api.service';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import {
  COMPLIANCE_STATUSES, ComplianceStatus, EVIDENCE_SOURCE_TYPES, EvidenceLink, EvidenceSourceType, StandardElement,
} from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { AuditTrailComponent } from '../../shared/ui/audit-trail.component';

interface ChapterGroup { code: string; title: string; elements: StandardElement[]; }

/**
 * Standard-set workspace (HQMS M07): live readiness (overall + per-chapter), the
 * prioritised gap list, and the element register where each measurable element is
 * self-assessed and has evidence attached. Editing is privilege-gated (affordance only).
 */
@Component({
    selector: 'qams-standards-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DatePipe, DecimalPipe, RouterLink, PageHeaderComponent, StatusPillComponent, AuditTrailComponent],
    template: `
    @if (set(); as s) {
      <qams-page-header [title]="i18n.t('acr.fw.' + s.framework) + ' — ' + s.name + ' (' + s.version + ')'">
        <a routerLink="/standards" class="ghost-link">← {{ i18n.t('acr.backToList') }}</a>
      </qams-page-header>

      <div class="meta">
        <div><span class="muted">{{ i18n.t('acr.status') }}</span><qams-status-pill [status]="s.status" /></div>
        @if (s.status === 'Draft' && perms.can('standards.approve')) {
          <button (click)="facade.activate(s.id)">{{ i18n.t('acr.activate') }}</button>
        }
        @if (s.status !== 'Archived' && perms.can('standards.void')) {
          <button class="secondary" (click)="facade.archive(s.id)">{{ i18n.t('acr.archive') }}</button>
        }
      </div>
      @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }

      <!-- Readiness -->
      @if (facade.readiness(); as r) {
        <section class="card">
          <div class="readiness-head">
            <div class="overall">
              <span class="big" [class.ok]="r.overall.compliancePercent >= 90" [class.warn]="r.overall.compliancePercent < 90 && r.overall.compliancePercent >= 60" [class.bad]="r.overall.compliancePercent < 60">{{ r.overall.compliancePercent | number:'1.0-1' }}%</span>
              <span class="muted">{{ i18n.t('acr.overallReadiness') }}</span>
            </div>
            <div class="tallies">
              <span class="tag ok">{{ r.overall.compliantCount }} {{ i18n.t('acr.cs.Compliant') }}</span>
              <span class="tag warn">{{ r.overall.partialCount }} {{ i18n.t('acr.cs.PartiallyCompliant') }}</span>
              <span class="tag bad">{{ r.overall.nonCompliantCount }} {{ i18n.t('acr.cs.NonCompliant') }}</span>
              <span class="tag neutral">{{ r.overall.notAssessedCount }} {{ i18n.t('acr.cs.NotAssessed') }}</span>
            </div>
          </div>
          <div class="chapters">
            @for (c of r.chapters; track c.chapterCode) {
              <div class="chapter-row">
                <span class="cname">{{ c.chapterCode }} {{ c.chapterTitle }}</span>
                <div class="bar"><span [style.width.%]="c.compliancePercent"
                  [class.ok]="c.compliancePercent >= 90" [class.warn]="c.compliancePercent < 90 && c.compliancePercent >= 60" [class.bad]="c.compliancePercent < 60"></span></div>
                <span class="pct">{{ c.compliancePercent | number:'1.0-0' }}%</span>
              </div>
            }
          </div>
        </section>
      }

      <!-- Gap analysis -->
      @if (facade.gaps().length > 0) {
        <section class="card">
          <h3>{{ i18n.t('acr.gapAnalysis') }} ({{ facade.gaps().length }})</h3>
          <table>
            <thead><tr><th>{{ i18n.t('acr.element') }}</th><th>{{ i18n.t('acr.weight') }}</th><th>{{ i18n.t('acr.reason') }}</th></tr></thead>
            <tbody>
              @for (g of facade.gaps(); track g.elementId) {
                <tr><td><b>{{ g.elementCode }}</b> — {{ g.text }}</td><td>{{ g.weight }}</td><td class="muted">{{ g.reason }}</td></tr>
              }
            </tbody>
          </table>
        </section>
      }

      <!-- Add element (draft only) -->
      @if (s.status === 'Draft' && perms.can('standards.create')) {
        <section class="card">
          <h3>{{ i18n.t('acr.addElement') }}</h3>
          <form class="drawer-form" [formGroup]="elementForm" (ngSubmit)="addElement(s.id)">
            <div class="grid">
              <div><label>{{ i18n.t('acr.chapterCode') }}</label><input formControlName="chapterCode" /></div>
              <div class="col-2"><label>{{ i18n.t('acr.chapterTitle') }}</label><input formControlName="chapterTitle" /></div>
              <div><label>{{ i18n.t('acr.standardCode') }}</label><input formControlName="standardCode" /></div>
              <div><label>{{ i18n.t('acr.elementCode') }}</label><input formControlName="elementCode" /></div>
              <div><label>{{ i18n.t('acr.weight') }} (1-10)</label><input type="number" formControlName="weight" /></div>
            </div>
            <label>{{ i18n.t('acr.elementText') }}</label>
            <textarea rows="2" formControlName="text"></textarea>
            <button type="submit" [disabled]="elementForm.invalid">{{ i18n.t('acr.addElement') }}</button>
          </form>
        </section>
      }

      <!-- Elements grouped by chapter -->
      <section class="card">
        <h3>{{ i18n.t('acr.elements') }} ({{ s.elements.length }})</h3>
        @if (s.elements.length === 0) { <p class="muted">{{ i18n.t('acr.noElements') }}</p> }
        @for (ch of chapters(); track ch.code) {
          <h4>{{ ch.code }} {{ ch.title }}</h4>
          <table>
            <thead><tr>
              <th>{{ i18n.t('acr.element') }}</th><th>{{ i18n.t('acr.weight') }}</th>
              <th>{{ i18n.t('acr.assessment') }}</th><th>{{ i18n.t('acr.evidence') }}</th>
            </tr></thead>
            <tbody>
              @for (e of ch.elements; track e.id) {
                <tr>
                  <td><b>{{ e.elementCode }}</b> — {{ e.text }}</td>
                  <td>{{ e.weight }}</td>
                  <td>
                    @if (s.status === 'Active' && perms.can('standards.edit')) {
                      <select [value]="e.complianceStatus" (change)="assess(s.id, e.id, $event)">
                        @for (cs of statuses; track cs) { <option [value]="cs">{{ i18n.t('acr.cs.' + cs) }}</option> }
                      </select>
                    } @else { <qams-status-pill [status]="e.complianceStatus" /> }
                  </td>
                  <td>
                    <button class="link" (click)="toggleEvidence(e.id)">{{ e.evidenceCount }} · {{ i18n.t('acr.viewEvidence') }}</button>
                  </td>
                </tr>
                @if (expandedId() === e.id) {
                  <tr class="evidence-row"><td colspan="4">
                    @if (evidence().length === 0) { <p class="muted">{{ i18n.t('acr.noEvidence') }}</p> }
                    <ul class="evidence-list">
                      @for (ev of evidence(); track ev.id) {
                        <li><span class="tag neutral">{{ i18n.t('evd.' + ev.sourceType) }}</span> <b>{{ ev.sourceRef }}</b> @if (ev.description) { — {{ ev.description }} } <span class="muted">({{ ev.linkedAtUtc | date:'mediumDate' }})</span></li>
                      }
                    </ul>
                    @if (s.status !== 'Archived' && perms.can('standards.edit')) {
                      <form class="evidence-form" [formGroup]="evidenceForm" (ngSubmit)="linkEvidence(s.id, e.id)">
                        <select formControlName="sourceType">@for (t of sourceTypes; track t) { <option [value]="t">{{ i18n.t('evd.' + t) }}</option> }</select>
                        <input formControlName="sourceRef" [placeholder]="i18n.t('acr.sourceRef')" />
                        <input formControlName="description" [placeholder]="i18n.t('acr.evidenceDesc')" />
                        <button type="submit" [disabled]="evidenceForm.invalid">{{ i18n.t('acr.linkEvidence') }}</button>
                      </form>
                    }
                  </td></tr>
                }
              }
            </tbody>
          </table>
        }
      </section>

      <qams-audit-trail [subject]="s.id" />
    } @else {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    }
  `,
    styles: [`
    .meta { display: flex; flex-wrap: wrap; gap: 1rem; align-items: center; margin-bottom: 1rem; }
    .meta span.muted { display: block; font-size: .75rem; }
    .meta button { width: auto; }
    .readiness-head { display: flex; justify-content: space-between; flex-wrap: wrap; gap: 1rem; align-items: center; }
    .overall .big { font-size: 2.2rem; font-weight: 700; display: block; }
    .overall .big.ok { color: var(--nt-ink-ok); } .overall .big.warn { color: var(--nt-ink-warn); } .overall .big.bad { color: var(--nt-ink-crit); }
    .tallies { display: flex; gap: .5rem; flex-wrap: wrap; }
    .tag { font-size: .78rem; padding: .1rem .5rem; border-radius: 10px; }
    .tag.ok { background: var(--nt-ink-ok); color: #fff; } .tag.warn { background: var(--nt-ink-warn); color: #fff; }
    .tag.bad { background: var(--nt-ink-crit); color: #fff; } .tag.neutral { background: var(--nt-ink-neutral); color: #fff; }
    .chapters { margin-top: 1rem; display: grid; gap: .35rem; }
    .chapter-row { display: grid; grid-template-columns: 1fr 160px 44px; gap: .6rem; align-items: center; }
    .cname { font-size: .85rem; }
    .bar { height: 9px; background: #e6ebf1; border-radius: 5px; overflow: hidden; }
    .bar > span { display: block; height: 100%; background: var(--nt-ink-info); }
    .bar > span.ok { background: var(--nt-ink-ok); } .bar > span.warn { background: var(--nt-ink-warn); } .bar > span.bad { background: var(--nt-ink-crit); }
    .pct { font-size: .8rem; text-align: end; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: .5rem 1rem; }
    .col-2 { grid-column: span 2; }
    .evidence-row td { background: var(--nt-surface-alt, #f4f7fa); }
    .evidence-list { margin: .3rem 0; padding-inline-start: 1rem; }
    .evidence-form { display: flex; gap: .5rem; flex-wrap: wrap; margin-top: .4rem; }
    .ghost-link { color: var(--nt-blue); text-decoration: none; }
    select, button, input { width: auto; }
    h4 { margin-top: 1rem; }
  `]
})
export class StandardsDetailComponent implements OnInit {
  readonly facade = inject(StandardsFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(StandardsApiService);

  readonly id = input.required<string>();
  readonly set = this.facade.selected;
  readonly statuses = COMPLIANCE_STATUSES;
  readonly sourceTypes = EVIDENCE_SOURCE_TYPES;

  /** The element whose evidence panel is expanded, plus its loaded evidence. */
  readonly expandedId = signal<string | null>(null);
  readonly evidence = signal<EvidenceLink[]>([]);

  /** Elements grouped by chapter for the register. */
  readonly chapters = computed<ChapterGroup[]>(() => {
    const groups = new Map<string, ChapterGroup>();
    for (const e of this.set()?.elements ?? []) {
      const key = e.chapterCode || '—';
      const g = groups.get(key) ?? { code: key, title: e.chapterTitle, elements: [] };
      g.elements.push(e);
      groups.set(key, g);
    }
    return [...groups.values()].sort((a, b) => a.code.localeCompare(b.code));
  });

  readonly elementForm = this.fb.nonNullable.group({
    chapterCode: ['', [Validators.maxLength(40)]],
    chapterTitle: ['', [Validators.maxLength(300)]],
    standardCode: ['', [Validators.maxLength(40)]],
    elementCode: ['', [Validators.required, Validators.maxLength(40)]],
    text: ['', [Validators.required, Validators.maxLength(4000)]],
    weight: [1, [Validators.required, Validators.min(1), Validators.max(10)]],
  });

  readonly evidenceForm = this.fb.nonNullable.group({
    sourceType: ['Document' as EvidenceSourceType, [Validators.required]],
    sourceRef: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.maxLength(1000)]],
  });

  ngOnInit(): void {
    void this.facade.loadDetail(this.id());
  }

  async addElement(id: string): Promise<void> {
    if (this.elementForm.invalid) { return; }
    await this.facade.addElement(id, this.elementForm.getRawValue());
    if (this.facade.error() === '') { this.elementForm.reset({ weight: 1 }); }
  }

  assess(id: string, elementId: string, event: Event): void {
    const status = (event.target as HTMLSelectElement).value as ComplianceStatus;
    void this.facade.assess(id, elementId, { status, note: null });
  }

  async toggleEvidence(elementId: string): Promise<void> {
    if (this.expandedId() === elementId) { this.expandedId.set(null); return; }
    this.expandedId.set(elementId);
    this.evidence.set(await firstValueFrom(this.api.elementEvidence(elementId)));
  }

  async linkEvidence(id: string, elementId: string): Promise<void> {
    if (this.evidenceForm.invalid) { return; }
    const raw = this.evidenceForm.getRawValue();
    await this.facade.linkEvidence(id, {
      elementId, sourceType: raw.sourceType, sourceId: '00000000-0000-0000-0000-000000000000',
      sourceRef: raw.sourceRef, description: raw.description || null,
    });
    if (this.facade.error() === '') {
      this.evidenceForm.reset({ sourceType: 'Document' });
      this.evidence.set(await firstValueFrom(this.api.elementEvidence(elementId)));
    }
  }
}
