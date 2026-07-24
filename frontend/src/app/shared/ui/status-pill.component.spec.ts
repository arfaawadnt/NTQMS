import { ComponentFixture, TestBed } from '@angular/core/testing';
import { StatusPillComponent } from './status-pill.component';

describe('StatusPillComponent', () => {
  let fixture: ComponentFixture<StatusPillComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [StatusPillComponent] }).compileComponents();
    fixture = TestBed.createComponent(StatusPillComponent);
  });

  function toneFor(status: string): string {
    fixture.componentRef.setInput('status', status);
    fixture.detectChanges();
    return fixture.componentInstance.tone();
  }

  it('maps terminal-positive states to ok', () => {
    expect(toneFor('Closed')).toBe('ok');
    expect(toneFor('Approved')).toBe('ok');
    expect(toneFor('Active')).toBe('ok');
  });

  it('maps verified states to teal per the design system', () => {
    expect(toneFor('Authorized')).toBe('teal');
    expect(toneFor('Published')).toBe('teal');
  });

  it('maps terminal-negative states to danger', () => {
    expect(toneFor('Rejected')).toBe('danger');
    expect(toneFor('Suspended')).toBe('danger');
    expect(toneFor('OutOfService')).toBe('danger');
  });

  it('treats anything else as in-progress (warn)', () => {
    expect(toneFor('Investigating')).toBe('warn');
    expect(toneFor('ActionPlan')).toBe('warn');
  });

  it('is case-insensitive on the backend status spelling', () => {
    expect(toneFor('CLOSED')).toBe('ok');
    expect(toneFor('rejected')).toBe('danger');
  });
});
