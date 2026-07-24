import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterOutlet } from '@angular/router';
import { RiskFacade } from './risk.facade';
import { I18nService } from '../../core/i18n.service';
import { HIGH_RESIDUAL_RPN_THRESHOLD, RISK_CATEGORIES } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';

/** Risk register: status-filterable list + an assess form (1-5 likelihood/impact). */
@Component({
  selector: 'qams-risk-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent],
  template: `
    <qams-page-header [title]="i18n.t('risk.title')">
      <select [value]="statusFilter()" (change)="onFilter($event)" aria-label="Status filter">
        <option value="">{{ i18n.t('nc.allStatuses') }}</option>
        @for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
      </select>
      <button (click)="showForm.set(!showForm())">{{ i18n.t('risk.new') }}</button>
    </qams-page-header>

    <qams-drawer [open]="showForm()" [title]="i18n.t('risk.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="assess()">
        <div class="grid">
          <div class="col-2"><label>{{ i18n.t('risk.riskTitle') }}</label><input formControlName="title" /></div>
          <div>
            <label>{{ i18n.t('risk.category') }}</label>
            <select formControlName="category">
              @for (c of categories; track c) { <option [value]="c">{{ c }}</option> }
            </select>
          </div>
          <div><label>{{ i18n.t('risk.likelihood') }}</label><input type="number" min="1" max="5" formControlName="likelihood" /></div>
          <div><label>{{ i18n.t('risk.impact') }}</label><input type="number" min="1" max="5" formControlName="impact" /></div>
        </div>
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('risk.assess') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.list().length === 0) {
      <p class="muted">{{ i18n.t('risk.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('risk.ref') }}</th><th>{{ i18n.t('risk.riskTitle') }}</th><th>{{ i18n.t('risk.category') }}</th>
            <th>{{ i18n.t('nc.status') }}</th><th>{{ i18n.t('risk.rpn') }}</th><th>{{ i18n.t('risk.residual') }}</th>
          </tr></thead>
          <tbody>
            @for (r of facade.list(); track r.id) {
              <tr class="clickable" (click)="open(r.id)">
                <td>{{ r.riskRef }}</td><td>{{ r.title }}</td><td>{{ r.category }}</td>
                <td><qams-status-pill [status]="r.status" /></td>
                <td><span class="rpn" [class.high]="r.rpn > threshold">{{ r.rpn }}</span></td>
                <td>
                  @if (r.residualRpn !== null) {
                    <span class="rpn" [class.high]="r.residualRpn > threshold">{{ r.residualRpn }}</span>
                  } @else { — }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <!-- Record workspace: the routed detail renders in a wide drawer over the list. -->
    <qams-drawer [open]="detailOpen()" [title]="i18n.t('risk.title')" width="920px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
  styles: [`
    .form { margin-bottom: 1rem; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: .5rem 1rem; }
    .col-2 { grid-column: span 2; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    .rpn { font-weight: 700; }
    .rpn.high { color: var(--nt-red, #b42318); }
    button, select { width: auto; }
  `],
})
export class RiskListComponent implements OnInit {
  readonly facade = inject(RiskFacade);
  readonly i18n = inject(I18nService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly statuses = ['Identified', 'Mitigating', 'Closed'];
  readonly categories = RISK_CATEGORIES;
  readonly threshold = HIGH_RESIDUAL_RPN_THRESHOLD;
  readonly showForm = signal(false);
  /** Whether the record-workspace drawer (child route) is active. */
  readonly detailOpen = signal(false);
  readonly statusFilter = signal('');

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    category: ['Operational', [Validators.required]],
    likelihood: [3, [Validators.required, Validators.min(1), Validators.max(5)]],
    impact: [3, [Validators.required, Validators.min(1), Validators.max(5)]],
  });

  ngOnInit(): void { void this.facade.loadList(); }

  onFilter(event: Event): void {
    this.statusFilter.set((event.target as HTMLSelectElement).value);
    void this.facade.loadList(this.statusFilter() || undefined);
  }

  async assess(): Promise<void> {
    if (this.form.invalid) { return; }
    const id = await this.facade.assess(this.form.getRawValue());
    if (id) { this.cancel(); void this.router.navigate(['/risks', id]); }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset({ category: 'Operational', likelihood: 3, impact: 3 });
  }

  open(id: string): void { void this.router.navigate(['/risks', id]); }

  /** Dismissing the workspace drawer returns to the plain list route. */
  closeDetail(): void { void this.router.navigate(['/risks']); }
}
