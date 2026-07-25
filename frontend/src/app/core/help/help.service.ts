import { Injectable, signal } from '@angular/core';
import { HelpTopic } from './help-content';

/**
 * Holds the currently-open help topic for the global page-help popup. The
 * page-header ? icon calls open(); the drawer reads topic() and calls close().
 */
@Injectable({ providedIn: 'root' })
export class HelpService {
  private readonly _topic = signal<HelpTopic | null>(null);

  /** The topic shown in the popup, or null when closed. */
  readonly topic = this._topic.asReadonly();

  open(topic: HelpTopic): void { this._topic.set(topic); }

  close(): void { this._topic.set(null); }
}
