import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeReasonDialogComponent } from './change-reason-dialog.component';
import { ChangeReasonService } from './change-reason.service';

/**
 * UI-014: the Part 11 reason-for-change modal must be a real accessible
 * dialog — labelled, focus-managed, cancellable — not a window.prompt.
 */
describe('ChangeReasonDialogComponent', () => {
  let fixture: ComponentFixture<ChangeReasonDialogComponent>;
  let svc: ChangeReasonService;
  let host: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ChangeReasonDialogComponent] }).compileComponents();
    fixture = TestBed.createComponent(ChangeReasonDialogComponent);
    svc = TestBed.inject(ChangeReasonService);
    host = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();
  });

  function textarea(): HTMLTextAreaElement {
    return host.querySelector('textarea') as HTMLTextAreaElement;
  }

  function confirmButton(): HTMLButtonElement {
    return host.querySelector('.row button:first-child') as HTMLButtonElement;
  }

  function cancelButton(): HTMLButtonElement {
    return host.querySelector('.row button.secondary') as HTMLButtonElement;
  }

  it('is hidden until a reason is requested, then opens as a labelled modal dialog', () => {
    expect(host.querySelector('[role="dialog"]')).toBeNull();

    void svc.request();
    fixture.detectChanges();

    const dialog = host.querySelector('[role="dialog"]') as HTMLElement;
    expect(dialog).not.toBeNull();
    expect(dialog.getAttribute('aria-modal')).toBe('true');
    const labelledBy = dialog.getAttribute('aria-labelledby') as string;
    const title = host.querySelector(`#${labelledBy}`) as HTMLElement;
    expect(title.textContent?.trim()).toBeTruthy();
  });

  it('moves the initial focus to the reason textarea', () => {
    void svc.request();
    fixture.detectChanges();

    expect(document.activeElement).toBe(textarea());
  });

  it('disables Confirm while the reason is blank', () => {
    void svc.request();
    fixture.detectChanges();

    expect(confirmButton().disabled).toBeTrue();

    textarea().value = '   ';
    textarea().dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(confirmButton().disabled).toBeTrue();

    textarea().value = 'valid reason';
    textarea().dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(confirmButton().disabled).toBeFalse();
  });

  it('resolves the trimmed reason on Confirm and closes', async () => {
    const promise = svc.request();
    fixture.detectChanges();

    textarea().value = '  transcription error  ';
    textarea().dispatchEvent(new Event('input'));
    fixture.detectChanges();
    confirmButton().click();
    fixture.detectChanges();

    await expectAsync(promise).toBeResolvedTo('transcription error');
    expect(host.querySelector('[role="dialog"]')).toBeNull();
  });

  it('resolves null on Cancel', async () => {
    const promise = svc.request();
    fixture.detectChanges();

    cancelButton().click();
    fixture.detectChanges();

    await expectAsync(promise).toBeResolvedTo(null);
    expect(host.querySelector('[role="dialog"]')).toBeNull();
  });

  it('resolves null when Escape is pressed', async () => {
    const promise = svc.request();
    fixture.detectChanges();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();

    await expectAsync(promise).toBeResolvedTo(null);
  });

  it('shows the caller-supplied title key (legal-hold flow)', () => {
    void svc.request('arc.placeHold');
    fixture.detectChanges();

    const title = host.querySelector('#change-reason-title') as HTMLElement;
    expect(title.textContent?.trim()).toBe('Place hold');
  });
});
