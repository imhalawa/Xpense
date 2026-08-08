# Web Shell and Transaction Filtering Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current Fluent shell with the Todoist-shaped shell the spec describes: an identity-led 280px sidebar, one-click taxonomy filtering driven by the URL, a compact Transactions toolbar in place of the hero header, and a route-backed `Add transaction` dialog owned by the shell.

**Architecture:** The URL is the only filter state. `TransactionFilterState` parses and serialises it and renders nothing. `VaultProjection` is the single data seam: every screen reads decrypted, authorised view models from it and never calls Axios. In this plan `VaultProjection` is implemented as a thin adapter over the existing plaintext API client so the shell is buildable and testable before the encrypted vault exists; the vault replaces the implementation later without touching a consumer.

**Tech Stack:** React 19.2.8, react-router 8.3.0, `@fluentui/react-components` 9.74.5, `@fluentui/react-icons` 2.0.335, `@fluentui/react-datepicker-compat` 0.6.35, Vite 8, TypeScript 7, Vitest 4.

**Source spec:** `docs/superpowers/specs/2026-08-08-web-shell-and-filtering-design.md`.
**Vocabulary specs (read, do not implement here):** `2026-08-08-identity-groups-and-sharing-design.md`, `2026-08-08-zero-knowledge-vault-and-recovery-design.md`.

---

## Baseline

Measured on `feat/fluent-migration` before this plan starts:

```
cd web && npx vitest run
Test Files  25 passed (25)
Tests       119 passed (119)
```

The API is not touched by this plan. No `api/` file is edited, added or deleted.

## Global constraints

- **No comments in any file.** Applies to `.ts` and `.tsx` exactly as it applies to C#. The only surviving exception in this repo is a Swagger `<summary>` on an API record, which does not exist in `web/`.
- **Spell names out.** No `ct`, `db`, `el`, `val`, `idx`, single letters. Lambda parameters are named for what they hold.
- **Constants first.** Module constants at the top of the file, before components and hooks.
- **`tsc` is the only static check.** There is no linter. Run `cd web && npx tsc --noEmit` on every task.
- **No hand-set font sizes.** Use `Subtitle1`, `Body1`, `Body2`, `Caption1`, `Title3`. If a size is missing from the ramp, question the design.
- **No hardcoded colours.** Fluent theme tokens via `tokens.*`, or the validated palette in `src/theme/tokens.ts` surfaced as the `--xpense-*` custom properties already defined in `src/fluent/GlobalStyles.tsx`.
- **No barrel value imports.** Import `Currency` from `src/typings/enums/Currency`, never from `src/typings`. A value import through the barrel has already crashed this app once at module-init time; `npm run check:init` is the guard and must pass.
- **No UI component calls Axios.** Only `src/clients/*` and the projection adapter may import `axios`. After Task 3 the check is `grep -rn "axios" src --include=*.tsx` returning nothing.
- **Styling is Griffel `makeStyles`.** No inline style objects.
- **Never sum across currencies.**

## Corrections found while executing

Facts that came out of Tasks 1, 2, 5 and 12 and change what a later task must do.

- **Task 12 does not wire itself up.** It lists `TransactionsView.tsx` and `SidebarFilters.tsx` as files to modify, but those are created by Tasks 11 and 7, which run later. Task 7 must import `resolveTagColors` and `categoryPaletteSlot` from `src/theme/tagColors.ts` itself, and Task 11 must do the same. Task 12 created the module and nothing else.
- **Task 8 must lift `useIsWideScreen`.** `AppShell.tsx` and `PageTitle.tsx` now hold the same private `matchMedia` hook against `(min-width: 1024px)`. Task 8 rewrites `AppShell` and owns the deduplication. A CSS media query cannot replace it: jsdom does not evaluate media queries, and Tasks 13 and 14 need the branch observable from a test.
- **`clip` must use the comma form.** jsdom's `cssstyle` silently drops `clip: rect(0 0 0 0)`; `getComputedStyle(...).clip` reads back empty. `clip: rect(0px, 0px, 0px, 0px)` parses correctly. Tasks 13 and 14 assert on this.
- **`space` is never stripped.** `FilterFacet` includes `"space"`, but `TransactionFilter.space` is non-nullable. `stripFacets` skips it and `resolveFilter` never reports it. An inaccessible space is a redirect to the fallback space, not a stripped facet.
- **No page renders an `h1` until Task 8.** `PageTitle` exists but nothing mounts it. This is the intended intermediate state; Task 8 mounts it in `AppShell` from the matched destination.
- **`Transactions.tsx` keeps its `New transaction` button** until Task 9 replaces it with the shell-owned dialog. Task 5's wording implied otherwise; removing it early would leave no way to add a transaction across Tasks 5 to 8.

## Test files that must pass UNCHANGED

Editing one of these means behaviour broke, not that the test needs updating:

`src/budgets/budgetFormRules.test.ts`, `src/budgets/budgetProgress.test.ts`,
`src/money/formatMoney.test.ts`, `src/theme/contrast.test.ts`,
`src/hooks/useDebouncedValue.test.ts`, `src/clients/options.test.ts`,
`src/clients/transactions.test.ts`.

`src/theme/density.test.ts` is deliberately **not** on this list. See Task 16.

---

## What this plan removes from the superseded specs

Both superseded documents stay on disk; Task 16 edits their status lines. What stops being true:

**From `2026-08-07-information-architecture-design.md`:**

- *Removed:* "Navigation consolidates … One remains: a top bar on desktop, a bottom bar on mobile." There is no top bar and no bottom bar. Navigation is the inline 280px sidebar on desktop and an overlay drawer below 1024px.
- *Removed:* "A notification bell with an unread count lives in the top bar." The bell moves to the sidebar footer beside appearance and vault-lock.
- *Replaced:* shipping-order item 3, "Transactions filtering — extension 3". Filtering is no longer a server query with new `categoryId`/`merchantId`/`tagId`/`accountId` parameters. It is local, over the projection, and API extension 3 is cancelled — the server will not be able to read those fields after the vault lands.
- *Unchanged and still binding:* the eight questions, the five-page structure, the never-convert-currency rule, the visible-`Uncounted` rule, the transfers-excluded-from-spending rule, and the null-budget-period state.

**From `2026-08-08-fluent-migration-design.md`:**

- *Removed:* "A page header component: title, optional supporting context, optional actions." `PageHeader` is deleted in Task 5.
- *Removed:* the `Page` → page-header mapping row in the component table.
- *Removed:* "a page title is `Title2`". A desktop page title is not rendered at all; the mobile title is `Subtitle1`.
- *Unchanged and still binding:* `FluentProvider` at the root, the brand ramp, the banned preview packages, the icon set, the chart palette, and every accessibility requirement.

Nothing in the identity or vault specs is implemented here. This plan only creates the seam they will plug into.

---

## The `VaultProjection` seam

The encrypted vault does not exist. The shell must not wait for it. `VaultProjection` is therefore specified as an interface first and implemented twice:

1. **`plaintextProjection`** — a thin adapter over today's `src/clients/*` endpoints. It fetches plaintext rows, stringifies the numeric ids into opaque record ids, applies every filter locally in memory, and reports a single hardcoded Personal space. Ships in Task 3.
2. **`vaultProjection`** — the real implementation over IndexedDB ciphertext and the crypto worker. Not in this plan. It replaces the adapter with **no change to any consumer**, because every consumer depends on the interface only.

Three design choices make the swap free:

- **Every method is async.** The real projection lives behind a Web Worker `postMessage` boundary. Async now means no consumer changes shape later.
- **Every identifier is an opaque `string`.** Today's ids are `number` and accounts are keyed by `accountNumber`; the vault will use UUIDs. The adapter stringifies at its boundary so consumers never learn either shape.
- **Authorisation is a field, not a caller concern.** `canEdit` is on `SpaceSummary`, `AccountView` and `TransactionView`. The adapter returns `true` everywhere because there is no auth yet; the vault returns the real grant. No component branches on "do we have auth yet".

The interface, exactly as it goes into `web/src/vault/VaultProjection.ts`:

```ts
import { Currency } from "../typings/enums/Currency";
import { TransactionKind } from "../clients/types";

export type SpaceId = string;
export type RecordId = string;
export type TaxonomyKind = "category" | "merchant" | "tag";
export type VaultState = "locked" | "loading" | "ready" | "error";
export type FilterFacet = "space" | "category" | "merchant" | "tag" | "account" | "from" | "to";

export interface SpaceSummary {
  id: SpaceId;
  name: string;
  kind: "personal" | "group";
  canEdit: boolean;
}

export interface AccountView {
  id: RecordId;
  label: string;
  currency: Currency;
  canEdit: boolean;
}

export interface TaxonomyValue {
  id: RecordId;
  kind: TaxonomyKind;
  label: string;
  foregroundHex: string | null;
  backgroundHex: string | null;
}

export interface TransactionView {
  id: RecordId;
  kind: TransactionKind;
  amountMinorUnits: number;
  currency: Currency;
  occurredAt: string;
  accountId: RecordId | null;
  counterpartyAccountId: RecordId | null;
  isCounterpartyPrivate: boolean;
  categoryId: RecordId | null;
  merchantId: RecordId | null;
  tagIds: RecordId[];
  canEdit: boolean;
}

export interface TransactionFilter {
  space: SpaceId;
  category: RecordId | null;
  merchant: RecordId | null;
  tag: RecordId | null;
  account: RecordId | null;
  from: string | null;
  to: string | null;
}

export interface FilterResolution {
  filter: TransactionFilter;
  removed: FilterFacet[];
}

export interface PageRequest {
  offset: number;
  limit: number;
}

export interface TransactionPage {
  rows: TransactionView[];
  totalRows: number;
}

export interface TransactionDraft {
  id: RecordId | null;
  space: SpaceId;
  kind: TransactionKind;
  amountMinorUnits: number;
  currency: Currency;
  occurredAt: string;
  accountId: RecordId;
  categoryId: RecordId | null;
  merchantLabel: string | null;
  tagLabels: string[];
  reason: string | null;
}

export interface VaultProjection {
  readonly state: VaultState;
  subscribe(listener: (state: VaultState) => void): () => void;
  unlock(): Promise<void>;
  lock(): void;
  listSpaces(): Promise<SpaceSummary[]>;
  listAccounts(space: SpaceId): Promise<AccountView[]>;
  listTaxonomy(space: SpaceId, kind: TaxonomyKind): Promise<TaxonomyValue[]>;
  resolveFilter(filter: TransactionFilter): Promise<FilterResolution>;
  queryTransactions(filter: TransactionFilter, page: PageRequest): Promise<TransactionPage>;
  saveTransaction(draft: TransactionDraft): Promise<TransactionView>;
}
```

`VaultProjection.ts` holds types and this interface only. No values, no re-exports of other modules' values. That keeps it out of the circular-init trap `check:init` guards.

`from` and `to` on `TransactionFilter` are inclusive `YYYY-MM-DD` calendar dates, exactly as the user typed them. Converting `to` into an exclusive instant is `queryTransactions`'s job and happens once, inside the projection. No component ever adds a day.

---

## Component boundary map

| Spec boundary | New file | Replaces / absorbs |
|---|---|---|
| `AppShell` | `web/src/shell/AppShell.tsx` (rewritten) | Its own current body: the `Xpense` wordmark header, the four-item `NavDrawer`, the 1120px content width. Absorbs the notification loading it already does, plus the `Add transaction` action and dialog route lifted out of `Transactions.tsx`. |
| `IdentityMenu` | `web/src/shell/IdentityMenu.tsx` | The `NavDrawerHeader` wordmark block in `AppShell.tsx`. |
| `SidebarFilters` | `web/src/shell/SidebarFilters.tsx` | Nothing today — new surface. |
| `TransactionFilterState` | `web/src/transactions/transactionFilterState.ts` | The `useState` date pair at the top of `web/src/pages/Transactions/Transactions.tsx`, and the `day`/`from`/`to` props threaded into `TransactionsGrid`. |
| `VaultProjection` | `web/src/vault/VaultProjection.ts` + `web/src/vault/plaintextProjection.ts` | The direct `listTransactions`/`listCategories` calls inside `TransactionsGrid.tsx`. |
| `TransactionsView` | `web/src/pages/Transactions/TransactionsView.tsx` | `web/src/pages/Transactions/TransactionsGrid/TransactionsGrid.tsx`, deleted. `Transactions.tsx` shrinks to a wrapper. |
| `TransactionDialog` | `web/src/shell/TransactionDialog.tsx` | The `Dialog` block in `Transactions.tsx`. Wraps the existing `TransactionsForm`, which is kept. |

**What happens to `PageHeader`:** deleted, with its test. The spec removes the large desktop page title. `AppShell` renders one `PageTitle` derived from the matched destination — visually hidden `h1` on desktop, visible `Subtitle1` below 1024px. Pages stop rendering any title. Page-level actions move into each page's own toolbar; on Transactions that is the compact toolbar, and on Budgets it is the existing `New budget` button placed above the list.

---

### Task 1: `TransactionFilterState`

**Files:**
- Create: `web/src/transactions/transactionFilterState.ts`
- Create: `web/src/transactions/transactionFilterState.test.ts`

Pure module. No React, no imports from `src/vault` except the `TransactionFilter` type. The exact URL contract:

```
/transactions?space=<personal-or-group-id>&category=<id>&merchant=<id>&tag=<id>&account=<id>&from=<date>&to=<date>
```

Exports:

- `parseTransactionFilter(search: URLSearchParams, fallbackSpace: SpaceId): TransactionFilter` — one value per facet; a repeated parameter takes the first and the rest are dropped, because multi-select is out of scope. A missing `space` falls back to the caller's active space. `from`/`to` accept `YYYY-MM-DD` only; anything else becomes `null`. `from` later than `to` clears both.
- `serialiseTransactionFilter(filter: TransactionFilter): URLSearchParams` — omits null facets entirely rather than writing empty values, so the URL stays short and comparable.
- `toggleTaxonomyFilter(filter, kind, id): TransactionFilter` — sets that facet, or clears it when the same id is already selected. Leaves `space`, `from`, `to`, `account` and the other two taxonomy facets untouched.
- `clearTransactionFilters(filter): TransactionFilter` — keeps `space`, nulls everything else.
- `countActiveFilters(filter): number` — counts non-null facets excluding `space`; a set `from`/`to` pair counts as one.
- `stripFacets(filter, removed: FilterFacet[]): TransactionFilter` — nulls the named facets. Used by Task 4 after the projection reports invalid ids.
- `localPageBounds(totalRows, pageIndex): PageRequest` — 50 rows per page, clamped to `totalRows`.

**Verification:**

```bash
cd web && npx vitest run src/transactions/transactionFilterState.test.ts && npx tsc --noEmit
```

Asserts: round-trip parse→serialise→parse is stable; each facet parses independently; two facets set at once both survive, which is the AND contract at the URL layer; a repeated `category` keeps the first; `from=2026-13-40` becomes null; `from` after `to` clears both; toggling the selected category clears it and leaves `merchant` and `account` alone; `clearTransactionFilters` keeps only `space`; `countActiveFilters` treats a date range as one; `localPageBounds(120, 2)` yields `{ offset: 100, limit: 20 }`.

- [ ] Step 1: write the failing test. Step 2: run it red. Step 3: implement. Step 4: green, then commit.

---

### Task 2: The `VaultProjection` interface and a fixture implementation

**Files:**
- Create: `web/src/vault/VaultProjection.ts` — exactly the interface printed above, nothing else.
- Create: `web/src/vault/fixtureProjection.ts` — an in-memory `VaultProjection` built from a plain array of `TransactionView`, `AccountView`, `TaxonomyValue` and `SpaceSummary`. It is the test double for every later component test and the fixture host for the integration suite in Task 15.
- Create: `web/src/vault/fixtureProjection.test.ts`

`fixtureProjection(seed)` returns a `VaultProjection` whose `queryTransactions` applies every facet with AND, converts the inclusive `to` date into an exclusive instant, sorts newest first, and slices by `PageRequest`. `resolveFilter` reports a facet in `removed` when its id is absent from the seed for that space. `state` starts `"ready"`; `lock()` moves it to `"locked"` and makes every list method reject.

Because `fixtureProjection` holds the only real filter implementation until the vault lands, `plaintextProjection` in Task 3 reuses it: the adapter fetches, maps to view models, and delegates the filtering. One filter implementation, tested once.

**Verification:**

```bash
cd web && npx vitest run src/vault/fixtureProjection.test.ts && npx tsc --noEmit
```

Asserts: category and merchant set together return only rows matching both; an unknown tag id lands in `removed` and is not applied; a date range excludes the day after `to` and includes `to` itself; `queryTransactions` with `{ offset: 50, limit: 50 }` returns rows 51–100 with the full `totalRows`; after `lock()` every method rejects and `state` is `"locked"`; `subscribe` fires on lock and the returned function unsubscribes.

- [ ] Step 1: write the failing test. Step 2: run it red. Step 3: implement. Step 4: green, then commit.

---

### Task 3: `plaintextProjection` and the `VaultProvider`

**Files:**
- Create: `web/src/vault/plaintextProjection.ts`
- Create: `web/src/vault/plaintextProjection.test.ts`
- Create: `web/src/vault/VaultProvider.tsx` — a context provider plus `useVault()`.
- Create: `web/src/vault/VaultProvider.test.tsx`
- Modify: `web/src/App.tsx` — mount `VaultProvider` inside `FluentProvider`, above the router.

`plaintextProjection` calls `listAccounts`, `listCategories`, `listMerchants`, `listTags` and pages through `listTransactions` until the last page or a 2,000-row ceiling, whichever comes first. It maps every numeric id and every `accountNumber` to a `string`, sets `canEdit: true` everywhere, and reports one space: `{ id: "personal", name: "Personal", kind: "personal", canEdit: true }`. It then hands the mapped arrays to `fixtureProjection` and delegates.

`saveTransaction` maps a `TransactionDraft` back onto `ICreateTransactionRequest` and calls the existing `createTransaction`, then refetches. A draft carrying an `id` throws `"Editing a transaction is not supported yet"` — see the backend-gap section; the dialog must never offer edit while that is true.

`unlock()` is the fetch. `lock()` drops the cached arrays. There is no key material, and the file says so through its name, not a comment.

**Verification:**

```bash
cd web && npx vitest run src/vault/plaintextProjection.test.ts src/vault/VaultProvider.test.tsx && npx tsc --noEmit
grep -rn "axios" web/src --include=*.tsx
```

Asserts: with `axios` mocked, `unlock()` issues exactly one request per option endpoint and pages transactions until `page === totalPages`; ids arrive as strings; an account filter matches on the stringified account number; `saveTransaction` posts the mapped request body; a draft with an `id` rejects; `useVault()` outside the provider throws with a named message. The `grep` must return nothing.

- [ ] Step 1: write the failing test. Step 2: run it red. Step 3: implement. Step 4: green, then commit.

---

### Task 4: `useTransactionFilter` — URL as source of truth, with stripping

**Files:**
- Create: `web/src/transactions/useTransactionFilter.ts`
- Create: `web/src/transactions/useTransactionFilter.test.tsx`

The hook reads `useSearchParams()`, parses with Task 1, resolves with `VaultProjection.resolveFilter`, and when `removed` is non-empty rewrites the URL with `navigate(..., { replace: true })` and exposes a one-shot `removedMessage`. `replace` matters: a stripped identifier must not leave a broken entry in the back stack.

Exposes `{ filter, setFilter, toggleTaxonomy, clearFilters, activeFilterCount, removedMessage, dismissRemovedMessage }`.

The message is reported **once** per resolution, as the spec requires. The hook keys the message on the serialised removed set, so re-rendering does not re-announce and a genuinely new bad identifier does.

**Verification:**

```bash
cd web && npx vitest run src/transactions/useTransactionFilter.test.tsx && npx tsc --noEmit
```

Asserts: mounting at `/transactions?category=999` with a fixture that has no category 999 rewrites the URL to `/transactions` and sets `removedMessage`; the history entry is replaced, not pushed; a second render does not re-set the message; `toggleTaxonomy("category", "3")` pushes `?category=3` and preserves an existing `?from`/`?to`/`?account`; toggling the same id again removes only that parameter; `clearFilters` leaves `?space=personal` alone and drops the rest.

- [ ] Step 1: write the failing test. Step 2: run it red. Step 3: implement. Step 4: green, then commit.

---

### Task 5: `PageTitle` replaces `PageHeader`

**Files:**
- Create: `web/src/shell/PageTitle.tsx`
- Create: `web/src/shell/PageTitle.test.tsx`
- Delete: `web/src/shell/PageHeader.tsx`
- Delete: `web/src/shell/PageHeader.test.tsx`
- Modify: `web/src/pages/Overview/Overview.tsx`, `web/src/pages/Budgets/Budgets.tsx`, `web/src/pages/Settings/Settings.tsx`, `web/src/pages/Transactions/Transactions.tsx` — drop the `PageHeader` import and element. Budgets keeps its `New budget` button by moving it above the list.

`PageTitle` renders one `<h1>` always. Above 1024px it carries a visually-hidden class — the clipped-rect pattern, not `display: none` and not `visibility: hidden`, because both remove it from the accessibility tree. Below 1024px it renders visibly as `Subtitle1`. There is no `description` prop and no `actions` prop; both were the large-header treatment the spec removes.

**Verification:**

```bash
cd web && npx vitest run src/shell/PageTitle.test.tsx && npx tsc --noEmit
grep -rn "PageHeader" web/src
```

Asserts: exactly one `heading` with `level: 1` in both viewports; at 1440px its computed class carries `position: absolute` and a 1px clip rect while `getByRole("heading", { level: 1 })` still finds it; at 390px it is visible. The `grep` must return nothing.

- [ ] Step 1: write the failing test. Step 2: run it red. Step 3: implement. Step 4: green, then commit.

---

### Task 6: `IdentityMenu`

**Files:**
- Create: `web/src/shell/IdentityMenu.tsx`
- Create: `web/src/shell/IdentityMenu.test.tsx`

A Fluent `Menu` opened by a button showing initials or avatar, display name, and active-space name stacked beneath it. Initials come from the display name after unlock; while locked the trigger shows the email prefix, per the identity spec.

Menu contents, in the spec's order: Personal and every group as a `MenuItemRadio` group with the active space checked, then `Manage groups`, `Account and passkeys`, `Lock vault`, `Sign out`.

Props: `spaces`, `activeSpace`, `displayName`, `emailPrefix`, `isUnlocked`, `onSelectSpace`, `onManageGroups`, `onAccountSettings`, `onLock`, `onSignOut`. Every callback except `onSelectSpace` and `onLock` is currently wired to a handler that opens nothing — see the backend-gap section. Those items are still rendered, because the menu is the spec's stated shape; the disabled-with-explanation treatment applies only where the spec asks for it.

Changing space calls `onSelectSpace`, which `AppShell` turns into a URL change on `?space`, which re-resolves the projection.

**Verification:**

```bash
cd web && npx vitest run src/shell/IdentityMenu.test.tsx && npx tsc --noEmit
```

Asserts: the trigger has an accessible name containing the display name and the active space; opening lists Personal plus each group with `aria-checked="true"` on exactly one; selecting a different space calls `onSelectSpace` with its id; `Lock vault` calls `onLock`; while locked the trigger shows the email prefix and not the display name; the menu closes and returns focus to the trigger on Escape.

- [ ] Step 1: write the failing test. Step 2: run it red. Step 3: implement. Step 4: green, then commit.

---

### Task 7: `SidebarFilters` and taxonomy ordering

**Files:**
- Create: `web/src/shell/taxonomyOrder.ts`
- Create: `web/src/shell/taxonomyOrder.test.ts`
- Create: `web/src/shell/SidebarFilters.tsx`
- Create: `web/src/shell/SidebarFilters.test.tsx`

`taxonomyOrder.ts` is pure: `orderTaxonomyValues(values, recentIds)` puts recently-used values first in recency order, then the rest sorted alphabetically with `localeCompare`, and `rememberTaxonomyUse(kind, id)` / `readTaxonomyRecency(kind)` persist a capped list of 20 ids per kind in `localStorage`. Recency is a UI preference derived from clicks, never from record content, so it stays outside the vault.

`SidebarFilters` renders the three sections — Categories, Tags, Merchants, in that order. Each header is a `button` with `aria-expanded` and `aria-controls`. Exactly one section may be open: opening one closes the other two, held in a single `expandedSection` state.

An open section lists at most five values. Each value is a link to `/transactions` carrying the toggled filter, so middle-click and copy-link behave. Clicking calls `toggleTaxonomy` from Task 4 and records recency. The selected value carries the brand tint, a brand-coloured leading marker, **and** a checkmark icon with an accessible name — colour is never the only signal.

`View all` opens a Fluent `Popover` containing a search `Input` and the full filtered list. Fluent's popover returns focus to its trigger on close; **verify that in the test rather than assuming it**, because the previous migration shipped a focus ring that looked correct and did nothing.

**Verification:**

```bash
cd web && npx vitest run src/shell/taxonomyOrder.test.ts src/shell/SidebarFilters.test.tsx && npx tsc --noEmit
```

Asserts, for `taxonomyOrder`: with recency `["7","2"]` the output starts `7, 2` and the remainder is alphabetical; unknown recency ids are ignored; the stored list caps at 20 and moves a repeat use to the front. For `SidebarFilters`: opening Tags sets `aria-expanded="true"` on Tags and `"false"` on the other two; a section with nine values renders five plus `View all`; typing in the popover narrows the list; closing the popover returns focus to `View all`; clicking a value calls `toggleTaxonomy` with that kind and id; the selected value exposes a non-colour selected indicator.

- [ ] Step 1: write the failing test. Step 2: run it red. Step 3: implement. Step 4: green, then commit.

---

### Task 8: `AppShell` rewrite

**Files:**
- Modify: `web/src/shell/AppShell.tsx`
- Modify: `web/src/shell/AppShell.test.tsx`

The sidebar becomes 280px with this fixed order, top to bottom:

1. `IdentityMenu`.
2. `Add transaction` — `appearance="primary"`, full width, `AddRegular` icon.
3. Overview, Transactions, Budgets, Manage as `NavItem`s.
4. `SidebarFilters`.
5. Footer: `NotificationBell`, `ThemeModeToggle`, `Lock vault`.

Content region: `maxWidth: "1200px"`, `padding: tokens.spacingHorizontalXXXL` — which is exactly 32px, verified in `@fluentui/tokens/lib/global/spacings.js`; do not write `"32px"`. Content starts at the top of its surface; there is no vertical centring and no `justify-content: center` anywhere in the main region.

Below 1024px the drawer is `type="overlay"` and closes after any navigation — the existing `navigateTo` already does this and must keep doing it for sidebar filter clicks too.

`AppShell` renders `PageTitle` with the label of the matched destination, so there is exactly one `h1` per page and pages render none. The `<main>` element stays; a `<nav>` landmark wraps the drawer body.

`Add transaction` navigates to `/transactions/new` rather than flipping local state. That is Task 9's route.

**Verification:**

```bash
cd web && npx vitest run src/shell/AppShell.test.tsx && npx tsc --noEmit
```

Asserts: the sidebar's direct children appear in the fixed order — identity trigger, `Add transaction`, the four destinations, the three filter sections, the footer actions — checked by comparing `compareDocumentPosition` between the queried elements; the four destinations still render as links with `aria-current="page"` on exactly one; at 1440px the drawer is inline and no `Open navigation` button exists; at 768px the button exists and clicking a destination closes the drawer; the content region carries `max-width: 1200px`; exactly one `h1` renders and its text matches the active destination.

- [ ] Step 1: write the failing test. Step 2: run it red. Step 3: implement. Step 4: green, then commit.

---

### Task 9: `TransactionDialog`, route-backed and shell-owned

**Files:**
- Create: `web/src/shell/TransactionDialog.tsx`
- Create: `web/src/shell/TransactionDialog.test.tsx`
- Modify: `web/src/App.tsx` — add `/transactions/new` as a child route of `Layout`.
- Modify: `web/src/pages/Transactions/Transactions.tsx` — delete its `Dialog` block, its `isFormOpen` state and its `New transaction` button.

The dialog is mounted by `AppShell`, not by a page, so it opens from Overview, Budgets and Manage identically. Its open state is the route: `/transactions/new` open, anything else closed. Closing navigates back, so browser Back closes the dialog and reload reopens it — which is the spec's stated reason for making it route-backed.

Account choices come from `listAccounts(activeSpace)` filtered to `canEdit`. When that list is empty the `Add transaction` button in the sidebar is `disabled` and carries a `title` plus an adjacent `Caption1` explaining that no account in this space can be edited. A disabled button alone is not an explanation.

The body is the existing `TransactionsForm`, unchanged in this task beyond receiving the permitted account list. The form's submit goes through `VaultProjection.saveTransaction`, not through `createTransaction` directly.

Focus moves into the dialog on open and returns to `Add transaction` on close. Fluent's `Dialog` does both; assert it.

**Verification:**

```bash
cd web && npx vitest run src/shell/TransactionDialog.test.tsx && npx tsc --noEmit
```

Asserts: navigating to `/transactions/new` from `/budgets` renders the dialog with `role="dialog"`; the account dropdown lists only `canEdit` accounts; with zero editable accounts the sidebar button is disabled and an explanation is in the accessible description; Escape closes the dialog and focus lands on `Add transaction`; submitting calls `saveTransaction` and then navigates away from `/transactions/new`.

- [ ] Step 1: write the failing test. Step 2: run it red. Step 3: implement. Step 4: green, then commit.

---

### Task 10: The compact transactions toolbar

**Files:**
- Create: `web/src/pages/Transactions/TransactionsToolbar.tsx`
- Create: `web/src/pages/Transactions/TransactionsToolbar.test.tsx`

A Fluent `Toolbar`, one row, in this order: a date-range summary button, an account `Dropdown`, a filter count `Badge`, and `Clear filters`. This replaces the two permanently-visible `DatePicker` fields currently occupying a grid at the top of `Transactions.tsx`.

The date-range button's label is the current range in plain text and it opens a `Popover` holding the two `DatePicker`s. The pickers stay `@fluentui/react-datepicker-compat`; it is still the weakest dependency in the stack and if it fights the popover, say so rather than working around it silently.

The filter count comes from `countActiveFilters`. It renders only when greater than zero. `Clear filters` is disabled at zero and calls `clearFilters`, which preserves only `space`.

**Verification:**

```bash
cd web && npx vitest run src/pages/Transactions/TransactionsToolbar.test.tsx && npx tsc --noEmit
```

Asserts: the toolbar has `role="toolbar"` with an accessible name; the date button's label reflects the current filter and opens a popover containing both pickers; the account dropdown lists the projection's accounts and setting one calls `setFilter` with only `account` changed; the badge shows 2 when a category and a date range are set; `Clear filters` is disabled with no filters and, when clicked with filters set, produces a filter whose only non-null field is `space`.

- [ ] Step 1: write the failing test. Step 2: run it red. Step 3: implement. Step 4: green, then commit.

---

### Task 11: `TransactionsView`

**Files:**
- Create: `web/src/pages/Transactions/TransactionsView.tsx`
- Create: `web/src/pages/Transactions/TransactionsView.test.tsx`
- Delete: `web/src/pages/Transactions/TransactionsGrid/TransactionsGrid.tsx`
- Delete: `web/src/pages/Transactions/TransactionsGrid/TransactionsGrid.test.tsx`
- Modify: `web/src/pages/Transactions/Transactions.tsx` — reduced to the toolbar plus the view, both fed by `useTransactionFilter`.
- Modify: `web/src/pages/Overview/Overview.tsx` — it uses `TransactionsGrid` for its recent-transactions block; switch it to `TransactionsView` with `hideToolbar` and a five-row limit.

Column order is the spec's, not today's: amount, date, category, merchant, account, tags. Amount is right-aligned with `fontVariantNumeric: "tabular-nums"`.

Desktop is a semantic table via Fluent `DataGrid` at `size="medium"`, which is 44px per row — verified in `@fluentui/react-table/lib/components/TableCell/useTableCellStyles.styles.raw.js`. Do not set a row height by hand and do not pass `size="small"`, which is 34px. Below 768px the table is replaced by the card list, each card carrying the same six fields and a 44px minimum target on every control.

States, all four required:

- Empty and unfiltered: how to add the first transaction, with the `Add transaction` action.
- Empty and filtered: no matches, plus `Clear filters`.
- Loading: row skeletons, not a spinner and not an empty table.
- Locked vault: the list is replaced by a single `Unlock` action.
- Sync failure: cached rows stay visible and a non-blocking `MessageBar` reports the failure. It must not unmount the table.

Paging is local: 50 rows rendered at a time from `queryTransactions`, and past 200 rows the list virtualises. Virtualise with a windowing calculation over the already-fetched array — no new dependency, and `@fluentui/react-list-preview` is banned by the React peer range.

An inaccessible transfer counterparty renders as `Private account`, from `isCounterpartyPrivate`.

**Verification:**

```bash
cd web && npx vitest run src/pages/Transactions/TransactionsView.test.tsx && npx tsc --noEmit
```

Asserts: at 1440px a `table` role renders with headers in the spec order and no card list; at 390px the card list renders and no `table` exists; a cell's computed height is 44px; the empty-unfiltered state names adding a first transaction and the empty-filtered state offers `Clear filters`; `state === "locked"` renders one `Unlock` button and zero rows; a rejected `queryTransactions` after a successful one keeps the previous rows and adds a `status` message; 220 fixture rows render fewer than 220 row elements while `totalRows` still reads 220; a transfer with `isCounterpartyPrivate` shows `Private account`.

- [ ] Step 1: write the failing test. Step 2: run it red. Step 3: implement. Step 4: green, then commit.

---

### Task 12: Colour and Fluent type roles

**Files:**
- Create: `web/src/theme/tagColors.ts`
- Create: `web/src/theme/tagColors.test.ts`
- Modify: `web/src/fluent/GlobalStyles.tsx` — no new colours; confirm the `--xpense-*` set covers alert red, income blue and the eight category slots in both themes.
- Modify: `web/src/pages/Transactions/TransactionsView.tsx` and `web/src/shell/SidebarFilters.tsx` to consume the rules.

The rules, verbatim from the spec:

- Brand actions and selection use the `#1565C0` ramp already in `src/fluent/brand.ts`.
- Category markers use the eight-slot palette in `src/theme/tokens.ts`, indexed by a stable hash of the category id — the modulo of a numeric id used today breaks the moment ids become UUIDs, so hash the string.
- Tags use their stored foreground/background pair, validated by `resolveTagColors(foregroundHex, backgroundHex)` in the new module. It calls the existing `contrastRatio` from `src/theme/contrast.ts` and returns the stored pair when the ratio is at least 4.5:1, otherwise the neutral Fluent badge pair.
- Merchants get a neutral icon and no colour.
- Income is the blue and ordinary expense the muted red already in `tokens.money`; alerts use `expenseAlert` plus an icon, so red is never the only signal.
- Errors and destructive actions use Fluent danger tokens. Never the brand colour.

Type roles: `Subtitle1` for the mobile page title, `Body1` for rows and navigation, `Caption1` for secondary metadata, tabular figures on amounts. Desktop body text never below 14px, which `Body1` satisfies at 14px — so `Caption1` at 12px is metadata only and never a row's primary content.

**Verification:**

```bash
cd web && npx vitest run src/theme/tagColors.test.ts src/theme/tokens.test.ts && npx tsc --noEmit
grep -rn "#[0-9a-fA-F]\{6\}" web/src --include=*.tsx
```

Asserts: a 7:1 stored pair is returned unchanged; a 1.5:1 pair falls back to the neutral pair; a null pair falls back; the same category id hashes to the same slot across calls and two different ids can share a slot without throwing. The `grep` must return nothing — every hex lives in `tokens.ts` or `brand.ts`.

- [ ] Step 1: write the failing test. Step 2: run it red. Step 3: implement. Step 4: green, then commit.

---

### Task 13: Accessibility suite

**Files:**
- Create: `web/src/shell/accessibility.test.tsx`

One file covering the spec's accessibility list against the assembled shell, because these are properties of the whole, not of one component.

Covers: exactly one `banner`-free landmark structure of `navigation` plus `main`; exactly one `h1`, hidden at 1440px and visible at 390px; every icon-only control has an accessible name — asserted by querying every `button` and `link` in the rendered shell and failing on an empty accessible name, which catches a future regression rather than only today's controls; keyboard reachability of the identity trigger, `Add transaction`, all four destinations and all three section headers by tab order; focus return from the `View all` popover and from the transaction dialog; `aria-expanded` present on every section header; `prefers-reduced-motion: reduce` honoured, asserted against `appGlobalStyles` rather than computed animation, since jsdom does not animate.

**Verification:**

```bash
cd web && npx vitest run src/shell/accessibility.test.tsx && npx tsc --noEmit
```

- [ ] Step 1: write the failing test. Step 2: run it red. Step 3: fix whatever it catches. Step 4: green, then commit.

---

### Task 14: Responsive suite at 390 / 768 / 1024 / 1440, light and dark

**Files:**
- Create: `web/src/shell/responsive.test.tsx`

Eight renders: four widths × two themes, driven by the `matchMedia` mock the existing `AppShell.test.tsx` already uses and by swapping `lightTheme`/`darkTheme` on `FluentProvider`.

**These are structural snapshots, not pixel snapshots.** jsdom has no layout engine. What each snapshot pins is which branch rendered: inline drawer versus overlay plus `Open navigation`, hidden `h1` versus visible, `table` versus card list, and the toolbar collapsing. Saying this plainly matters — a reader who expects visual regression coverage from this file will be wrong.

The 1024px boundary is asserted explicitly in both directions, because it is the drawer breakpoint and an off-by-one there is the most likely responsive bug.

**Verification:**

```bash
cd web && npx vitest run src/shell/responsive.test.tsx && npx tsc --noEmit
```

Asserts: at 390 and 768 the overlay drawer and the visible title and the card list; at 1024 and 1440 the inline drawer and the hidden title and the table; each of the eight snapshots is stable across two runs; dark and light differ only in theme attributes, not in structure.

- [ ] Step 1: write the failing test. Step 2: run it red. Step 3: implement. Step 4: green, then commit.

---

### Task 15: Integration over fixtures — filtering with no API query

**Files:**
- Create: `web/src/vault/projectionIntegration.test.tsx`

Renders the whole shell over `fixtureProjection` seeded with a two-space, multi-account, multi-taxonomy dataset, with `axios` mocked to **throw on any call**. That is the assertion, not a detail: if filtering reaches the network the test fails.

Flow under test: land on `/transactions`, click a sidebar category, assert the URL gains `?category=<id>` and the row set narrows; click a tag, assert both parameters are present and the rows satisfy both, which is AND end to end; click the selected category again, assert only the tag survives; set a date range in the toolbar and assert the boundary days; `Clear filters` and assert the URL is `?space=personal`; switch space in the identity menu and assert the URL's `space` changes and the taxonomy sections reload for that space.

This is the spec's stated proof that "no filter sends private financial fields or labels to the server".

**Verification:**

```bash
cd web && npx vitest run src/vault/projectionIntegration.test.tsx && npx tsc --noEmit
```

- [ ] Step 1: write the failing test. Step 2: run it red. Step 3: implement. Step 4: green, then commit.

---

### Task 16: Cleanup and spec status updates

**Files:**
- Delete: `web/src/theme/density.ts`, `web/src/theme/density.test.ts` — nothing imports them, and their `compact.rowHeight: 40` contradicts the spec's 44px. A dead constant that disagrees with the shipped value is how the wrong number gets copied later. This intentionally lifts `density.test.ts` off the fluent-migration plan's unchanged list; that guarantee protected a rewrite that is now finished.
- Delete: `web/src/fluent/tmpls.test.ts` if it is still untracked scratch at this point.
- Delete: `web/src/contexts/TransactionUtilitiesContext.tsx` and its provider in `App.tsx` — its only job was telling `TransactionsGrid` to refetch after a submit, which `VaultProjection.saveTransaction` now owns.
- Modify: `docs/superpowers/specs/2026-08-07-information-architecture-design.md` — status line records that navigation and the transaction-filter transport are superseded, and that API extension 3 is cancelled.
- Modify: `docs/superpowers/specs/2026-08-08-fluent-migration-design.md` — status line records that the page-header treatment is superseded.

**Verification:**

```bash
cd web && npm test && npm run build && npm run check:init
grep -rn "TransactionsGrid\|PageHeader\|density\|TransactionUtilities" web/src
cd ../api && dotnet test src/Xpense/Xpense.sln
```

Asserts: the full web suite passes, the production build succeeds, `check:init` prints `MODULE INIT OK` — the guard against a circular value import through a barrel — the greps return nothing, and the 178 API tests still pass untouched.

- [ ] Step 1: delete. Step 2: run the full verification. Step 3: commit.

---

## Controls with no backend

Every control below is in the spec and has nothing to call today. None of them may ship as a stub that silently does nothing.

| Control | Missing today | Depends on |
|---|---|---|
| Identity trigger: display name, initials, avatar | No user, no session, no `/auth/me` | `2026-08-08-identity-groups-and-sharing-design.md` |
| Space switcher: groups list | No group model, no membership | identity spec |
| `Manage groups` | No group endpoints | identity spec |
| `Account and passkeys` | No passkey management | identity spec |
| `Sign out` | No session to revoke | identity spec |
| `Lock vault`, `Unlock` | No key material to clear or unwrap | `2026-08-08-zero-knowledge-vault-and-recovery-design.md` |
| Per-account editor permission on the dialog | No grants; `canEdit` is hardcoded `true` | identity spec |
| Viewer/editor distinction in the row actions | Same | identity spec |
| **Editing a transaction** | **`Features/Transactions/` has `CreateTransaction`, `GetTransactionById`, `ListTransactions` and nothing else. There is no update and no delete endpoint at all.** | A transactions-mutation spec, or the sync surface |
| `account=<id>` in the URL | Accounts are keyed by `accountNumber`; there is no account id | vault spec's record UUIDs |
| Server-side taxonomy filtering | `ListTransactions` accepts only `page`, `pageSize`, `from`, `to` | Not needed — filtering is local by design |
| Loading the whole accessible set | `GET /api/v1/transactions` is paged; the adapter loops to a 2,000-row ceiling | vault spec's `GET /api/v1/sync/changes` cursor |
| Notification dismiss and mark-unread | No endpoint. Already known and already excluded. | Its own spec |

Two consequences the executor must honour:

1. **`TransactionDialog` renders create only.** No edit entry point, no row-level edit or delete affordance, not even disabled. `saveTransaction` throws on a draft carrying an id so a future caller fails loudly instead of posting a duplicate.
2. **`IdentityMenu` renders the full menu shape but three items are inert.** They are rendered because the menu's shape is the spec's product decision and because the spec's acceptance criteria name it. They must not pretend to work: each inert item opens a `Dialog` stating the feature arrives with accounts, rather than doing nothing on click.

## Testing checklist mapped to files

| Spec checklist line | File |
|---|---|
| URL parsing, toggle, AND, invalid identifiers, date ranges | `web/src/transactions/transactionFilterState.test.ts` |
| Invalid identifiers stripped from the URL and reported once | `web/src/transactions/useTransactionFilter.test.tsx` |
| Local pagination | `web/src/transactions/transactionFilterState.test.ts` (`localPageBounds`) and `web/src/vault/fixtureProjection.test.ts` (slicing) |
| Identity menu | `web/src/shell/IdentityMenu.test.tsx` |
| One-open taxonomy sections | `web/src/shell/SidebarFilters.test.tsx` |
| Recency-then-alphabetical ordering | `web/src/shell/taxonomyOrder.test.ts` |
| Viewer/editor actions | `web/src/shell/TransactionDialog.test.tsx` |
| Empty states, locked state, loading, sync failure | `web/src/pages/Transactions/TransactionsView.test.tsx` |
| Mobile title | `web/src/shell/PageTitle.test.tsx` and `web/src/shell/responsive.test.tsx` |
| Landmarks, hidden desktop heading, focus return, labels, keyboard | `web/src/shell/accessibility.test.tsx` |
| Fixtures filter with no API query | `web/src/vault/projectionIntegration.test.tsx` |
| 390 / 768 / 1024 / 1440, light and dark | `web/src/shell/responsive.test.tsx` |
| Sidebar order, 1200px, drawer breakpoint | `web/src/shell/AppShell.test.tsx` |
| Toolbar, filter count, clear | `web/src/pages/Transactions/TransactionsToolbar.test.tsx` |
| Tag contrast fallback, category slot stability | `web/src/theme/tagColors.test.ts` |

## Self-review

**Spec coverage.** Every named boundary has a task: `TransactionFilterState` (1), `VaultProjection` (2–3), `AppShell` (8), `IdentityMenu` (6), `SidebarFilters` (7), `TransactionDialog` (9), `TransactionsView` (10–11). `PageHeader` is deleted in 5. The colour and type rules are 12. Accessibility, responsive and integration are 13–15. Cleanup and the two spec status edits are 16.

**Order.** Pure modules first, then the seam, then chrome, then views, then the cross-cutting suites. Tasks 1, 2, 5, 7's `taxonomyOrder` and 12 are independent of everything else and can be parallelised. Task 8 depends on 5, 6 and 7. Task 11 depends on 3, 4 and 10.

**Three riskiest tasks.**

1. **Task 11, `TransactionsView`.** It carries five states, two layouts, local paging, virtualisation and the deletion of the file every transaction list currently uses, including Overview's. If it grows past roughly 250 lines, split the card list into its own file rather than letting one component own both layouts.
2. **Task 3, `plaintextProjection`.** It is the pretence that a vault exists. The failure mode is leaking today's shapes through it — a numeric id, an `accountNumber`, a `page`/`pageSize` parameter — which makes the later swap a rewrite instead of a replacement. The 2,000-row ceiling is also a real limit that will be hit by a real dataset before the vault lands; it must surface as a visible "showing the most recent 2,000" message, not a silent truncation.
3. **Task 9, `TransactionDialog`.** Route-backed dialogs are where browser Back, focus return and form state disagree. Submitting must navigate away exactly once, and a reload of `/transactions/new` must open an empty dialog rather than restoring a half-typed form.

**One risk that is not a task.** The `@fluentui/react-datepicker-compat` picker at 0.6.35 is still a v8 compatibility layer and now has to work inside a `Popover`. If it misbehaves there, report it and fall back to two `<input type="date">` fields inside the popover rather than quietly building a custom calendar.

## Open questions resolved by assumption

- **Does clicking a sidebar taxonomy value clear the other taxonomy facets?** The spec says the click "preserves the active space and valid date/account filters, and toggles that taxonomy filter". *Assumption:* only the clicked facet changes; the other two taxonomy facets survive. The enumeration names space, date and account because those come from the toolbar and the author wanted them guaranteed, not because the others are dropped. The opposite reading would contradict "Different facets combine with AND" for every sidebar-driven combination.
- **Which element owns the `h1`?** *Assumption:* `AppShell`, derived from the matched destination. Pages render no title at all. This guarantees exactly one `h1` per page without every page remembering to add one, and it is why `PageHeader` is deleted with no per-page replacement.
- **Is the large title removed only on Transactions, or everywhere?** The spec's product rule is unqualified. *Assumption:* everywhere. Overview, Budgets and Manage lose their visible desktop titles too.
- **Where does "used most recently in a filter" live?** *Assumption:* `localStorage`, per taxonomy kind, capped at 20 ids, written on click. It is a click history, not record content, so it does not belong in the vault and does not need encrypting. It is not synced across devices.
- **How are the five shown values chosen when recency is empty?** *Assumption:* the first five alphabetically. The spec only orders the list; it does not say what a new user sees.
- **What is the personal space's identifier?** *Assumption:* the literal string `personal`, as `?space=personal`. Group spaces use their UUID. The spec writes `<personal-or-group-id>`, which requires the personal case to be a reserved value.
- **What counts as one filter for the count badge?** *Assumption:* a set `from`/`to` pair counts as one, not two. A user thinks of "this month" as one filter.
- **Virtualisation library?** *Assumption:* hand-rolled windowing over the already-fetched array. `@fluentui/react-list-preview` caps at React `<19.0.0` and is banned; adding a third-party virtualiser for a 200-row threshold is not worth a dependency.
- **How is a category's palette slot chosen?** *Assumption:* a stable string hash of the record id, modulo eight. Today's `Math.abs(categoryId) % 8` stops working the moment ids become UUIDs.
- **Does `Add transaction` disable per space or per account?** *Assumption:* per active space — disabled when that space has zero accounts the user may edit, with the explanation adjacent to the button rather than only in a tooltip.
- **`density.ts`.** *Assumption:* delete it. It is imported by nothing but its own test, and its 40px compact row contradicts the 44px the spec fixes. Keeping a dead, wrong constant is worse than breaking the previous plan's unchanged-tests promise for a file that no longer describes anything shipped.
