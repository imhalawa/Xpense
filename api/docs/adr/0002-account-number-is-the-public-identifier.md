---
status: accepted
date: 2026-08-04
supersedes: the "URLs use the database-backed id" rule in docs/contract/api-v1-contract-design.md
---

# Account number is the public identifier

`AccountNumber` becomes the only account identifier a client ever sends or reads — in routes,
request bodies and responses. `Id` stays the primary key and the target of every foreign key,
and stops appearing in responses so nothing can start depending on it.

This reverses a decision made deliberately during the v1 contract reset, which states:
*"Resource URLs use the database-backed `id`, not an account number."*

## Why the reversal

Users will move money to each other. You cannot hand someone another user's database id, and
"send it to account 1000000042" is how people already think about it — the same shape as an
IBAN or a payment handle. An internal surrogate key is the wrong thing to put in front of a
person who is naming somebody else's account.

The two create endpoints also disagreed today, so collapsing them into one `Transaction`
forced a choice regardless: `CreateTransaction.Request` identified an account by
`AccountNumber` while `CreateTransfer.Request` used `SourceAccountId` and
`DestinationAccountId`.

## Considered options

**Keep `Id` public.** Matches the primary key, matches every foreign key, and matches the
existing contract doc — but exposes an internal key to a user who has to name someone else's
account, and it reads as an implementation detail.

**Number in bodies, `Id` in routes.** No reversal of a written decision and less churn, but
the same resource ends up addressed two ways depending on where you look.

**Number everywhere, `Id` still in responses.** Friendlier to a client wanting a stable
opaque key, but two public identifiers means clients pick either, which is the ambiguity this
decision exists to remove.

## Consequences

`Account.Equals`, `GetHashCode` and `IEquatable<Account>` are deleted rather than moved to
`Id`. They were dead code: `Seeder.Seed<T>` is the only caller that needs `IEquatable`, and
`Program.cs:44` seeds `Priority` only. Identity in code is the primary key.

**Account numbers are guessable, and that now matters.** `CreateAccount.cs:22` starts them at
`1_000_000_000` and `NextAccountNumber` increments from the maximum. Today that is harmless,
because nothing lets you act on an account you do not own. Once a cross-user transfer can
*write* to an account named by number, a sequential public identifier lets anyone walk the
range and push transactions into strangers' histories. Non-sequential numbers and an
authorisation check are both required, and both belong with the ownership work rather than
with this rename.

`docs/contract/api-v1-contract-design.md` must be corrected when the code lands.
