import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { ReviewFacade } from './review.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';

/** Management review list + a schedule form (QM-gated). */
@Component({
  selector: 'qams-review-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, PageHeaderComponent, DrawerComponent, StatusPillComponent],
  template: `
    <qams-page-header [title]="i18n.t('mrv.title')">
      @if (perms.canApprove()) {
        <button (click)="showForm.set(!showForm())">{{ i18n.t('mrv.new') }}</button>
      }
    </qams-page-header>

    <qams-drawer [open]="showForm()" [title]="i18n.t('mrv.new')" (closed)="cancel()">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="schedule()">
        <div class="grid">
          <div class="col-2"><label>{{ i18n.t('mrv.reviewTitle') }}</label><input formControlName="title" /></div>
          <div><label>{{ i18n.t('mrv.reviewDate') }}</label><input type="date" formControlName="reviewDate" /></div>
        </div>
        <label>{{ i18n.t('mrv.participants') }}</label>
        <textarea formControlName="participants" rows="2" [placeholder]="i18n.t('mrv.participantsHint')"></textarea>
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('mrv.schedule') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.list().length === 0) {
      <p class="muted">{{ i18n.t('mrv.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('mrv.ref') }}</th><th>{{ i18n.t('mrv.reviewTitle') }}</th><th>{{ i18n.t('mrv.reviewDate') }}</th>
            <th>{{ i18n.t('nc.status') }}</th><th>{{ i18n.t('mrv.decisions') }}</th>
          </tr></thead>
          <tbody>
            @for (r of facade.list(); track r.id) {
              <tr class="clickable" (click)="open(r.id)">
                <td>{{ r.reviewRef }}</td><td>{{ r.title }}</td><td>{{ r.reviewDate | date:'mediumDate' }}</td>
                <td><qams-status-pill [status]="r.status" /></td>
                <td>{{ r.decisionCount }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: [`
    .form { margin-bottom: 1rem; }
    .form textarea { width: 100%; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: .5rem 1rem; }
    .col-2 { grid-column: span 2; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    button { width: auto; }
  `],
})
export class ReviewListComponent implements OnInit {
  readonly facade = inject(ReviewFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly showForm = signal(false);

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    reviewDate: ['', [Validators.required]],
    participants: ['', [Validators.maxLength(2000)]],
  });

  ngOnInit(): void { void this.facade.loadList(); }

  async schedule(): Promise<void> {
    if (this.form.invalid) { return; }
    const id = await this.facade.schedule(this.form.getRawValue());
    if (id) { this.cancel(); void this.router.navigate(['/management-reviews', id]); }
  }

  cancel(): void {
    this.showForm.set(false);
    this.form.reset();
  }

  open(id: string): void { void this.router.navigate(['/management-reviews', id]); }
}
