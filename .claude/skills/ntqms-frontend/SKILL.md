---
name: ntqms-frontend
description: >-
  NT.QMS Angular 22 conventions — the exact shape of a feature page. Signal facades, standalone
  OnPush components with inline templates, typed API services over the Paged envelope, permission
  gating via PermissionsService, the trilingual EN/AR/FR dictionary with RTL, shared UI components
  (page-header, list-stats, drawer, load-more, status-pill), design tokens and the fill-vs-ink
  colour rule, lazy routes and nav registration, Karma and Playwright specs. Invoke before adding
  or changing anything under frontend/.
---

# NT.QMS — Frontend Conventions (Angular 22)

**Angular 22.0.8 · TypeScript ~6.0.3 · zone-based (not zoneless) · Karma/Jasmine + Playwright.**
Component prefix `qams`. There is **one** global stylesheet, `src/styles.css`. There are no SCSS
files, no component `.html`/`.css` files — every component is a single `.ts` with inline
`template:` and `styles: [...]`. Keep inline styles small: the budget is 8 kb warn / 16 kb error.

Strictness you must satisfy: `strict`, `strictTemplates`, `noPropertyAccessFromIndexSignature`,
`noImplicitReturns`. No `any` — the sanctioned escape hatch for DOM event values is
`$any($event.target).value`, and index-signature maps are read with brackets (`ICONS['dashboard']`).

## 1. Feature folder — flat, 3–4 files, no exceptions

```
src/app/features/<x>/
  <x>.facade.ts            signal store: all state + all API orchestration
  <x>-list.component.ts    the register page
  <x>-detail.component.ts  the record workspace (only if the feature has one)
  <x>.facade.spec.ts       Karma spec
```
No per-feature `*.routes.ts` (there are zero in the repo), no barrels, no per-feature models file,
no service other than the facade. DTOs all live in `core/models.ts`. Simple single-page features
(`roles`, `dashboard`) are one component with no facade — don't add one just for symmetry.

## 2. The facade — copy this shape verbatim

`@Injectable({ providedIn: 'root' })`, private writable signals with a leading underscore, public
`.asReadonly()` twins:
```ts
private readonly _list = signal<XListItem[]>([]);
private readonly _selected = signal<XDetail | null>(null);
private readonly _loading = signal(false);
private readonly _error = signal('');
readonly list = this._list.asReadonly();
readonly loading = this._loading.asReadonly();   // …error, selected, total, hasMore
```
Three helpers appear in **every** facade — reproduce them identically:
```ts
private async run<T>(operation: () => Promise<T>): Promise<T | null> {
  this._loading.set(true); this._error.set('');
  try { return await operation(); }
  catch (err) { this._error.set(this.describe(err)); return null; }
  finally { this._loading.set(false); }
}
private describe(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    return (err.error as { title?: string } | null)?.title ?? `Request failed (${err.status}).`;
  }
  return 'Unexpected error.';
}
private async mutate(id: string, call: () => Observable<void>): Promise<void> {
  await this.run(async () => {
    await firstValueFrom(call());
    this._selected.set(await firstValueFrom(this.api.getById(id)));
  });
}
```
Every public method is `async` returning `Promise<void>` (or `Promise<string | null>` for creates,
returning the new id). Observables are consumed with `firstValueFrom` — **a facade never exposes an
Observable**. The backend ProblemDetails `title` *is* the user-facing error string; server errors
are deliberately not translated.

**Pager** — only when the endpoint returns the envelope. Keep `_page`, `_total`, `_hasMore` and a
plain `lastStatus?` field that `loadMore()` reuses:
```ts
async loadMore(): Promise<void> {
  if (this._loading() || !this._hasMore()) { return; }
  await this.run(async () => {
    const next = this._page() + 1;
    const page = await firstValueFrom(this.api.list(this.lastStatus, next));
    this._page.set(next);
    this._list.update((items) => [...items, ...page.items]);
    this._total.set(page.total); this._hasMore.set(page.hasMore);
  });
}
```
Only 12 of 44 APIs are paged. **Do not invent a pager where the API returns a bare array.**

## 3. API service — thin, typed, stateless

One file per module in `core/api/`, `providedIn: 'root'`, one method per backend endpoint,
returning raw `Observable<T>`. **No `catchError`, no retry, no mapping** — errors propagate to the
facade's `describe()`.
```ts
private readonly base = `${environment.apiBaseUrl}/changes`;   // apiBaseUrl === '/api'

list(status?: string, page = 1, pageSize = DEFAULT_PAGE_SIZE): Observable<Paged<ChangeListItem>> {
  let params = new HttpParams().set('page', page).set('pageSize', pageSize);
  if (status) { params = params.set('status', status); }
  return this.http.get<Paged<ChangeListItem>>(this.base, { params });
}
approve(id: string): Observable<void> { return this.http.post<void>(`${this.base}/${id}/approve`, {}); }
```
Use `HttpParams`, not manual `URLSearchParams`. From `core/models.ts`:
`Paged<T> { items; total; page; pageSize; hasMore }`, `CreatedResource { id }`,
`DEFAULT_PAGE_SIZE = 50`. Uploads use `FormData` field name `file`; exports go through HttpClient
as a blob so the bearer applies, with the filename parsed from `Content-Disposition`.

## 4. Components

`ChangeDetectionStrategy.OnPush` is **mandatory** (96 of 100 components; the 4 exceptions are the
shell and login). Decorator key order: `selector`, `changeDetection`, `imports`, `template`,
`styles`. Feature components rely on the standalone default and just declare `imports`.

**Zero `@Input()`/`@Output()` decorators exist** — use `input.required<T>()`, `input<T>(default)`,
`output<T>()`. A routed detail component receives `:id` as a signal input
(`readonly id = input.required<string>()`), enabled globally by `withComponentInputBinding()`.

```ts
readonly facade = inject(XFacade);        // public: templates read facade.loading() directly
readonly i18n = inject(I18nService);
readonly perms = inject(PermissionsService);
readonly org = inject(OrgDataService);
private readonly fb = inject(FormBuilder);

readonly showForm = signal(false);
readonly statusFilter = signal('');
readonly filtered = computed(() => /* client-side filter over facade.list() */);
readonly stats = computed<ListStat[]>(() => [...]);
readonly form = this.fb.nonNullable.group({
  title: ['', [Validators.required, Validators.maxLength(200)]],   // mirror the backend validator
});
ngOnInit(): void { void this.facade.loadList(); void this.org.ensureOrg(); }
```
Always `fb.nonNullable.group`, never `new FormGroup`. Fire-and-forget async is prefixed `void`.
**`effect()` is not used in feature components** — only in `app.component.ts`,
`permissions.service.ts` and `change-reason-dialog.component.ts`. Server-side filter ⇒ re-`loadList()`;
client-side filter ⇒ `computed`.

Control flow is `@if / @else if / @for (… ; track …)`. `*ngIf`/`*ngFor` and `CommonModule` are
never imported.

**List page template order** (fixed across all 30 register pages):
1. `<qams-page-header [title]="i18n.t('x.title')">` with action buttons projected as content
2. `<qams-list-stats [stats]="stats()" ratioFromFirst />`
3. `<div class="filterbar card">` — search input + selects, each with an `aria-label`
4. `<qams-drawer [open]="showForm()" …>` wrapping `<form class="drawer-form" [formGroup]="form">`
5. loading / empty / `<div class="card"><table>…</table></div>` + `<qams-load-more … />`
6. `<qams-drawer [open]="detailOpen()" width="920px"><router-outlet (activate)="detailOpen.set(true)"
   (deactivate)="detailOpen.set(false)" /></qams-drawer>`

## 5. Permissions — affordance only

```ts
perms.can('changes.approve')                       // PlatformAdmin ⇒ always true
perms.canAny('changes.approve', 'changes.void')    // the standard approve/reject pair-gate
perms.roleName() · perms.branchIds() · perms.isPlatformAdmin()
```
Keys are `<module>.<action>`, lower-case kebab module + one of
view/create/edit/approve/sign/void/export/manage. Privileges load from
`GET /api/auth/me/privileges` via a constructor `effect()`; **before it lands `can()` returns
false by design** — a button appearing a beat late is cosmetic, a button appearing wrongly is a
broken promise.

Gate in the **template**, and show a denied user an explanation rather than a silent gap:
```html
@if (perms.canAny('changes.approve', 'changes.void')) { … }
@else { <p class="muted">{{ i18n.t('chg.approverOnly') }}</p> }
```
Nav items take an optional `visible: () => this.perms.can('users.view')`; empty groups disappear.
Route guards are only `authGuard`, `platformOnlyGuard`, `tenantOnlyGuard` — **do not add a
permission-key route guard.** This is affordance; the server re-enforces every call.

## 6. i18n and RTL

One in-memory dictionary in `core/i18n.service.ts` — no JSON files, no `@angular/localize`, no
pipe. `type Lang = 'en' | 'ar' | 'fr'`; `Dict = Record<string, { en; ar; fr }>`; **all three
languages are mandatory** for every key (a missing key returns the key itself).

Templates call the method: `{{ i18n.t('chg.title') }}`, `[placeholder]="i18n.t('common.search')"`,
`[attr.aria-label]="…"`. Key naming is `<shortModulePrefix>.<camelCaseThing>`; shared prefixes are
`nav.*`, `common.*`, `stat.*`, `perm.*`, `trail.*`, `help.*`, `changeReason.*`. Acronyms (NC, CAPA,
SOP, QC, PT, RPN) stay untranslated. Placeholders interpolate manually:
`.replace('{shown}', …)`.

RTL is applied once globally by an `effect()` in `AppComponent` setting
`document.documentElement.dir`. **Component CSS must be direction-agnostic** — use logical
properties (`margin-inline-start`, `inset-inline-end`, `text-align: start`). Where a transform is
unavoidable use `:host-context([dir="rtl"])`.

## 7. Shared UI — use these, don't re-implement

`page-header` (title/subtitle + action slot; auto-renders the `?` help button) · `list-stats` ·
`load-more` · `drawer` (Esc-closable, `role="dialog" aria-modal="true"`) · `status-pill` ·
`workflow-stepper` · `audit-trail` (self-fetching, gated on `compliance.view`) ·
`allocation-picker` (cascading branch→department) · `user-select` and `lov-select` (both
`ControlValueAccessor`) · `csv-import` · `page-help` · `help-body`.

`ListStat` is `{ label; value; tone: 'blue'|'teal'|'green'|'gold'|'orange'|'red'|'slate'; of?; link? }`.
`ratioFromFirst` is written as a bare attribute and asserts "tile[0] is the register total". A meter
renders only when the value is numeric and `0 <= value <= whole`.

**Dialogs:** `window.prompt`/`confirm` are **banned** (EA UI-014 / R-4). Use
`core/text-prompt.service.ts`. You rarely need the change-reason dialog explicitly — the global
`change-reason.interceptor` opens it automatically for **any HTTP DELETE** lacking
`X-Change-Reason`, and cancelling returns `EMPTY` so nothing is sent.

`OrgDataService` caches branches, departments, the user directory and LOV entries per session:
`org.ensureOrg()` in `ngOnInit`, then `org.branchName(id)`, `org.lovEntries('RISK_CATEGORY')`.

## 8. Design tokens — the one rule that matters

All `--nt-*` tokens are on `:root` in `styles.css`. Brand `--nt-blue #0077C2`, `--nt-navy #1E3A5F`,
`--nt-teal #00B2A9`.

**Fill tokens are not text colours.** As a categorical set the semantic tones fail contrast (gold
1.80:1, teal 2.58:1 on white). Anything that must be *read* — a statistic, a meter fill, coloured
text — uses the ink ramp, each ≥4.5:1 on `--nt-surface`:
`--nt-ink-info #00639E` · `--nt-ink-teal #00706A` · `--nt-ink-ok #12631F` · `--nt-ink-warn #8A5A00` ·
`--nt-ink-serious #B4430F` · `--nt-ink-crit #B3202D` · `--nt-ink-neutral #4A5768`.
So `--nt-red` for a border accent, a pill tint or a solid button; `--nt-ink-crit` for a number or a
label. Never use a saturated tone as a tile background, and **never carry severity by colour
alone** — pair it with a caption.

Global styles already cover `button` (+`.secondary/.ghost/.danger/.link`), inputs, `.card`,
`table`, `.pill`, `.error`, `.muted`, `.helper`, `.code`. Component `styles:[]` should add layout
only — including `button, select { width: auto; }`, needed because global inputs are `width:100%`.
Headings are Title Case (`text-transform: none` is deliberate).

**There is no dark mode** in the authenticated app — zero `prefers-color-scheme` hits, and the
tokens have no dark variants. The only dark styling is a page-local `.dark` class on the sign-in
page. Do not add dark-mode rules to a feature page.

## 9. Routing and registration

`app.routes.ts` is one flat array. **Always `loadComponent` with `.then(m => m.XComponent)`** —
never `loadChildren`, never an eager reference, never a feature routes file. A detail page is
always a `:id` **child** of its list route; that is what makes the drawer workspace work. Paths are
lower-case kebab plurals.

## 10. The exact files a new page must touch

1. `core/models.ts` — DTOs mirroring `NT.QAMS.Contracts`
2. `core/api/x-api.service.ts` — typed client
3. `features/x/x.facade.ts` — signal store (pager only if the API is paged)
4. `features/x/x-list.component.ts` (+ `x-detail.component.ts`)
5. `features/x/x.facade.spec.ts` — including an error-title case
6. `app.routes.ts` — `loadComponent` list route with a `:id` child
7. `shell/shell.component.ts` — one `NavItem` in the right group
8. `core/nav-icons.ts` — the glyph path (24×24 feather-style `d` geometry only, no `<svg>` wrapper)
9. `core/i18n.service.ts` — `nav.x` plus every `x.*` key, **all three languages**
10. `core/help/help-content.ts` — a `HelpTopic` so the `?` icon appears

Nothing else: no `styles.css` edit, no new provider, no feature routes file, no barrel.

## 11. Tests

Specs sit beside the file under test. Coverage is deliberately concentrated on core services,
facades and shared UI — **no feature list/detail component has a spec**. A new feature ships a
**facade spec**; a new shared-UI component ships a component spec.

Facade spec idioms — hold the un-awaited promise, drive HTTP, then await:
```ts
const done = facade.review('ch1', true, 'KPIs confirm objective met.');
const post = http.expectOne(`${base}/ch1/review`);
expect(post.request.body).toEqual({ effective: true, notes: '…' });
post.flush(null);
await new Promise((resolve) => setTimeout(resolve));   // the refetch is a microtask later
http.expectOne(`${base}/ch1`).flush({ ...closed, status: 'Reviewed' });
await done;
```
`provideHttpClient(withXhr())` must match `app.config`; `afterEach(() => http.verify())`; error
tests flush a real ProblemDetails body with a backend `code`. Component specs put standalone
components in `imports:` and set signal inputs with `fixture.componentRef.setInput(name, value)`.

E2E is Playwright, chromium, `workers: 1`. Select by component element (`qams-page-header`) or a
`name=`/`type=` attribute — **there are no `data-testid` attributes** in this codebase.
`e2e/a11y.spec.ts` runs axe-core and fails on `serious`/`critical`.

```bash
cd frontend && CHROME_BIN="/c/Program Files (x86)/Google/Chrome/Application/chrome.exe" node node_modules/@angular/cli/bin/ng.js test --watch=false --browsers=ChromeHeadless
```
```bash
cd frontend && node node_modules/@playwright/test/cli.js test
```

## 12. Documentation style

Every service, facade, component and shared interface carries a JSDoc block stating its
**domain/regulatory purpose**, not its mechanics — e.g. *"The UI asks `can(...)` to decide what to
OFFER; authoritative enforcement stays on the server — this is affordance, never a security
boundary."* Cite finding ids inline (`API-004`, `R-3`, `UI-014`, `ADR-0009`). Comments explaining a
**refusal** — why a meter is not drawn, why a spec is skipped — are part of the convention.
