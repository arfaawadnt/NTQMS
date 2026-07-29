import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ListStat, ListStatsComponent } from './list-stats.component';

describe('ListStatsComponent', () => {
  let fixture: ComponentFixture<ListStatsComponent>;

  const stats: ListStat[] = [
    { label: 'Total', value: 12, tone: 'slate' },
    { label: 'Open', value: 5, tone: 'blue' },
    { label: 'Overdue', value: 2, tone: 'red' },
  ];

  /** Builds the component with the given inputs and renders it. */
  function render(input: readonly ListStat[], ratioFromFirst = false): ComponentFixture<ListStatsComponent> {
    const f = TestBed.createComponent(ListStatsComponent);
    f.componentRef.setInput('stats', input);
    f.componentRef.setInput('ratioFromFirst', ratioFromFirst);
    f.detectChanges();
    return f;
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ListStatsComponent] }).compileComponents();
    fixture = render(stats);
  });

  it('renders one tile per stat with its value and label', () => {
    const tiles = fixture.nativeElement.querySelectorAll('.stat');
    expect(tiles.length).toBe(3);
    expect(tiles[0].querySelector('.v').textContent.trim()).toBe('12');
    expect(tiles[0].querySelector('.l').textContent.trim()).toBe('Total');
  });

  it('applies the semantic tone class to each tile', () => {
    const tiles = fixture.nativeElement.querySelectorAll('.stat');
    expect(tiles[1].classList).toContain('blue');
    expect(tiles[2].classList).toContain('red');
  });

  it('shows no meter until a denominator is known', () => {
    // A wrong denominator is worse than none, so nothing is inferred by default.
    expect(fixture.nativeElement.querySelectorAll('.meter').length).toBe(0);
  });

  it('meters each part against the first tile when the page opts in', () => {
    const f = render(stats, true);
    const tiles = f.nativeElement.querySelectorAll('.stat');

    // The total IS the whole, so it gets no meter of its own.
    expect(tiles[0].querySelector('.meter')).toBeNull();
    expect(tiles[1].querySelector('.fill').style.width).toBe('42%');   // 5 of 12
    expect(tiles[2].querySelector('.fill').style.width).toBe('17%');   // 2 of 12
    expect(tiles[1].querySelector('.cap').textContent.replace(/\s/g, '')).toBe('5/12');
  });

  it('labels the meter for assistive technology', () => {
    const meter = render(stats, true).nativeElement.querySelectorAll('.stat')[1].querySelector('.meter');
    expect(meter.getAttribute('role')).toBe('img');
    expect(meter.getAttribute('aria-label')).toBe('Open: 5 of 12');
  });

  it('honours an explicit denominator over the first tile', () => {
    const f = render([
      { label: 'Total', value: 12, tone: 'slate' },
      { label: 'Signed off', value: 3, tone: 'green', of: 6 },
    ], true);

    const signedOff = f.nativeElement.querySelectorAll('.stat')[1];
    expect(signedOff.querySelector('.fill').style.width).toBe('50%');  // 3 of 6, not 3 of 12
    expect(signedOff.querySelector('.cap').textContent.replace(/\s/g, '')).toBe('3/6');
  });

  it('refuses to meter a value that is not a part of the whole', () => {
    // A subset larger than its total, or a non-numeric value, means the ratio is
    // meaningless — render the count alone rather than a misleading bar.
    const f = render([
      { label: 'Total', value: 4, tone: 'slate' },
      { label: 'Impossible', value: 9, tone: 'red' },
      { label: 'Text', value: 'n/a', tone: 'slate' },
    ], true);

    const tiles = f.nativeElement.querySelectorAll('.stat');
    expect(tiles[1].querySelector('.meter')).toBeNull();
    expect(tiles[2].querySelector('.meter')).toBeNull();
  });

  it('shows no meter when the whole is zero', () => {
    const f = render([
      { label: 'Total', value: 0, tone: 'slate' },
      { label: 'Open', value: 0, tone: 'blue' },
    ], true);

    expect(f.nativeElement.querySelectorAll('.meter').length).toBe(0);
  });

  it('de-emphasises a tile reading zero', () => {
    const f = render([
      { label: 'Total', value: 10, tone: 'slate' },
      { label: 'Overdue', value: 0, tone: 'red' },
    ], true);

    expect(f.nativeElement.querySelectorAll('.stat')[1].classList).toContain('zero');
  });
});
