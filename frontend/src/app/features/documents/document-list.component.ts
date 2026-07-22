import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { DocumentsFacade } from './documents.facade';
import { I18nService } from '../../core/i18n.service';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { StatusPillComponent } from '../../shared/ui/status-pill.component';

/** Controlled-document register: list + a create form that uploads the initial file. */
@Component({
  selector: 'qams-document-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePipe, PageHeaderComponent, StatusPillComponent],
  template: `
    <qams-page-header [title]="i18n.t('doc.title')">
      <button (click)="showForm.set(!showForm())">{{ i18n.t('doc.new') }}</button>
    </qams-page-header>

    @if (showForm()) {
      <form class="card form" [formGroup]="form" (ngSubmit)="create()">
        <div class="grid">
          <div>
            <label>{{ i18n.t('doc.code') }}</label>
            <input formControlName="code" placeholder="SOP-CAL-045" />
          </div>
          <div class="col-2">
            <label>{{ i18n.t('doc.docTitle') }}</label>
            <input formControlName="title" />
          </div>
          <div>
            <label>{{ i18n.t('doc.category') }}</label>
            <input formControlName="category" placeholder="SOP" />
          </div>
        </div>
        <label>{{ i18n.t('doc.changeSummary') }}</label>
        <input formControlName="changeSummary" />
        <label>{{ i18n.t('doc.file') }}</label>
        <input type="file" (change)="onFile($event)" />
        <div class="row">
          <button type="submit" [disabled]="form.invalid || !file() || facade.loading()">{{ i18n.t('doc.create') }}</button>
          <button type="button" class="secondary" (click)="cancel()">{{ i18n.t('nc.cancel') }}</button>
        </div>
        @if (facade.error()) { <div class="error">{{ facade.error() }}</div> }
      </form>
    }

    @if (facade.loading() && facade.list().length === 0) {
      <p class="muted">{{ i18n.t('common.loading') }}</p>
    } @else if (facade.list().length === 0) {
      <p class="muted">{{ i18n.t('doc.empty') }}</p>
    } @else {
      <div class="card">
        <table>
          <thead>
            <tr>
              <th>{{ i18n.t('doc.code') }}</th><th>{{ i18n.t('doc.docTitle') }}</th>
              <th>{{ i18n.t('doc.category') }}</th><th>{{ i18n.t('doc.published') }}</th>
              <th>{{ i18n.t('nc.status') }}</th><th>{{ i18n.t('doc.created') }}</th>
            </tr>
          </thead>
          <tbody>
            @for (d of facade.list(); track d.id) {
              <tr class="clickable" (click)="open(d.id)">
                <td>{{ d.code }}</td><td>{{ d.title }}</td><td>{{ d.category }}</td>
                <td>{{ d.publishedVersion ?? '—' }}</td>
                <td><qams-status-pill [status]="d.status" /></td>
                <td>{{ d.createdAtUtc | date:'mediumDate' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: [`
    .form { margin-bottom: 1rem; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: .5rem 1rem; }
    .col-2 { grid-column: span 2; }
    .row { display: flex; gap: .6rem; margin-top: 1rem; }
    .clickable { cursor: pointer; } .clickable:hover { background: #f4f6f9; }
    button { width: auto; }
  `],
})
export class DocumentListComponent implements OnInit {
  readonly facade = inject(DocumentsFacade);
  readonly i18n = inject(I18nService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly showForm = signal(false);
  readonly file = signal<File | null>(null);

  readonly form = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.maxLength(40)]],
    title: ['', [Validators.required, Validators.maxLength(300)]],
    category: ['SOP', [Validators.required, Validators.maxLength(50)]],
    changeSummary: ['Initial issue.', [Validators.maxLength(1000)]],
  });

  ngOnInit(): void { void this.facade.loadList(); }

  onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.file.set(input.files?.[0] ?? null);
  }

  async create(): Promise<void> {
    const selected = this.file();
    if (this.form.invalid || !selected) { return; }
    const id = await this.facade.create(selected, this.form.getRawValue());
    if (id) {
      this.cancel();
      void this.router.navigate(['/documents', id]);
    }
  }

  cancel(): void {
    this.showForm.set(false);
    this.file.set(null);
    this.form.reset({ category: 'SOP', changeSummary: 'Initial issue.' });
  }

  open(id: string): void { void this.router.navigate(['/documents', id]); }
}
