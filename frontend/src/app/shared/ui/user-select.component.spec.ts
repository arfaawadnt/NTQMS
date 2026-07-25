import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { UserSelectComponent } from './user-select.component';
import { OrgDataService } from '../../core/org-data.service';
import { UserDirectoryEntry } from '../../core/models';

describe('UserSelectComponent', () => {
  let fixture: ComponentFixture<UserSelectComponent>;

  const directory = signal<UserDirectoryEntry[]>([
    { id: 'u1', displayName: 'Amina QM', role: 'QualityManager' },
    { id: 'u2', displayName: 'Omar Analyst', role: 'Analyst' },
  ]);

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UserSelectComponent],
      providers: [{
        provide: OrgDataService,
        useValue: { directory: directory.asReadonly(), ensureDirectory: () => Promise.resolve() },
      }],
    }).compileComponents();
    fixture = TestBed.createComponent(UserSelectComponent);
  });

  it('renders directory names, not GUIDs, in single mode', () => {
    fixture.detectChanges();
    const options = fixture.nativeElement.querySelectorAll('option');
    expect(options.length).toBe(3); // empty + 2 users
    expect(options[1].textContent).toContain('Amina QM');
  });

  it('propagates the picked user id through the ControlValueAccessor', () => {
    fixture.detectChanges();
    let emitted: string | string[] = '';
    fixture.componentInstance.registerOnChange((v) => { emitted = v; });

    const select: HTMLSelectElement = fixture.nativeElement.querySelector('select');
    select.value = 'u2';
    select.dispatchEvent(new Event('change'));

    expect(emitted).toBe('u2');
  });

  it('collects multiple ids as an array in multi mode', () => {
    fixture.componentRef.setInput('multiple', true);
    fixture.detectChanges();
    let emitted: string | string[] = [];
    fixture.componentInstance.registerOnChange((v) => { emitted = v; });

    fixture.componentInstance.toggle('u1');
    fixture.componentInstance.toggle('u2');
    expect(emitted).toEqual(['u1', 'u2']);

    fixture.componentInstance.toggle('u1');
    expect(emitted).toEqual(['u2']);
  });

  it('restores form values via writeValue', () => {
    fixture.detectChanges();
    fixture.componentInstance.writeValue('u1');
    expect(fixture.componentInstance.single()).toBe('u1');
  });
});
