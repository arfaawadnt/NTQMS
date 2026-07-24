import { ChangeDetectionStrategy, Component, HostListener, input, output } from '@angular/core';

/**
 * Right-side drawer (slide-over) per the QAMS Design System: a slate scrim
 * plus a panel that slides from the inline-end edge (left in RTL) with the
 * system's .26s ease curve. Closes on the scrim click, the header ✕, or Esc.
 * Content is projected into a scrollable body; the parent owns the open state
 * via [open] and reacts to (closed).
 */
@Component({
  selector: 'qams-drawer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="scrim" [class.open]="open()" (click)="closed.emit()" aria-hidden="true"></div>
    <aside class="drawer" [class.open]="open()" [style.width]="width()" role="dialog" aria-modal="true" [attr.aria-label]="title()">
      <div class="hd">
        <h3>{{ title() }}</h3>
        <button type="button" class="x" (click)="closed.emit()" aria-label="Close">✕</button>
      </div>
      <div class="bd">
        <ng-content />
      </div>
    </aside>
  `,
  styles: [`
    .scrim {
      position: fixed; inset: 0; background: rgba(59, 70, 88, .42); z-index: 200;
      opacity: 0; visibility: hidden; transition: opacity .2s;
    }
    .scrim.open { opacity: 1; visibility: visible; }
    .drawer {
      position: fixed; top: 0; inset-inline-end: 0; height: 100%; max-width: 96vw;
      background: var(--nt-surface); z-index: 210; box-shadow: var(--nt-shadow-pop);
      transform: translateX(110%); transition: transform .26s cubic-bezier(.4, 0, .2, 1);
      display: flex; flex-direction: column; visibility: hidden;
    }
    :host-context([dir="rtl"]) .drawer { transform: translateX(-110%); }
    .drawer.open, :host-context([dir="rtl"]) .drawer.open { transform: translateX(0); visibility: visible; }
    .hd {
      display: flex; align-items: center; gap: 12px; padding: 14px 20px;
      border-bottom: 1px solid var(--nt-border); flex-shrink: 0;
      border-top: 4px solid var(--nt-teal);
    }
    .hd h3 { margin: 0; flex: 1; font-size: 15px; font-weight: 700; color: var(--nt-slate); }
    .hd h3::before { content: none; }
    .x {
      background: transparent; border: none; color: var(--nt-grey-m); cursor: pointer;
      padding: 6px 10px; border-radius: 5px; font-size: 14px;
    }
    .x:hover { background: var(--nt-bg-grey); color: var(--nt-slate); }
    .bd { flex: 1; overflow-y: auto; padding: 18px 20px; }
  `],
})
export class DrawerComponent {
  /** Whether the drawer is visible (owned by the parent). */
  readonly open = input.required<boolean>();
  /** Header title. */
  readonly title = input.required<string>();
  /** CSS width of the panel (default suits forms; use larger for workspaces). */
  readonly width = input('560px');

  /** Emitted when the user dismisses via scrim, ✕, or Esc. */
  readonly closed = output<void>();

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open()) { this.closed.emit(); }
  }
}
