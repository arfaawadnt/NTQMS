import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { SignatureManifestComponent } from './signature-manifest.component';
import { SignatureRecord } from '../../core/models';

describe('SignatureManifestComponent', () => {
  let fixture: ComponentFixture<SignatureManifestComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SignatureManifestComponent],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    }).compileComponents();
    fixture = TestBed.createComponent(SignatureManifestComponent);
  });

  function render(signatures: SignatureRecord[]): HTMLElement {
    fixture.componentRef.setInput('signatures', signatures);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders nothing when there are no signatures (so a parent can place it unconditionally)', () => {
    expect(render([]).querySelector('section')).toBeNull();
  });

  it('renders a row per signature with the signer and meaning', () => {
    const sig: SignatureRecord = {
      id: 's1', tenantId: 't1', signerId: 'u1', signerDisplay: 'QM Lead',
      meaning: 'Verified corrective-action effectiveness on NC-2026-0001: passed',
      subjectRef: 'NC:abc', contentHash: 'deadbeef', signedAtUtc: '2026-08-06T10:00:00Z',
    };
    const el = render([sig]);

    expect(el.querySelector('section')).not.toBeNull();
    expect(el.querySelectorAll('tbody tr').length).toBe(1);
    expect(el.textContent).toContain('QM Lead');
    expect(el.textContent).toContain('passed');
  });
});
