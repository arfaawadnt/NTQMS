import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { I18nService } from '../../core/i18n.service';
import { SignatureRecord } from '../../core/models';

/**
 * Renders the 21 CFR Part 11 §11.50 signature manifest for a single record — who
 * signed, what the signing meant, and when — directly on the signed record, as the
 * regulation requires. Two modes:
 *  - presentational: the parent fetches and passes `[signatures]` (e.g. the NC facade);
 *  - self-fetching: the parent passes `[subjectUrl]` (a record viewer may read them) and
 *    the component GETs the manifest itself — used by the analytical study pages, which
 *    have no signatures signal on their facade.
 * When there are no signatures it renders nothing, so a parent can place it unconditionally.
 */
@Component({
  selector: 'qams-signature-manifest',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe],
  template: `
    @if (rows().length > 0) {
      <section class="card">
        <h3>{{ i18n.t('esign.manifest') }}</h3>
        <table>
          <thead>
            <tr>
              <th>{{ i18n.t('esign.signer') }}</th>
              <th>{{ i18n.t('esign.meaning') }}</th>
              <th>{{ i18n.t('esign.when') }}</th>
            </tr>
          </thead>
          <tbody>
            @for (s of rows(); track s.id) {
              <tr>
                <td><b>{{ s.signerDisplay }}</b></td>
                <td>{{ s.meaning }}</td>
                <td>{{ s.signedAtUtc | date:'medium' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </section>
    }
  `,
  styles: [`
    h3 { margin: 0 0 .5rem; }
    table { width: 100%; }
  `],
})
export class SignatureManifestComponent {
  readonly i18n = inject(I18nService);
  private readonly http = inject(HttpClient);

  /** Presentational mode: signatures passed in by the parent (oldest-first). */
  readonly signatures = input<SignatureRecord[]>([]);
  /** Self-fetching mode: a URL to GET the record's Part 11 §11.50 signatures from. */
  readonly subjectUrl = input<string>('');

  private readonly _fetched = signal<SignatureRecord[] | null>(null);

  /** Rows to render: the self-fetched manifest when a URL is given, else the passed-in input. */
  readonly rows = computed(() => this._fetched() ?? this.signatures());

  constructor() {
    effect(() => {
      const url = this.subjectUrl();
      if (url) {
        this.http.get<SignatureRecord[]>(url).subscribe((s) => this._fetched.set(s));
      }
    });
  }
}
