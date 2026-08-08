# Xpense product-readiness implementation

Date: 2026-08-09
Status: in progress

## Goal

Turn the current Fluent shell and encrypted-storage foundation into a coherent product surface.
Every visible action must perform its named behavior. No placeholder dialog, no dead callback and
no route-specific escape from a global state is allowed.

## Binding product decisions

- The sidebar follows the compact Todoist direction already agreed in the web-shell design.
- The active personal or group space owns newly created accounts and taxonomy values.
- Accounts, categories, tags and merchants live as expandable sidebar sections with create and
  item context actions. The separate Manage destination disappears after its responsibilities move.
- Selecting a sidebar filter never reorders the list.
- Transactions open for editing from their row and expose edit/delete through an accessible action
  menu. Dialog routes preserve filters and return focus.
- The vault can be locked and unlocked from every route. The shell never offers Lock while locked.
- Notification and sidebar controls live in the header. Theme and lock state live in the identity
  menu. The bottom control shelf disappears.
- Quick Add follows `docs/superpowers/specs/2026-08-07-data-entry-design.md`, including live
  Todoist-style decoration of recognised text.
- Authentication, groups, invitations, permissions and encrypted sharing must use the existing
  identity and zero-knowledge specifications. UI must not simulate unavailable server behavior.

## Tasks

### Task 1: Quick Add parser and decorated input

Implement the deterministic parser, resolution results, source ranges, decorated input, accessible
feedback, table-driven tests and integration with transaction creation and editing.

### Task 2: Global vault and identity controls

Make lock/unlock global, move header/theme controls, remove placeholder identity dialogs, add the
avatar gap and connect every identity action to real routes and behavior.

### Task 3: Transaction update and deletion

Add vertical-slice API endpoints and encrypted projection operations where missing. Add route-backed
editing, row and keyboard activation, an action menu, permission behavior and confirmed deletion.

### Task 4: Sidebar resource management

Implement account, category, tag and merchant create/edit/delete flows in the sidebar. Preserve
server or configured order independently of filter selection. Remove Manage after all functions
have a destination.

### Task 5: Identity and groups completion

Complete the remaining tasks in
`docs/superpowers/plans/2026-08-08-identity-groups-and-sharing.md`: authentication configuration,
passkey and recovery ceremonies, sessions, boundary enforcement, groups, memberships, invitations,
grants, notification delivery, errors and operator documentation.

### Task 6: Encrypted-vault completion

Complete Tasks 21–32 in
`docs/superpowers/plans/2026-08-08-zero-knowledge-vault-and-recovery.md`: envelope grant/revoke,
blindness gates, client sync and projection, offline outbox/conflicts, client domain migration,
browser security, claim mode rehearsals and the unapplied contract migration artifact.

### Task 7: Product audit and release gates

Search for placeholders, no-op callbacks, dead navigation and visible controls without behavior.
Run API build/tests, web typecheck/tests/build, browser accessibility and responsive suites, encrypted
network assertions and full-stack user journeys for claim, unlock, CRUD, sharing and recovery.

## Completion criteria

- Every task above is complete and reviewed.
- No visible action is a placeholder or no-op.
- The repository is warning-free under the project verification commands, except explicitly
  documented third-party bundler notices that cannot be removed locally.
- Destructive contract migration remains written but unapplied until its rehearsal gate passes.
