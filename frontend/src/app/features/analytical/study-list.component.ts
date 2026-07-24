import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterOutlet } from '@angular/router';
import { ValidationFacade } from './validation.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';

/** Method-validation study register: state-filterable list + configure form. */
@Component({
  selector: 'qams-study-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent],
  template: `
    <qams-page-header [title]="i18n.t('val.title')">
      <select [value]="stateFilter()" (change)="onFilter($event)" aria-label="State filter">
        <option value="">{{ i18n.t('nc.allStatuses') }}</option>
        @for (s of states; track s) { <option [value]="s">{{ s }}</option> }
      </select>
      @if (perms.canAssignTraining()) {
        <button (click)="showForm.set(!showForm())">{{ i18n.t('val.new') }}</button>
      }
    </qams-page-header>

    <qams-drawer [open]="showForm()" [title]="i18n.t('val.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="configure()">
        <div class="grid">
          <div><label>{{ i18n.t('qc.analyte') }}</label><input formControlName="analyte" /></div>
          <div><label>{{ i18n.t('val.protocol') }}</label><input formControlName="protocol" [placeholder]="i18n.t('val.protocolHint')" /></div>
          <div><label>{{ i18n.t('val.tea') }}</label><input type="number" step="any" min="0.000001" formControlName="totalAllowableError" /></div>
        </div>
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('val.configure') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.list().length === 0) {
      <p class="muted">{{ i18n.t('val.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('val.ref') }}</th><th>{{ i18n.t('qc.analyte') }}</th><th>{{ i18n.t('val.protocol') }}</th>
            <th>{{ i18n.t('nc.status') }}</th><th>{{ i18n.t('val.verdict') }}</th>
          </tr></thead>
          <tbody>
            @for (s of facade.list(); track s.id) {
              <tr class="clickable" (click)="open(s.id)">
                <td class="code">{{ s.studyRef }}</td><td>{{ s.analyte }}</td><td>{{ s.protocol }}</td>
                <td><qams-status-pill [status]="s.state" /></td>
                <td>
                  @if (s.passed === true) { <qams-status-pill status="Satisfactory" /> }
                  @else if (s.passed === false) { <qams-status-pill status="Failed" /> }
                  @else { — }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <!-- Record workspace: the routed detail renders in a wide drawer over the list. -->
    <qams-drawer [open]="detailOpen()" [title]="i18n.t('val.title')" width="920px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
  styles: [`
    .form { margin-bottom: 1rem; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(170px, 1fr)); gap: .5rem 1rem; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    button, select { width: auto; }
  `],
})
export class StudyListComponent implements OnInit {
  readonly facade = inject(ValidationFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly states = ['ProtocolConfigured', 'DataEntered', 'StatsCalculated', 'SignedOff'];
  readonly showForm = signal(false);
  /** Whether the record-workspace drawer (child route) is active. */
  readonly detailOpen = signal(false);
  readonly stateFilter = signal('');

  readonly form = this.fb.nonNullable.group({
    analyte: ['', [Validators.required, Validators.maxLength(200)]],
    protocol: ['', [Validators.required, Validators.maxLength(200)]],
    totalAllowableError: [10, [Validators.required, Validators.min(0.000001)]],
  });

  ngOnInit(): void { void this.facade.loadList(); }

  onFilter(event: Event): void {
    this.stateFilter.set((event.target as HTMLSelectElement).value);
    void this.facade.loadList(this.stateFilter() || undefined);
  }

  async configure(): Promise<void> {
    if (this.form.invalid) { return; }
    const id = await this.facade.configure(this.form.getRawValue());
    if (id) { this.cancel(); void this.router.navigate(['/validation-studies', id]); }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset({ totalAllowableError: 10 });
  }

  open(id: string): void { void this.router.navigate(['/validation-studies', id]); }

  /** Dismissing the workspace drawer returns to the plain list route. */
  closeDetail(): void { void this.router.navigate(['/validation-studies']); }
}
