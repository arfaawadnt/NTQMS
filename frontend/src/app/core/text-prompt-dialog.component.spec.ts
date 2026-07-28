import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TextPromptDialogComponent } from './text-prompt-dialog.component';
import { TextPromptService } from './text-prompt.service';

/**
 * R-4: the admin text/password prompt must be a real accessible dialog —
 * labelled, focus-managed, cancellable, masked for passwords — not a
 * window.prompt.
 */
describe('TextPromptDialogComponent', () => {
  let fixture: ComponentFixture<TextPromptDialogComponent>;
  let svc: TextPromptService;
  let host: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [TextPromptDialogComponent] }).compileComponents();
    fixture = TestBed.createComponent(TextPromptDialogComponent);
    svc = TestBed.inject(TextPromptService);
    host = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();
  });

  function requestPassword(): Promise<string | null> {
    const promise = svc.request({
      titleKey: 'users.resetPassword',
      labelKey: 'users.resetPrompt',
      inputType: 'password',
    });
    fixture.detectChanges();
    return promise;
  }

  function input(): HTMLInputElement {
    return host.querySelector('#text-prompt-input') as HTMLInputElement;
  }

  function confirmButton(): HTMLButtonElement {
    return host.querySelector('.row button:first-child') as HTMLButtonElement;
  }

  function cancelButton(): HTMLButtonElement {
    return host.querySelector('.row button.secondary') as HTMLButtonElement;
  }

  it('is hidden until requested, then opens as a labelled modal dialog', () => {
    expect(host.querySelector('[role="dialog"]')).toBeNull();

    void requestPassword();

    const dialog = host.querySelector('[role="dialog"]') as HTMLElement;
    expect(dialog).not.toBeNull();
    expect(dialog.getAttribute('aria-modal')).toBe('true');
    const labelledBy = dialog.getAttribute('aria-labelledby') as string;
    const title = host.querySelector(`#${labelledBy}`) as HTMLElement;
    expect(title.textContent?.trim()).toBe('Reset password');
  });

  it('masks the input when inputType is password and moves the initial focus to it', () => {
    void requestPassword();

    expect(input().type).toBe('password');
    expect(document.activeElement).toBe(input());
  });

  it('renders a plain text input by default', () => {
    void svc.request({ titleKey: 'users.resetPassword', labelKey: 'users.resetPrompt' });
    fixture.detectChanges();

    expect(input().type).toBe('text');
  });

  it('disables Confirm while the value is blank', () => {
    void requestPassword();

    expect(confirmButton().disabled).toBeTrue();

    input().value = '   ';
    input().dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(confirmButton().disabled).toBeTrue();

    input().value = 'Corr3ct-Horse-Battery!';
    input().dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(confirmButton().disabled).toBeFalse();
  });

  it('resolves the entered value on Confirm and closes', async () => {
    const promise = requestPassword();

    input().value = 'Corr3ct-Horse-Battery!';
    input().dispatchEvent(new Event('input'));
    fixture.detectChanges();
    confirmButton().click();
    fixture.detectChanges();

    await expectAsync(promise).toBeResolvedTo('Corr3ct-Horse-Battery!');
    expect(host.querySelector('[role="dialog"]')).toBeNull();
  });

  it('resolves null on Cancel', async () => {
    const promise = requestPassword();

    cancelButton().click();
    fixture.detectChanges();

    await expectAsync(promise).toBeResolvedTo(null);
    expect(host.querySelector('[role="dialog"]')).toBeNull();
  });

  it('resolves null when Escape is pressed', async () => {
    const promise = requestPassword();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();

    await expectAsync(promise).toBeResolvedTo(null);
  });
});
