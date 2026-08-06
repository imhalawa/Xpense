# Ubiquitous Language

Xpense is a personal-finance ledger. It records money that has already moved and
reports on it. It holds balances in more than one currency but never converts
between them.

Xpense never holds money itself. The movement happens somewhere else — a real bank,
or a payment app such as Tikkie — and Xpense records the fact that it happened.
Users will be able to record money moving to each other on the platform, which is
the one place where the record is shared rather than personal.

> This model is implemented, [Budgeting](#budgeting) included. See
> [docs/model-rename-pass.md](docs/model-rename-pass.md) for what changed and the ADRs in
> [docs/adr/](docs/adr/) for why. **User** and **Owner** are the exception: they are in the language
> because the **Transaction** model depends on them, and they do not exist in code yet.

## Money

| Term            | Definition                                                                     | Aliases to avoid                      |
| --------------- | ------------------------------------------------------------------------------ | ------------------------------------- |
| **Money**       | An amount in a single currency, held as a whole number of minor units          | Amount, value, decimal, sum           |
| **Currency**    | The denomination a **Money** or **Account** is expressed in (`EUR`, `USD`)     | Denomination, ccy                     |
| **Minor units** | The smallest indivisible unit of a **Currency**, the integer **Money** stores  | Cents, pennies, fractional part       |
| **Balance**     | The **Money** currently held in an **Account**                                | Total, sum, current amount            |

**Money** is a value object, never a bare number. Two **Money** values can only be
added, subtracted, or compared when their **Currency** matches; otherwise the
operation is rejected. There is no conversion and no exchange rate anywhere in
the system.

**Minor units** is the word wherever an integer amount appears — in code, in the database
and on the wire. "Cents" is true of `EUR` and `USD` and will be wrong for the first
currency without them.

## Accounts

| Term               | Definition                                                                       | Aliases to avoid            |
| ------------------ | -------------------------------------------------------------------------------- | --------------------------- |
| **Account**        | A named store of **Money** denominated in exactly one **Currency**              | Wallet, ledger, pot, bucket |
| **Account number** | The stable, public identifier of an **Account**                                  | Code, reference, IBAN       |
| **Default account** | The one **Account** a client should offer first                                  | Primary, main, fallback     |
| **User**           | A person who uses Xpense                                                         | Account, customer, client   |
| **Owner**          | The **User** an **Account** belongs to                                           | Tenant, holder, member      |
| **Deposit**        | To increase an **Account** **Balance** by a **Money** amount                    | Credit, add, top up, fund   |
| **Withdraw**       | To decrease an **Account** **Balance** by a **Money** amount                     | Debit, subtract, deduct     |

An amount may only be applied to an **Account** whose **Currency** matches it.

**Account number** is the only identifier a client ever sends or reads. The database key
exists but is not public — see
[ADR 0002](docs/adr/0002-account-number-is-the-public-identifier.md).

**User** and **Owner** are in the language because the **Transaction** model depends on
them: a **Transfer** is cross-user when its two **Accounts** have different **Owners**.
Neither exists in code yet.

## Recording money movement

| Term                    | Definition                                                                                  | Aliases to avoid                          |
| ----------------------- | ------------------------------------------------------------------------------------------- | ----------------------------------------- |
| **Transaction**         | A single recorded movement of **Money**, naming an **Account** on each side that is inside Xpense | Entry, record, payment, purchase, leg, posting |
| **Transaction kind**    | Whether a **Transaction** is **Income**, **Expense** or **Transfer**                        | Type, direction, sign                     |
| **Income**              | A **Transaction** whose **Money** came from outside Xpense                                  | Credit, deposit, inflow, earning          |
| **Expense**             | A **Transaction** whose **Money** went outside Xpense                                       | Debit, withdrawal, outflow, spend         |
| **Transfer**            | A **Transaction** between two **Accounts** inside Xpense                                    | Move, swap, internal payment              |
| **Source account**      | The **Account** a **Transaction** took **Money** from; absent on **Income**                 | From, origin, sender                      |
| **Destination account** | The **Account** a **Transaction** put **Money** into; absent on **Expense**                 | To, target, recipient, receiver           |
| **Occurred at**         | When the **Money** actually moved, as told to Xpense                                        | Date, timestamp, when, created            |
| **Created at**          | When Xpense wrote the row; never supplied by a client                                       | Recorded, entered, inserted               |
| **Reason**              | Free text explaining why a **Transaction** happened                                         | Note, memo, description, comment          |

One entity records all three kinds. Each side is either an **Account** inside Xpense
or nothing, and nothing means the **Money** crossed the system boundary — in which case
the **Merchant** names who was on that side. Every **Transaction** therefore says where
the money came from and where it went.

**Transaction kind** is derived from which sides are **Accounts**, never stored, so a
**Transaction** cannot contradict itself:

| Kind         | Source account | Destination account |
| ------------ | -------------- | ------------------- |
| **Income**   | absent         | an **Account**      |
| **Expense**  | an **Account** | absent              |
| **Transfer** | an **Account** | another **Account** |

A **Transfer** is cross-user when its two **Accounts** have different **Owners**. That
is also derived, not stored.

**Occurred at** and **Created at** are different facts and both are recorded. A purchase
made last month and entered today has last month's **Occurred at** and today's **Created
at**. Reporting and ordering use **Occurred at**; **Created at** exists so a shared record
can show when each side entered what.

## Classifying a transaction

| Term         | Definition                                                                                          | Aliases to avoid              |
| ------------ | --------------------------------------------------------------------------------------------------- | ----------------------------- |
| **Merchant** | The party on the other side of a **Transaction**, when that side is outside Xpense                  | Payee, vendor, supplier, shop |
| **Category** | The single spending class a **Transaction** falls under                                             | Type, group, bucket, class    |
| **Priority** | How important a **Category** is, carrying a weight used for reporting                               | Importance, rank, level, tier |
| **Tag**      | A free-form label attached to a **Transaction** alongside its **Category**                          | Marker, flag, keyword         |
| **Option**   | An input that either points at an existing **Tag** or **Merchant** or creates one                   | Lookup, picker, ref, upsert   |
| **Label**    | The human-readable name of an **Account**, **Category**, **Priority**, **Merchant** or **Tag**      | Name, title, description      |

**Merchant** and **Category** answer different questions. **Merchant** is *who* — it is the
counterparty, and it is the only record of the side that is outside Xpense. **Category** is
*what kind* — your own lens on your spending, used for reporting. The same purchase can be
filed under a different **Category** without changing who was paid.

A **Transaction** must have exactly one **Merchant** and exactly one **Category** when one of
its sides is outside Xpense, and must have neither when both sides are **Accounts**. A
**Transfer** between your own **Accounts** has no shop and no spending class. **Tags** are
optional and unlimited on any **Transaction**.

**Label** is the one word for a human-readable name, in code, in the database and on the wire.

## Budgeting

| Term              | Definition                                                                              | Aliases to avoid                          |
| ----------------- | --------------------------------------------------------------------------------------- | ----------------------------------------- |
| **Budget**        | An intended limit on **Expense** for one **Category**, in one **Currency**, over a **Budget period** | Cap, allowance, envelope, target, quota   |
| **Budget period** | The window of time a **Budget** measures **Expenses** in: a calendar week, month or year when it repeats, otherwise any stretch of dates | Cycle, month, term, range |
| **Recurrence**    | How a **Budget** repeats: not at all, or once per week, month or year                     | Schedule, frequency, interval, repeat     |
| **Spent**         | The total **Money** of **Expenses** in a **Budget**'s **Category** and **Currency** within its period | Used, consumed, actual, burn         |
| **Remaining**     | A **Budget**'s amount minus **Spent**, negative once exceeded                            | Left, available, **Balance**              |

A **Budget** reports and never blocks. Xpense records money that has already moved, so a
**Budget** cannot refuse a **Transaction** — refusing the record would not un-spend the
money, it would only make Xpense disagree with the bank.

**Remaining** is deliberately not called a balance. **Balance** is **Money** an **Account**
holds; **Remaining** is the unused part of an intention, and the two must not be confused.

Only **Expenses** count. A **Transfer** has no **Category** and is money you still own, so
it is never spending. **Income** is not spending either. Like all reporting, a **Budget**
measures by **Occurred at**, so a purchase entered today but made last month counts against
last month.

A **Budget** has one **Currency**, and only **Expenses** in that **Currency** count toward
it — nothing converts here either. A **Category** spans **Accounts** and therefore
**Currencies**, so spending in another **Currency** is reported as explicitly not counted
rather than dropped in silence. Two **Currencies** worth of groceries means two **Budgets**.

One entity covers both shapes. A one-off **Budget** has no **Recurrence** and one fixed window;
a repeating one has a **Recurrence** and may run indefinitely. There is no separate "recurring
budget" — that would be two entities differing by one column, which is the mistake
[ADR 0001](docs/adr/0001-one-transaction-entity-with-two-nullable-sides.md) already corrected
once.

A repeating **Budget**'s periods are calendar weeks, months or years, so each has a name —
`2026-W32`, `2026-08`, `2026` — that a client, a report and a notification can all mean the same
thing by. A one-off **Budget** may cover any stretch of dates.

**Budgets** are independent of one another. Xpense does not compare them, rank them, or refuse
one for overlapping another: several may cover the same **Category** at once, in different
**Currencies**, over different lengths, or over the very same days. **Spent** and **Remaining**
belong to a **Budget** and not to a **Category**, so each **Budget** answers only for itself and
no rule decides which of two applies. Keeping a set of **Budgets** coherent is the user's
business, not Xpense's.

## Notifications

| Term                  | Definition                                                                                   | Aliases to avoid                        |
| --------------------- | -------------------------------------------------------------------------------------------- | --------------------------------------- |
| **Event**             | A fact that something happened in Xpense, published once and never changed afterwards         | Message, command, trigger, hook, signal |
| **Event attributes**  | The metadata identifying an **Event**: which one it is, what kind, when, and what raised it    | Header, envelope, metadata              |
| **Event body**        | The facts particular to one kind of **Event**                                                 | Payload, data, content, args            |
| **Notification**      | Something a **User** should be told about, decided from an **Event**                          | Alert, message, toast, push, reminder    |
| **Notification kind** | What class of thing a **Notification** tells you about                                        | Type, category, severity                |
| **Notification rule** | The thing that decides, from one **Event**, whether a **Notification** of one kind is warranted | Handler, policy, trigger, checker      |
| **Alert threshold**   | The share of a **Budget** at which it is worth saying something, before the limit is reached   | Warning level, soft limit, buffer       |
| **Read**              | That the recipient has seen a **Notification**                                                | Seen, acknowledged, dismissed, archived |

**Event** and **Notification** are not the same thing and must not be used for each other. An
**Event** is a fact, stated by whichever part of Xpense the thing happened in, and it is
published whether or not anyone cares. A **Notification** is a judgement that somebody should
be told, made after the fact by whatever consumes **Events**. Most **Events** produce no
**Notification** at all.

That separation is the point. The part of Xpense that records a **Transaction** does not know
what is worth telling anyone, and nothing it does depends on the answer.

An **Event** is immutable and is never edited or withdrawn. A correction is a new **Event**, the
same way a mistaken **Transaction** is corrected by recording another one rather than by
rewriting history.

A **Notification** carries both the facts it was built from and text rendering them. The facts
are what it is: they are what a client acts on, links from, or groups by. The rendered text
exists so that anything which only needs to show the **Notification** can do so without
knowing what kind it is.

Each **Notification kind** is decided by its own **Notification rule**, which knows nothing
about any other rule. One **Event** may satisfy several rules and so produce several
**Notifications**, or satisfy none and produce nothing.

An **Alert threshold** belongs to a **Budget** and defaults to three quarters of it. A
**Budget** may have none, meaning it says nothing until the limit itself is passed.

Xpense tells you about the *crossing*, not the state. Passing the **Alert threshold** and
passing the limit are each said once, because each is a thing that happened; spending further
while already over is also a thing that happened, so it is said again.

## Relationships

- An **Account** is denominated in exactly one **Currency**, fixed for its life
- An **Account** belongs to exactly one **Owner**
- A **Transaction** names zero or one **Source account** and zero or one **Destination account**, and at least one of the two
- A **Transaction** with exactly one **Account** has exactly one **Category** and exactly one **Merchant**
- A **Transaction** with two **Accounts** has neither a **Category** nor a **Merchant**
- A **Transaction** has zero or more **Tags**
- A **Category** has exactly one **Priority**; a **Priority** covers zero or more **Categories**
- A **Transfer**'s two **Accounts** and its amount all share one **Currency**
- A **Budget** covers exactly one **Category** and is denominated in one **Currency**
- A **Category** has zero or more **Budgets**, which need not agree with each other
- A **Budget** counts **Expenses** only, never **Income** and never **Transfers**
- A **Budget** has zero or one **Alert threshold**
- An **Event** has exactly one **Event body** and one set of **Event attributes**
- An **Event** produces zero or more **Notifications**, one per **Notification rule** it satisfies
- A **Notification** names the one **Event** it came from

## Example dialogue

> **Dev:** "If someone moves 50 EUR from savings to current, is that one record or two?"

> **Domain expert:** "One **Transaction**. **Source account** is savings, **Destination account** is current. Both sides are known, so it is a **Transfer**."

> **Dev:** "So what **Category** and **Merchant** does it get?"

> **Domain expert:** "Neither. **Category** and **Merchant** describe money crossing the boundary — a **Transfer** is money you still own, just somewhere else. It carries a **Reason** instead."

> **Dev:** "And when I buy groceries, what goes in the **Destination account**?"

> **Domain expert:** "Nothing. The shop is not in Xpense. That is what the **Merchant** is for — it names the side we do not hold an **Account** for. The **Category** tells you it was groceries."

> **Dev:** "What if savings is USD and current is EUR?"

> **Domain expert:** "Rejected. We hold both **Currencies** but never convert, so there is no honest rate to apply. The user makes the exchange at their bank and records the result as two separate **Transactions**."

> **Dev:** "And if I send 25 EUR to another user?"

> **Domain expert:** "Still one **Transaction**, still a **Transfer**. The **Destination account** just has a different **Owner**. Xpense did not move the money — Tikkie did. We are recording that it happened."

> **Dev:** "I recorded a purchase from last month. Is it last month's spending or today's?"

> **Domain expert:** "Last month's. **Occurred at** is when the money moved and that is what reports use. **Created at** says you typed it in today."

## Resolved

Every ambiguity previously flagged in this file has been decided. The reasoning is in
[ADR 0001](docs/adr/0001-one-transaction-entity-with-two-nullable-sides.md),
[ADR 0002](docs/adr/0002-account-number-is-the-public-identifier.md) and
[ADR 0003](docs/adr/0003-generated-openapi-is-the-contract.md); the resulting renames are
listed in [docs/model-rename-pass.md](docs/model-rename-pass.md).

- **"Transfer" no longer means two things.** There is no `Transfer` entity. **Transfer** is
  one of the three **Transaction kinds**, derived rather than stored.
- **"Transfer leg" is gone.** The entity carried nothing its parent did not already hold, so
  it was deleted rather than renamed. "Leg" was trading vocabulary in any case.
- **`Credit` and `Debit` are gone.** Both enums that held them are deleted, and the stored
  `TransactionType` column is replaced by a derived **Transaction kind**. They can no longer
  be numbered inconsistently because they no longer exist.
- **Two vocabularies for up and down, not three.** **Deposit** and **Withdraw** are the verbs
  that change a **Balance**; **Income** and **Expense** classify a **Transaction**. These are
  genuinely different concepts — a **Transfer** changes two balances while being neither
  income nor expense — so two pairs is correct and a third was not.
- **Label everywhere.** One word for a human-readable name, and the requests that used to
  send `name` while the responses returned `label` now agree.
- **Minor units everywhere.** In code, in the database and on the wire, replacing "cents".
  Every stored integer amount says its unit.
- **Row time and business time are separate.** **Occurred at** is a column of its own, and
  **Created at** is never supplied by a client. One `At` suffix and one ISO 8601 format for
  every timestamp on the wire.
- **An Account has one identity.** **Account number** is public; the database key is not.
  Equality on the entity is deleted, because nothing used it.
- **The rule that moves Money lives on Transaction.** Static factories, one per kind, each
  enforcing its own invariants — so income and expense are guarded as well as transfers.
- **`/transfers` is gone.** One entity, one resource: `/transactions` covers all three kinds.
- **"Option" stays.** It is a deliberate term with a clear definition, it never reaches the
  wire, and renaming it would trade one word for another on this file's own avoid list.

## Not in code yet

- **User** and **Owner** are named here but do not exist: no entity, no ownership column, no
  query scoping, and no authentication anywhere to enforce them. That work comes next, as one
  piece, together with transfer authorisation and receiver confirmation.
- A **Transaction** can currently be created against another **User**'s **Destination
  account**, changing their **Balance** without their agreement.
- The rule rejecting a **Transfer** when the **Source account** **Balance** is too low is
  correct between your own **Accounts** and probably wrong for a cross-user record, because
  the real payment already happened elsewhere.
