import { HELP_TOPICS, helpTopicForUrl } from './help-content';
import { NAV_ICONS } from '../nav-icons';
import { routes } from '../../app.routes';
import { Route } from '@angular/router';

/**
 * Keeps the user manual and the routed pages from drifting apart. A seven-page
 * documentation gap accumulated silently because nothing asserted parity — this
 * spec makes the audit self-enforcing.
 */
describe('help-content coverage', () => {
  /** Pages that deliberately carry no help topic. */
  const exempt = new Set([
    '/manual', // the manual documenting itself would be circular
  ]);

  /** Collects every shell-hosted page path from the real route table. */
  function routedPages(): string[] {
    const found: string[] = [];
    const walk = (rs: readonly Route[], prefix: string): void => {
      for (const r of rs) {
        if (r.path === undefined || r.path === '**' || r.redirectTo !== undefined) { continue; }
        const path = r.path === '' ? prefix : `${prefix}/${r.path}`;
        // A routed *page* loads a component; grouping nodes only carry children.
        if (r.loadComponent && path !== '') { found.push(path); }
        if (r.children) { walk(r.children, path); }
      }
    };
    // Skip the three out-of-shell routes (tenant entry, login, mfa-setup): they
    // render no page header, so the help system does not apply to them.
    const shell = routes.find((r) => r.path === '' && r.children !== undefined);
    walk(shell?.children ?? [], '');
    return found.filter((p) => !/:/.test(p)); // detail children inherit the parent topic
  }

  it('every routed page has a help topic or is explicitly exempt', () => {
    const undocumented = routedPages()
      .filter((p) => !exempt.has(p))
      .filter((p) => helpTopicForUrl(p) === undefined);
    expect(undocumented).toEqual([]);
  });

  it('every topic points at a routed page', () => {
    const pages = new Set(routedPages());
    const orphans = HELP_TOPICS.map((t) => t.route).filter((r) => !pages.has(r));
    expect(orphans).toEqual([]);
  });

  it('every topic icon exists in the registry', () => {
    const missing = HELP_TOPICS.map((t) => t.icon).filter((i) => !(i in NAV_ICONS));
    expect(missing).toEqual([]);
  });

  it('a multi-segment route resolves its own topic, not its first segment', () => {
    expect(helpTopicForUrl('/settings/security')?.route).toBe('/settings/security');
    expect(helpTopicForUrl('/platform/tenants')?.route).toBe('/platform/tenants');
  });

  it('a detail child still inherits its list page topic', () => {
    expect(helpTopicForUrl('/nonconformances/abc-123')?.route).toBe('/nonconformances');
  });

  it('no topic ships an empty usage list (it would render a bare heading)', () => {
    const empty = HELP_TOPICS.filter((t) => t.usage.length === 0).map((t) => t.route);
    expect(empty).toEqual([]);
  });
});
