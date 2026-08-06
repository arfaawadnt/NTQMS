import { ComponentFixture, TestBed } from '@angular/core/testing';
import { EsignCredentials, EsignDialogComponent } from './esign-dialog.component';

describe('EsignDialogComponent', () => {
  let fixture: ComponentFixture<EsignDialogComponent>;
  let component: EsignDialogComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [EsignDialogComponent] }).compileComponents();
    fixture = TestBed.createComponent(EsignDialogComponent);
    component = fixture.componentInstance;
  });

  function open(): HTMLElement {
    fixture.componentRef.setInput('open', true);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders nothing while closed', () => {
    fixture.componentRef.setInput('open', false);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('.modal')).toBeNull();
  });

  it('shows the modal and the attested meaning when open', () => {
    fixture.componentRef.setInput('meaning', 'I verify that the corrective action is effective.');
    const el = open();
    expect(el.querySelector('.modal[role="dialog"]')).not.toBeNull();
    expect(el.textContent).toContain('I verify that the corrective action is effective.');
  });

  it('withholds signing until BOTH identification components are present (§11.200(a)(1))', () => {
    open();
    expect(component.canConfirm()).toBeFalse();
    component.password.set('Sign-Pass-123');
    expect(component.canConfirm()).toBeFalse(); // password alone is not enough
    component.pin.set('2468');
    expect(component.canConfirm()).toBeTrue();
  });

  it('emits the captured credentials on confirm', () => {
    open();
    let emitted: EsignCredentials | undefined;
    component.confirm.subscribe((c) => (emitted = c));
    component.password.set('Sign-Pass-123');
    component.pin.set('2468');
    component.onConfirm();
    expect(emitted).toEqual({ password: 'Sign-Pass-123', pin: '2468' });
  });

  it('does not emit credentials while a signing is in flight', () => {
    fixture.componentRef.setInput('busy', true);
    open();
    let emitted = false;
    component.confirm.subscribe(() => (emitted = true));
    component.password.set('p');
    component.pin.set('1234');
    component.onConfirm();
    expect(emitted).toBeFalse();
  });

  it('emits cancel on dismiss', () => {
    open();
    let cancelled = false;
    component.cancel.subscribe(() => (cancelled = true));
    component.onCancel();
    expect(cancelled).toBeTrue();
  });
});
