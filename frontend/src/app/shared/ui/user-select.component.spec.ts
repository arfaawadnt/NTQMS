import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { UserSelectComponent } from './user-select.component';
import { OrgDataService } from '../../core/org-data.service';
import { UserDirectoryEntry } from '../../core/models';

describe('UserSelectComponent (searchable combobox)', () => {
  let fixture: ComponentFixture<UserSelectComponent>;

  const directory = signal<UserDirectoryEntry[]>([
    { id: 'u1', displayName: 'Amina QM', role: 'QualityManager' },
    { id: 'u2', displayName: 'Omar Analyst', role: 'Analyst' },
    { id: 'u3', displayName: 'Sara Head', role: 'DepartmentHead' },
  ]);

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UserSelectComponent],
      providers: [{
        provide: OrgDataService,
        useValue: {
          directory: directory.asReadonly(),
          ensureDirectory: () => Promise.resolve(),
          userName: (id: string | null) => directory().find((u) => u.id === id)?.displayName ?? '',
        },
      }],
    }).compileComponents();
    fixture = TestBed.createComponent(UserSelectComponent);
  });

  function openPanel(): void {
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('.trigger') as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  it('filters the directory as the user types (scales past 100 users)', () => {
    openPanel();
    fixture.componentInstance.query.set('omar');
    fixture.detectChanges();
    const rows = fixture.nativeElement.querySelectorAll('.opt .nm');
    expect(rows.length).toBe(1);
    expect(rows[0].textContent).toContain('Omar Analyst');
  });

  it('single mode: picking an option emits the id and closes the panel', () => {
    let emitted: string | string[] = '';
    fixture.componentInstance.registerOnChange((v) => { emitted = v; });
    openPanel();

    (fixture.nativeElement.querySelectorAll('.opt')[1] as HTMLElement).click();
    fixture.detectChanges();

    expect(emitted).toBe('u2');
    expect(fixture.componentInstance.open()).toBeFalse();
    expect(fixture.nativeElement.querySelector('.trigger').textContent).toContain('Omar Analyst');
  });

  it('multi mode: toggling options accumulates ids and renders removable tags', () => {
    fixture.componentRef.setInput('multiple', true);
    let emitted: string | string[] = [];
    fixture.componentInstance.registerOnChange((v) => { emitted = v; });
    openPanel();

    const options = fixture.nativeElement.querySelectorAll('.opt');
    (options[0] as HTMLElement).click();
    (options[2] as HTMLElement).click();
    fixture.detectChanges();

    expect(emitted).toEqual(['u1', 'u3']);
    expect(fixture.componentInstance.open()).toBeTrue(); // multi stays open
    const tags = fixture.nativeElement.querySelectorAll('.tag:not(.more)');
    expect(tags.length).toBe(2);
    expect(tags[0].textContent).toContain('Amina QM');

    // removing via the tag's ✕
    (tags[0].querySelector('.x') as HTMLElement).click();
    expect(emitted).toEqual(['u3']);
  });

  it('collapses overflowing selections into a +N counter', () => {
    fixture.componentRef.setInput('multiple', true);
    fixture.componentInstance.writeValue(['u1', 'u2', 'u3', 'u1x', 'u2x', 'u3x']);
    fixture.detectChanges();
    const more = fixture.nativeElement.querySelector('.tag.more');
    expect(more).toBeTruthy();
    expect(more.textContent.trim()).toBe('+2'); // 6 selected, 4 shown
  });

  it('restores form values via writeValue in both modes', () => {
    fixture.detectChanges();
    fixture.componentInstance.writeValue('u1');
    expect(fixture.componentInstance.single()).toBe('u1');

    fixture.componentRef.setInput('multiple', true);
    fixture.componentInstance.writeValue(['u1', 'u2']);
    expect(fixture.componentInstance.selected()).toEqual(['u1', 'u2']);
  });
});
