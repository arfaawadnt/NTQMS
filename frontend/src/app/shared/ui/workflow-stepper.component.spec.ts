import { ComponentFixture, TestBed } from '@angular/core/testing';
import { WorkflowStepperComponent } from './workflow-stepper.component';

describe('WorkflowStepperComponent', () => {
  const NC_FLOW = ['Draft', 'Raised', 'Assigned', 'Rca', 'ActionPlan', 'PendingVerification', 'EffectivenessCheck', 'Closed'];
  let fixture: ComponentFixture<WorkflowStepperComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [WorkflowStepperComponent] }).compileComponents();
    fixture = TestBed.createComponent(WorkflowStepperComponent);
    fixture.componentRef.setInput('steps', NC_FLOW);
  });

  function setCurrent(status: string): WorkflowStepperComponent {
    fixture.componentRef.setInput('current', status);
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  it('marks the current status position on the canonical path', () => {
    const c = setCurrent('Assigned');
    expect(c.activeIndex()).toBe(2);
    expect(c.offPath()).toBeFalse();
  });

  it('renders one dot per step plus connectors', () => {
    setCurrent('Rca');
    const dots = fixture.nativeElement.querySelectorAll('.step .dot');
    expect(dots.length).toBe(NC_FLOW.length);
  });

  it('flags statuses outside the path as terminal off-path without claiming progress', () => {
    const c = setCurrent('Rejected');
    expect(c.offPath()).toBeTrue();
    expect(c.activeIndex()).toBe(0);
    const offBadge = fixture.nativeElement.querySelector('.step.off .lbl');
    expect(offBadge.textContent.trim()).toBe('Rejected');
  });

  it('prettifies camel-case statuses and upper-cases short acronyms', () => {
    const c = setCurrent('Draft');
    expect(c.pretty('PendingVerification')).toBe('Pending Verification');
    expect(c.pretty('Rca')).toBe('RCA');
  });
});
