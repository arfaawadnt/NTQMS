import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { QamsApiService } from '../../core/qams-api.service';
import { I18nService } from '../../core/i18n.service';
import { NcListItem } from '../../core/models';

@Component({
  selector: 'qams-nc-list',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="head">
      <h1>{{ i18n.t('nc.title') }}</h1>
      <button (click)="showForm.set(!showForm())">{{ i18n.t('nc.new') }}</button>
    </div>

    @if (showForm()) {
      <div class="card form">
        <div class="grid">
          <div>
            <label>{{ i18n.t('nc.subject') }}</label>
            <input [(ngModel)]="title" />
          </div>
          <div>
            <label>{{ i18n.t('nc.source') }}</label>
            <select [(ngModel)]="sourceType">
              <option value="Internal">Internal</option>
              <option value="Complaint">Complaint</option>
              <option value="Audit">Audit</option>
              <option value="Supplier">Supplier</option>
              <option value="ProficiencyTest">ProficiencyTest</option>
            </select>
          </div>
          <div>
            <label>{{ i18n.t('nc.severity') }} (1-5)</label>
            <input type="number" min="1" max="5" [(ngModel)]="severity" />
          </div>
          <div>
            <label>{{ i18n.t('nc.likelihood') }} (1-5)</label>
            <input type="number" min="1" max="5" [(ngModel)]="likelihood" />
          </div>
        </div>
        <label>{{ i18n.t('nc.description') }}</label>
        <textarea rows="3" [(ngModel)]="description"></textarea>
        <div class="row">
          <button (click)="create()" [disabled]="busy()">{{ i18n.t('nc.create') }}</button>
          <button class="secondary" (click)="showForm.set(false)">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (error()) { <div class="error">{{ error() }}</div> }
      </div>
    }

    @if (loading()) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (items().length === 0) {
      <p class="muted">{{ i18n.t('nc.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead>
            <tr>
              <th>{{ i18n.t('nc.ref') }}</th>
              <th>{{ i18n.t('nc.subject') }}</th>
              <th>{{ i18n.t('nc.status') }}</th>
              <th>{{ i18n.t('nc.severity') }}</th>
              <th>{{ i18n.t('nc.rpn') }}</th>
              <th>{{ i18n.t('nc.source') }}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (nc of items(); track nc.id) {
              <tr>
                <td>{{ nc.ncRef }}</td>
                <td>{{ nc.title }}</td>
                <td><span class="pill" [class.warn]="nc.status !== 'Closed' && nc.status !== 'Rejected'"
                                       [class.ok]="nc.status === 'Closed'">{{ nc.status }}</span></td>
                <td>{{ nc.severity }}</td>
                <td><span [class.danger-text]="nc.rpn > 12">{{ nc.rpn }}</span></td>
                <td>{{ nc.sourceType }}</td>
                <td>
                  @if (nc.status === 'Draft') {
                    <button class="ghost" (click)="submit(nc)">{{ i18n.t('nc.submit') }}</button>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: [`
    .head { display: flex; align-items: center; justify-content: space-between; }
    .form { margin-bottom: 1rem; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: .5rem 1rem; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .danger-text { color: var(--nt-danger); font-weight: 700; }
  `],
})
export class NcListComponent implements OnInit {
  readonly i18n = inject(I18nService);
  private readonly api = inject(QamsApiService);

  readonly items = signal<NcListItem[]>([]);
  readonly loading = signal(true);
  readonly showForm = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');

  title = '';
  description = '';
  severity = 3;
  likelihood = 3;
  sourceType = 'Internal';

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.api.listNcs().subscribe({
      next: (items) => { this.items.set(items); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  create(): void {
    this.error.set('');
    this.busy.set(true);
    this.api.raiseNc({
      title: this.title.trim(),
      description: this.description.trim(),
      severity: Number(this.severity),
      likelihood: Number(this.likelihood),
      sourceType: this.sourceType,
    }).subscribe({
      next: () => {
        this.busy.set(false);
        this.showForm.set(false);
        this.title = ''; this.description = '';
        this.load();
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(err.error?.title ?? 'Could not raise the nonconformance.');
      },
    });
  }

  submit(nc: NcListItem): void {
    this.api.submitNc(nc.id).subscribe({ next: () => this.load() });
  }
}
