import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterOutlet } from '@angular/router';
import { CommitteesFacade } from './committees.facade';
import { I18nService } from '../../core/i18n.service';
import { PermissionsService } from '../../core/permissions.service';
import { COMMITTEE_FREQUENCIES, CommitteeFrequency } from '../../core/models';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';
import { ListStat, ListStatsComponent } from '../../shared/ui/list-stats.component';

/** Committee register (HQMS M17): the standing governance committees. */
@Component({
    selector: 'qams-committees-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, PageHeaderComponent, DrawerComponent, RouterOutlet, StatusPillComponent, ListStatsComponent],
    template: `
    <qams-page-header [title]="i18n.t('cte.title')">
      @if (perms.can('committees.create')) {
        <button (click)="showForm.set(true)">{{ i18n.t('cte.new') }}</button>
      }
    </qams-page-header>

    <qams-list-stats [stats]="stats()" ratioFromFirst />

    <qams-drawer [open]="showForm()" [title]="i18n.t('cte.new')" (closed)="showForm.set(false)">
      <form class="drawer-form" [formGroup]="form" (ngSubmit)="create()">
        <label>{{ i18n.t('cte.name') }}</label>
        <input formControlName="name" />
        <label>{{ i18n.t('cte.tor') }}</label>
        <textarea rows="3" formControlName="termsOfReference"></textarea>
        <div class="grid">
          <div>
            <label>{{ i18n.t('cte.frequency') }}</label>
            <select formControlName="frequency">@for (f of frequencies; track f) { <option [value]="f">{{ i18n.t('cte.fq.' + f) }}</option> }</select>
          </div>
          <div><label>{{ i18n.t('cte.quorum') }}</label><input type="number" min="1" formControlName="quorumSize" /></div>
        </div>
        <div class="row">
          <button type="submit" [disabled]="form.invalid || facade.loading()">{{ i18n.t('cte.create') }}</button>
          <button type="button" class="secondary" (click)="showForm.set(false)">{{ i18n.t('common.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    </qams-drawer>

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.list().length === 0) {
      <p class="muted">{{ i18n.t('cte.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead><tr>
            <th>{{ i18n.t('cte.name') }}</th><th>{{ i18n.t('cte.frequency') }}</th>
            <th>{{ i18n.t('cte.quorum') }}</th><th>{{ i18n.t('cte.members') }}</th><th>{{ i18n.t('cte.status') }}</th>
          </tr></thead>
          <tbody>
            @for (c of facade.list(); track c.id) {
              <tr class="clickable" (click)="open(c.id)">
                <td>{{ c.name }}</td>
                <td>{{ i18n.t('cte.fq.' + c.frequency) }}</td>
                <td>{{ c.quorumSize }}</td>
                <td>{{ c.memberCount }}</td>
                <td><qams-status-pill [status]="c.status" /></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <qams-drawer [open]="detailOpen()" [title]="i18n.t('cte.title')" width="960px" (closed)="closeDetail()">
      <router-outlet (activate)="detailOpen.set(true)" (deactivate)="detailOpen.set(false)" />
    </qams-drawer>
  `,
    styles: [`
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: .5rem 1rem; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; }
    select, button { width: auto; }
  `]
})
export class CommitteesListComponent implements OnInit {
  readonly facade = inject(CommitteesFacade);
  readonly i18n = inject(I18nService);
  readonly perms = inject(PermissionsService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly frequencies = COMMITTEE_FREQUENCIES;
  readonly showForm = signal(false);
  readonly detailOpen = signal(false);

  readonly stats = computed<ListStat[]>(() => {
    const all = this.facade.list();
    return [
      { label: this.i18n.t('stat.total'), value: all.length, tone: 'slate' },
      { label: this.i18n.t('cte.stat.active'), value: all.filter((c) => c.status === 'Active').length, tone: 'blue' },
    ];
  });

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    termsOfReference: ['', [Validators.required, Validators.maxLength(4000)]],
    frequency: ['Monthly' as CommitteeFrequency, [Validators.required]],
    quorumSize: [3, [Validators.required, Validators.min(1)]],
  });

  ngOnInit(): void {
    void this.facade.loadList();
  }

  async create(): Promise<void> {
    if (this.form.invalid) { return; }
    const id = await this.facade.create(this.form.getRawValue());
    if (id) {
      this.showForm.set(false);
      this.form.reset({ frequency: 'Monthly', quorumSize: 3 });
      void this.router.navigate(['/committees', id]);
    }
  }

  open(id: string): void { void this.router.navigate(['/committees', id]); }
  closeDetail(): void { void this.router.navigate(['/committees']); }
}
