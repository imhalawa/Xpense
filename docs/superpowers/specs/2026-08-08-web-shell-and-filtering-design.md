# Xpense web shell and transaction filtering

Date: 2026-08-08
Status: written for user review
Scope: the browser shell, active-space navigation, transaction entry, and local filtering
Depends on: `2026-08-08-identity-groups-and-sharing-design.md` and
`2026-08-08-zero-knowledge-vault-and-recovery-design.md`
Supersedes: the navigation and transaction-filter transport in
`2026-08-07-information-architecture-design.md`; the large page-header treatment in
`2026-08-08-fluent-migration-design.md`

## Outcome

The web app should feel closer to Todoist: quiet chrome, a useful sidebar, compact content, and one
obvious action. The selected sidebar destination already provides page context on desktop, so the
Transactions page does not repeat a large visible title.

This remains a React and Fluent UI web application. Native Android, iOS, macOS, and Windows clients
are deferred. The browser is the trusted client for this phase and unlocks the encrypted vault
locally.

## Product rules

- The user identity replaces the `Xpense` wordmark at the top of the sidebar.
- `Add transaction` sits directly below the identity control and opens from every page.
- Categories, tags, and merchants appear as curated sidebar sections and filter Transactions with
  one click.
- The active space is either Personal or one group. There is no mixed `All spaces` view in this
  phase, because totals from different privacy and permission contexts are easy to misread.
- A viewer can inspect shared transactions. Only an owner or editor can create, edit, or delete
  them.
- The large desktop page title is removed. A semantic `h1` remains visually hidden for assistive
  technology. Mobile shows a compact visible title because the selected sidebar item is hidden.

## Desktop shell

The inline sidebar is 280 pixels wide. Its order is fixed:

1. User control: initials or avatar, display name, and active-space name.
2. Brand-coloured `Add transaction` action.
3. Primary destinations: Overview, Transactions, Budgets, Manage.
4. One-at-a-time collapsible sections: Categories, Tags, Merchants.
5. Notification, appearance, and vault-lock actions at the bottom.

Each taxonomy section shows at most five values. Values used most recently in a filter come first;
unused values follow alphabetically. `View all` opens a searchable popover. Expanding one section
collapses the other two. This keeps the sidebar useful when a user has hundreds of merchants.

The selected destination uses the brand tint, a brand-coloured leading marker, and stronger text.
Selected filter values use the same treatment at lower emphasis. The selected state must not rely
on colour alone.

The main content begins at the top of its surface rather than vertically centring sparse content.
It uses a maximum width of 1,200 pixels, 32 pixels of desktop padding, and Fluent spacing tokens
inside components. Transaction rows are compact at 44 pixels; forms and overview cards remain
comfortable.

## User and space menu

Selecting the identity control opens one menu with:

- Personal and every group the user belongs to, with the active space checked.
- `Manage groups`.
- `Account and passkeys`.
- `Lock vault`.
- `Sign out`.

Changing space updates the URL and reloads the local projection for that space. Locking clears
decrypted state without ending the authenticated server session. Signing out locks first and then
clears the session cookie.

## Transaction page

Desktop starts with a compact toolbar, not a hero header. It contains the date-range summary, an
account filter, a filter count, and `Clear filters`. Date fields live in a popover instead of
occupying a permanent row. Mobile shows `Transactions` above the toolbar.

The transaction list follows these rules:

- Desktop uses a semantic table; mobile uses a card list with equivalent information.
- Amount is first, followed by date, category, merchant, account, and tags.
- Ordinary expenses use the existing muted-red token, income uses blue, and alerts use the stronger
  red plus an icon. Transfers use neutral ink.
- An empty unfiltered space explains how to add the first transaction. An empty filtered result
  says no matches and offers `Clear filters`.
- Loading shows row skeletons. A locked vault replaces the list with one `Unlock` action. A sync
  failure keeps cached rows visible and shows a non-blocking status message.

`Add transaction` is owned by the shell, not the Transactions page. It opens a route-backed dialog
so browser navigation and reload behave predictably. The dialog lists only accounts the current
user may edit in the active space. If there are none, the action is disabled with an explanation.

## Filter behaviour

The URL is the source of truth. The form is:

`/transactions?space=<personal-or-group-id>&category=<id>&merchant=<id>&tag=<id>&account=<id>&from=<date>&to=<date>`

Category, merchant, tag, and account each allow one selected value in this phase. Different facets
combine with AND. The date range is inclusive in the UI and converted to a half-open range in the
local query. Invalid or inaccessible identifiers are removed from the URL and reported once in a
small message.

Clicking a sidebar taxonomy value navigates to Transactions, preserves the active space and valid
date/account filters, and toggles that taxonomy filter. Clicking the selected value clears it.
`Clear filters` preserves only the active space.

Filtering is local after vault unlock. The server cannot query encrypted category, merchant, tag,
amount, or occurrence data. The encrypted sync cache therefore contains the accessible records for
the active space, and a local projection builds the table and sidebar counts. Pagination is local;
the UI renders 50 rows at a time and virtualises after 200 rows.

## Colour and typography

The palette is not deferred:

- Brand actions and selection: the existing `#1565C0` Fluent brand ramp.
- Category markers: the validated eight-slot category palette in `web/src/theme/tokens.ts`.
- Tags: their stored foreground/background colours after contrast validation; invalid pairs fall
  back to neutral Fluent badge colours.
- Merchants: neutral icons so merchants do not compete with categories and tags.
- Income/expense: the existing blue and muted-red pair.
- Errors and destructive actions: Fluent danger tokens, never the brand colour.

Fluent's system font stack remains. Components use Fluent type roles rather than manual font sizes:
`Subtitle1` for the mobile page title, `Body1` for rows and navigation, `Caption1` for secondary
metadata, and tabular figures for amount columns. Desktop body text never drops below 14 pixels.

## Component boundaries

- `AppShell`: responsive frame, session state, notifications, lock, and global transaction dialog.
- `IdentityMenu`: user, spaces, account actions, lock, and sign-out.
- `SidebarFilters`: taxonomy section layout and URL navigation only.
- `TransactionFilterState`: parses and serialises the URL; contains no rendering.
- `VaultProjection`: exposes decrypted, authorised view models and local filter operations.
- `TransactionsView`: toolbar, empty/loading/error states, and responsive list.
- `TransactionDialog`: create/edit form and permission-aware account choices.

No UI component calls Axios directly. Server authentication and encrypted sync clients sit below
the session and vault stores.

## Accessibility and responsive behaviour

- All controls have a visible focus indicator and work by keyboard.
- Sidebar sections use buttons with `aria-expanded`; the searchable `View all` popover returns focus
  to its trigger.
- The drawer becomes an overlay below 1,024 pixels and closes after navigation.
- Mobile keeps a 44-pixel minimum target even though desktop transaction rows are denser.
- Icons always have a label or accessible name.
- Reduced-motion preference disables drawer, dialog, and selection animation.
- Focus moves into the transaction dialog when opened and returns to `Add transaction` when closed.

## Testing

- Unit tests cover URL parsing, toggle behaviour, AND semantics, invalid identifiers, date ranges,
  and local pagination.
- Component tests cover the identity menu, one-open taxonomy sections, viewer/editor actions,
  empty states, locked state, and the mobile title.
- Accessibility tests cover landmark structure, the hidden desktop heading, focus return, labels,
  and keyboard navigation.
- Integration tests load encrypted fixtures into the local vault projection and prove that sidebar
  selections filter without an API query.
- Responsive snapshots cover 390, 768, 1,024, and 1,440 pixel widths in light and dark themes.

## Acceptance criteria

- Desktop Transactions has no redundant large visible page title.
- The sidebar starts with the signed-in user and active space, not the app name.
- `Add transaction` works from every destination and respects editor permissions.
- A category, tag, or merchant filters Transactions with one click and is reflected in the URL.
- The palette, Fluent typography, and spacing rules are visible in the finished shell.
- Reloading an unlocked route restores the encrypted cache and asks for unlock before revealing it.
- No filter sends private financial fields or labels to the server.

## Out of scope

- Native clients.
- Multi-select within one filter facet.
- An `All spaces` aggregate.
- Server-side full-text search over encrypted data.
- New analytics screens or hosted billing UI.
