# Xpense documentation

Xpense is a self-hosted personal finance ledger. It records money that has already moved and
reports on it. It never moves money itself, and it never converts between currencies.

## Feature guides

| Guide | Covers |
| --- | --- |
| [Getting started](getting-started.md) | Running the stack, creating the first account, the development loop |
| [Users and access](users-and-access.md) | Registration, passkeys, sessions, recovery, groups, invitations |
| [Accounts](accounts.md) | Creating accounts, the fixed currency rule, balances, the default account |
| [Transactions](transactions.md) | Income, expense and transfer; Quick Add; filtering; editing and deleting |
| [Categories](categories.md) | Categories and the necessity scale that ranks them |
| [Tags](tags.md) | Free-form labels and their colours |
| [Merchants](merchants.md) | Who you paid, typeahead, and inline creation |
| [Budgets](budgets.md) | Limits, periods, progress, over-budget reporting, uncounted currencies |
| [Notifications](notifications.md) | What raises a notification and how the worker delivers it |
| [Multi-currency](multi-currency.md) | Why nothing is converted and what that means on screen |
| [Security and encryption](security-and-encryption.md) | The vault, what the server can see, and the data modes |
| [API reference](api-reference.md) | Every HTTP route, grouped by feature |

## How the pieces fit

- **`api/`** — an ASP.NET Core API on .NET 10, PostgreSQL, and a separate notifications worker.
  Organised as vertical slices: one endpoint per file holding its route, request, validator and
  handler.
- **`web/`** — a React 19 client on Vite, built with Fluent UI v9.

Deeper engineering material lives beside the code: architecture decisions in
[`api/docs/adr/`](../api/docs/adr/), the shared vocabulary in
[`api/UBIQUITOUS_LANGUAGE.md`](../api/UBIQUITOUS_LANGUAGE.md), and the slice architecture in
[`api/docs/vertical-slicing-architecture/`](../api/docs/vertical-slicing-architecture/).

## What Xpense deliberately does not do

Reading these first will save you from expecting features that were ruled out on purpose.

- **It does not convert currencies.** There is no rate table and no provider. Amounts in different
  currencies are reported side by side, never summed. See [Multi-currency](multi-currency.md).
- **It does not hold money.** The transfer happens at your bank or payment app; Xpense records that
  it happened.
- **A budget never blocks a transaction.** It reports, and that is all. See [Budgets](budgets.md).
- **There is no operator password reset.** Nobody can recover your data for you, by design. See
  [Security and encryption](security-and-encryption.md).

## Built but not yet on screen

These are implemented and tested in the API, and reachable over HTTP, but no part of the client
calls them yet. They are documented here so the gap is explicit rather than surprising.

- Groups, resource grants and invitations — see [Users and access](users-and-access.md)
- Recovery-file and recovery-password sign-in
- Registering an additional passkey
- Budget alert thresholds, which are stored and reported but raise no notification
