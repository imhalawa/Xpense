# Xpense

Self-hosted personal finance, where the server cannot read your spending.

Xpense is a ledger for money that has already moved. You record what you spent, on what, and to
whom; it tells you where your money went and warns you when a budget is gone. It holds several
currencies at once and never converts between them, because a converted number in a ledger is a
number nobody can reproduce.

![The Xpense overview](docs/images/overview.png)

## Why it exists

Most expense trackers ask you to hand a company your complete financial history. Xpense is the
other trade: you run it, your browser holds the keys, and the database holds ciphertext. You give
up cloud convenience and an operator who can reset your password. You get a ledger nobody else can
read.

**It suits you if** you want to self-host, you care that the operator cannot read your data, and
you are willing to keep a recovery file safe.

**It does not suit you if** you want a hosted service, automatic bank imports, or someone to call
when you lose every key. There is no operator override, by design.

## What it does

| | |
| --- | --- |
| **Passkey sign-in** | No passwords. The same passkey proves who you are and unlocks your data |
| **Accounts** | Any number, each fixed to one currency for life |
| **Transactions** | Income, expense and transfer, with a natural-language Quick Add |
| **Categories** | Ranked on a necessity scale, so you can see what was avoidable |
| **Tags and merchants** | Free-form labels and who you paid, created inline as you type |
| **Budgets** | Weekly, monthly, yearly or one-off. They report and never block |
| **Multi-currency** | Balances and budgets per currency. Nothing is ever converted |
| **Sharing** | Share one account or budget with a group, without exposing the rest — API only so far |
| **Notifications** | Told once when a budget is crossed, not nagged afterwards |
| **Encrypted vault** | Keys live in your browser; the server stores ciphertext it cannot open |

## Quick start

```bash
make start
```

That runs PostgreSQL, the migrations, the API and the notifications worker in containers, then the
web client. Open `http://localhost:5173` and choose **Create one** to register.

You need a browser and authenticator that support **passkeys with the PRF extension** — that PRF
output is what your vault key is derived from, so an authenticator without it is refused rather
than left with a vault it could never unlock.

Full instructions, including the `make` targets and configuration, are in
[docs/getting-started.md](docs/getting-started.md).

## Documentation

Start at [docs/README.md](docs/README.md).

| Guide | Covers |
| --- | --- |
| [Getting started](docs/getting-started.md) | Running the stack and creating your first account |
| [Users and access](docs/users-and-access.md) | Passkeys, sessions, recovery, groups, invitations |
| [Accounts](docs/accounts.md) | Currencies, balances, the default account |
| [Transactions](docs/transactions.md) | The three kinds, Quick Add, filtering, editing |
| [Categories](docs/categories.md) | Categories and the necessity scale |
| [Tags](docs/tags.md) · [Merchants](docs/merchants.md) | Labels, colours, typeahead, inline creation |
| [Budgets](docs/budgets.md) | Periods, progress, over-budget, uncounted currencies |
| [Notifications](docs/notifications.md) | What raises one, and how delivery works |
| [Multi-currency](docs/multi-currency.md) | Why nothing converts |
| [Security and encryption](docs/security-and-encryption.md) | The vault, the threat model, data modes |
| [API reference](docs/api-reference.md) | Every route, grouped by feature |

## A look at it

| | |
| --- | --- |
| ![Transactions](docs/images/transactions.png) | ![Budgets](docs/images/budgets.png) |
| **Transactions** — filter by date, account, category, tag and merchant | **Budgets** — progress, over-budget, and spending in a currency this budget cannot count |

![Adding a transaction](docs/images/transaction-form.png)

Quick Add reads a plain sentence and fills in what it recognises. The parser is deterministic — no
model and no network call — and it marks what it could not resolve instead of guessing.

## How it is built

| Tree | What it is |
| --- | --- |
| [`api/`](api/) | ASP.NET Core on .NET 10, PostgreSQL, and a separate notifications worker |
| [`web/`](web/) | React 19 and Fluent UI v9 on Vite, in TypeScript |

The API is organised in vertical slices: one endpoint per file holding its route, request,
validator and handler, with slices that never reference each other. Architecture decisions are
recorded in [`api/docs/adr/`](api/docs/adr/), the shared vocabulary in
[`api/UBIQUITOUS_LANGUAGE.md`](api/UBIQUITOUS_LANGUAGE.md), and conventions for both trees in
[`AGENTS.md`](AGENTS.md).

## Honest limitations

Worth knowing before you rely on it:

- **Not fully zero-knowledge yet.** Encrypted records, group names and envelopes are stored as
  ciphertext, but the older plaintext financial tables have not been migrated into the encrypted
  store. Until that lands, do not describe the whole database as unreadable.
- **Recovery is not in the interface.** Recovery files and recovery passwords are implemented
  end to end in the API and the cryptography, but the sign-in page currently offers passkeys only.
- **Groups, sharing and invitations have no interface.** The whole API surface exists and is
  tested, but no screen calls it yet, so today it is reachable only over HTTP. The same applies to
  registering a second passkey.
- **Alert thresholds do not notify.** A budget stores and reports its threshold, but only crossing
  the limit itself raises a notification.
- **EUR and USD only.**
- **Analytics is one endpoint**, reporting today's spending by category. There are no charts yet.
- **Lose every key and the data is gone.** Nobody can recover it for you.

## Running the tests

```bash
make test          # the .NET suite; needs Docker for Testcontainers
cd web && npm test # the web suite
```

## CI

`api/**` and `web/**` have separate workflows, each triggered only by changes in its own tree. The
API job builds, tests, reports coverage and smoke-tests the whole compose stack. The web job
installs from the lockfile, builds, runs the module-init check and audits dependencies.
