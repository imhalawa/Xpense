# Ubiquitous Language

Xpense is a personal-finance ledger. It records money that has already moved and
reports on it. It holds balances in more than one currency but never converts
between them.

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

## Accounts

| Term               | Definition                                                                       | Aliases to avoid            |
| ------------------ | -------------------------------------------------------------------------------- | --------------------------- |
| **Account**        | A named store of **Money** denominated in exactly one **Currency**              | Wallet, ledger, pot, bucket |
| **Account number** | The stable human-facing identifier of an **Account**                            | Code, reference, IBAN       |
| **Default account** | The one **Account** used when a **Transaction** names none                      | Primary, main, fallback     |
| **Deposit**        | To increase an **Account** **Balance** by a **Money** amount                    | Credit, add, top up, fund   |
| **Withdraw**       | To decrease an **Account** **Balance** by a **Money** amount                    | Debit, subtract, deduct     |

An amount may only be applied to an **Account** whose **Currency** matches it.

## Recording money movement

| Term                 | Definition                                                                             | Aliases to avoid                    |
| -------------------- | -------------------------------------------------------------------------------------- | ----------------------------------- |
| **Transaction**      | A single recorded movement of **Money** affecting one **Account**                      | Entry, record, payment, purchase    |
| **Transaction type** | Whether a **Transaction** raised or lowered the **Account** **Balance**                | Kind, direction, sign               |
| **Transfer**         | An atomic movement of **Money** from one **Account** to another                        | Move, swap, internal payment        |
| **Transfer leg**     | One of the two sides of a **Transfer**, naming the **Account** and the direction       | Entry, side, half, line             |
| **Source account**   | The **Account** a **Transfer** takes **Money** from                                    | From, origin, sender                |
| **Destination account** | The **Account** a **Transfer** puts **Money** into                                  | To, target, recipient, receiver     |
| **Reason**           | Free text explaining why a **Transfer** happened                                       | Note, memo, description, comment    |

A **Transfer** is deliberately not a **Transaction**: it touches two **Accounts**
and either records both sides or none. Both **Accounts** and the amount must
share one **Currency**, and the **Source account** must already hold enough
**Money**.

## Classifying a transaction

| Term         | Definition                                                                          | Aliases to avoid              |
| ------------ | ----------------------------------------------------------------------------------- | ----------------------------- |
| **Category** | The single spending class a **Transaction** falls under                             | Type, group, bucket, class    |
| **Priority** | How important a **Category** is, carrying a weight used for reporting               | Importance, rank, level, tier |
| **Merchant** | The external party a **Transaction** was with                                       | Payee, vendor, supplier, shop |
| **Tag**      | A free-form label attached to a **Transaction** alongside its **Category**          | Label, marker, flag, keyword  |
| **Option**   | An input that either points at an existing **Tag** or **Merchant** or creates one   | Lookup, picker, ref, upsert   |

Every **Transaction** must have exactly one **Category** and one **Merchant**.
**Tags** are optional and unlimited.

## Relationships

- An **Account** is denominated in exactly one **Currency**, fixed for its life
- A **Transaction** affects exactly one **Account** — the **Default account** when none is named
- A **Transaction** has exactly one **Category**, exactly one **Merchant**, and zero or more **Tags**
- A **Category** has exactly one **Priority**; a **Priority** covers zero or more **Categories**
- A **Transfer** has exactly two **Transfer legs**: one leaving the **Source account**, one entering the **Destination account**
- A **Transfer leg** belongs to exactly one **Transfer** and names exactly one **Account**
- A **Transfer**'s two **Accounts** and its amount all share one **Currency**

## Example dialogue

> **Dev:** "If someone moves 50 EUR from savings to current, do I write two **Transactions**?"

> **Domain expert:** "No — that's one **Transfer** with two **Transfer legs**. A **Transaction** only ever touches one **Account**. The **Transfer** is what makes both legs land together or not at all."

> **Dev:** "So what **Category** and **Merchant** does a **Transfer** get?"

> **Domain expert:** "Neither. **Category** and **Merchant** describe money entering or leaving the system — a **Transfer** is money you still own, just somewhere else. It carries a **Reason** instead."

> **Dev:** "And if savings is USD and current is EUR?"

> **Domain expert:** "Rejected. We hold both **Currencies** but never convert, so there's no honest rate to apply. The user makes the exchange at their bank and records the result as two separate **Transactions**."

## Flagged ambiguities

- **`Credit` and `Debit` are numbered inconsistently.** `TransactionType` declares
  `Credit, Debit, Transfer` (so `Credit` is `0`); `TransferLegDirection` declares
  `Debit, Credit` (so `Debit` is `0`). The same two words carry opposite numeric
  values in the two enums. Any cast, shared serializer, or column reuse between
  them inverts the direction of money silently. Fix the declaration order so the
  shared names share values, or rename one pair so no one can confuse them.

- **"Transfer" means two different things.** There is a **Transfer** entity with
  two **Transfer legs**, and there is also a `TransactionType.Transfer` value on
  **Transaction**. A reader cannot tell which is meant from the word alone.
  Recommendation: drop `Transfer` from **Transaction type** — a **Transaction**
  affects one **Account**, so it is only ever a rise or a fall. Let the
  **Transfer** entity be the only thing called a transfer.

- **Three vocabularies for "up" and "down".** **Account** exposes
  `Deposit`/`Withdraw`, **Transaction type** says `Credit`/`Debit`, and
  **Transfer leg** direction says `Debit`/`Credit`. Recommendation: keep
  **Deposit** and **Withdraw** as the domain verbs, and treat `Credit`/`Debit`
  as bookkeeping directions on legs only. `Credit` is also ambiguous outside
  double-entry — a bank statement credits the bank, not you.

- **The HTTP contract uses a fourth vocabulary.** The v1 contract exposes
  `type` as `income` or `expense`, which maps onto `Credit` and `Debit`. That
  boundary translation is fine, but it is currently undocumented, so four words
  exist for two states. Recommendation: state the mapping in the contract doc and
  nowhere else.

- **"Name" and "Label" are the same concept.** **Account** has `Name`;
  **Category**, **Tag**, **Merchant**, and **Priority** have `Label`, and
  `IOptionEntity` requires `Label`. Recommendation: **Label** everywhere, since
  the interface already depends on it.

- **"Cents" names a currency-specific unit.** `Money.Cents`,
  `Money.OfCents`, and `Account.BalanceCents` all say cents. It happens to hold
  for `EUR` and `USD`, but the concept is **minor units** and the name will be
  wrong for the first currency that has none. Low urgency, worth fixing before
  a third currency arrives.

- **Amounts in minor units are not named as such.** `Money.Cents` and
  `Account.BalanceCents` say what unit they are in; `Transaction.Amount`,
  `Transfer.Amount`, and `TransferLeg.Amount` are the same integer minor units
  but read as if they might be decimals. Recommendation: one suffix, applied
  everywhere a raw integer amount is stored.

- **An Account has two identities.** Equality is defined on **Account number**,
  but HTTP routes and foreign keys address an **Account** by `Id`. Two accounts
  with the same number and different ids compare equal. Recommendation: decide
  which one is the identity, and make the other a plain attribute.
