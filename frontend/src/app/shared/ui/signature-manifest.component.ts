import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { I18nService } from '../../core/i18n.service';
import { SignatureRecord } from '../../core/models';

/**
 * Renders the 21 CFR Part 11 §11.50 signature manifest for a single record — who
 * signed, what the signing meant, and when — directly on the signed record, as the
 * regulation requires. Presentational: the parent fetches the signatures (a record
 * viewer may read them) and passes them in. Emits nothing and holds no state; when
 * there are no signatures it renders nothing, so a parent can place it
 * unconditionally.
 */
@Component({
  selector: 'qams-signature-manifest',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe],
  template: `
    @if (signatures().length > 0) {
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
            @for (s of signatures(); track s.id) {
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

  /** The record's signatures (Part 11 §11.50), oldest-first as returned by the server. */
  readonly signatures = input.required<SignatureRecord[]>();
}
