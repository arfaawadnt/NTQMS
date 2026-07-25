import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ListStat, ListStatsComponent } from './list-stats.component';

describe('ListStatsComponent', () => {
  let fixture: ComponentFixture<ListStatsComponent>;

  const stats: ListStat[] = [
    { label: 'Total', value: 12, tone: 'slate' },
    { label: 'Open', value: 5, tone: 'blue' },
    { label: 'Overdue', value: 2, tone: 'red' },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ListStatsComponent] }).compileComponents();
    fixture = TestBed.createComponent(ListStatsComponent);
    fixture.componentRef.setInput('stats', stats);
    fixture.detectChanges();
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
});
