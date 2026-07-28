import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { Router, RouterOutlet } from '@angular/router';
import { QcFacade } from './qc.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';

/** QC control profiles register + a create form (QM-gated). */
@Component({
    selector: 'qams-qc-profiles',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, DecimalPipe, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent],
    template: `
    <qams-page-header [title]="i18n.t('qc.title')" [subtitle]="i18n.t('qc.subtitle')">
      @if (perms.canApprove()) {
        <button (click)="showForm.set(!showForm())">{{ i18n.t('qc.new') }}</button>
      }
    </qams-page-header>

    <qams-drawer [open]="showForm()" [title]="i18n.t('qc.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="create()">
        <div class="grid">
          <div><label>{{ i18n.t('qc.analyte') }}</label><input formControlName="analyte" /></div>
          <div><label>{{ i18n.t('qc.instrument') }}</label><input formControlName="instrument" /></div>
          <div><label>{{ i18n.t('qc.lot') }}</label><input formControlName="controlLot" /></div>
          <div><label>{{ i18n.t('qc.mean') }}</label><input type="number" step="any" formControlName="targetMean" /></div>
          <div><label>{{ i18n.t('qc.sd') }}</label><input type="number" step="any" min="0.000001" formControlName="targetSd" /></div>
        </div>
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('qc.create') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.profiles().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.profiles().length === 0) {
      <p class="muted">{{ i18n.t('qc.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('qc.analyte') }}</th><th>{{ i18n.t('qc.instrument') }}</th><th>{{ i18n.t('qc.lot') }}</th>
            <th>{{ i18n.t('qc.mean') }}</th><th>{{ i18n.t('qc.sd') }}</th><th>{{ i18n.t('nc.status') }}</th>
          </tr></thead>
          <tbody>
            @for (p of facade.profiles(); track p.id) {
              <tr class="clickable" (click)="open(p.id)">
                <td>{{ p.analyte }}</td><td>{{ p.instrument }}</td><td class="code">{{ p.controlLot }}</td>
                <td>{{ p.targetMean | number:'1.0-4' }}</td><td>{{ p.targetSd | number:'1.0-4' }}</td>
                <td><qams-status-pill [status]="p.isActive ? 'Active' : 'Retired'" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <!-- Record workspace: the routed detail renders in a wide drawer over the list. -->
    <qams-drawer [open]="detailOpen()" [title]="i18n.t('qc.title')" width="920px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .form { margin-bottom: 1rem; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: .5rem 1rem; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    button { width: auto; }
  `]
})
export class QcProfilesComponent implements OnInit {
  readonly facade = inject(QcFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly showForm = signal(false);
  /** Whether the record-workspace drawer (child route) is active. */
  readonly detailOpen = signal(false);

  readonly form = this.fb.nonNullable.group({
    analyte: ['', [Validators.required, Validators.maxLength(200)]],
    instrument: ['', [Validators.required, Validators.maxLength(200)]],
    controlLot: ['', [Validators.required, Validators.maxLength(100)]],
    targetMean: [0, [Validators.required]],
    targetSd: [1, [Validators.required, Validators.min(0.000001)]],
  });

  ngOnInit(): void { void this.facade.loadProfiles(); }

  async create(): Promise<void> {
    if (this.form.invalid) { return; }
    const id = await this.facade.createProfile(this.form.getRawValue());
    if (id) { this.cancel(); void this.router.navigate(['/qc', id]); }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset({ targetMean: 0, targetSd: 1 });
  }

  open(id: string): void { void this.router.navigate(['/qc', id]); }

  /** Dismissing the workspace drawer returns to the plain list route. */
  closeDetail(): void { void this.router.navigate(['/qc']); }
}
