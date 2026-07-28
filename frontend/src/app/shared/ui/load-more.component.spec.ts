import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LoadMoreComponent } from './load-more.component';

/** R-3: the shared pager footer over the API-004 pagination envelope. */
describe('LoadMoreComponent', () => {
  let fixture: ComponentFixture<LoadMoreComponent>;
  let host: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [LoadMoreComponent] }).compileComponents();
    fixture = TestBed.createComponent(LoadMoreComponent);
    host = fixture.nativeElement as HTMLElement;
    fixture.componentRef.setInput('shown', 50);
    fixture.componentRef.setInput('total', 120);
    fixture.componentRef.setInput('hasMore', true);
    fixture.detectChanges();
  });

  function button(): HTMLButtonElement | null {
    return host.querySelector('button');
  }

  it('renders the interpolated "showing X of Y" count as a polite live region', () => {
    const count = host.querySelector('.count') as HTMLElement;
    expect(count.textContent?.trim()).toBe('Showing 50 of 120');
    expect(count.getAttribute('aria-live')).toBe('polite');
  });

  it('shows the Load-more button only while more pages exist', () => {
    expect(button()).not.toBeNull();

    fixture.componentRef.setInput('hasMore', false);
    fixture.detectChanges();
    expect(button()).toBeNull();
  });

  it('emits `more` when the button is clicked', () => {
    const emitted: void[] = [];
    fixture.componentInstance.more.subscribe(() => emitted.push(undefined));

    button()?.click();
    expect(emitted.length).toBe(1);
  });

  it('disables the button while a page fetch is in flight', () => {
    fixture.componentRef.setInput('loading', true);
    fixture.detectChanges();
    expect(button()?.disabled).toBeTrue();

    fixture.componentRef.setInput('loading', false);
    fixture.detectChanges();
    expect(button()?.disabled).toBeFalse();
  });
});
