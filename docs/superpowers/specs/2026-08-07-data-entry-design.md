# Xpense data entry

Date: 2026-08-07
Status: agreed, not yet implemented
Depends on: `2026-08-07-design-standard-design.md`, `2026-08-07-information-architecture-design.md`
Related: `2026-08-07-auth-and-users-design.md` supplies the per-user AI token this spec consumes.

## Why

Data entry is the hardest part of an expense tracker. Not because typing is slow, but because
the transaction you never log is the one that ruins the numbers. Every input method below exists
to lower the cost of capture.

The design goal is explicit: **traditional form components stay relevant by getting better, not by
being protected.** They earn their place as the confirmation surface that makes every other input
method safe to trust.

## Architecture: one draft, many adapters

Every input path produces the same client-side `TransactionDraft`. The form renders that draft.
Nothing reaches the API until the draft is complete and the user confirms.

```
form ─────────────┐
quick-add text ───┤
voice ────────────┼──▶ TransactionDraft ──▶ the form (confirmation) ──▶ POST /api/v1/transactions
LLM parse ────────┤
receipt photo ────┤
CSV row ──────────┘
```

Two consequences worth stating plainly:

- **Adding an input method is adding an adapter, not a feature.** Six inputs share one
  confirmation surface, one validation path and one API call.
- **No adapter can save a wrong value silently.** Confidence lives in the draft, not in the save.

`TransactionDraft` mirrors `CreateTransaction.Request` field for field, plus resolution state:

| Field | Draft holds |
|---|---|
| amount | minor units + currency, or the raw text that failed to parse |
| occurredAt | a date, defaulting to now |
| sourceAccount / destinationAccount | a resolved account number, or an unresolved label |
| category | a resolved id, or an unresolved label plus an intent to create |
| merchant | a resolved id, or a label with `create: true` |
| tags | resolved ids, or labels with `create: true` |
| reason | free text, transfers only |
| unresolved | every token the adapter could not place |

## What the API already supports

Read from `CreateTransaction.cs` rather than assumed:

- `OptionRequest(int? Id, string Label, bool Create)` with an `OptionResolver<T>` means **merchants
  and tags are created inline** by the transaction endpoint. No separate create call, no
  `CreateMerchant` endpoint needed.
- `CategoryId` is an int and a missing one throws `CategoryNotFoundException`. Categories are a
  closed set at transaction time. A separate `CreateCategory` endpoint does exist.
- Accounts resolve by `AccountNumber`, a string, not by id.
- Kind is inferred from which accounts are named, and the validator enforces it:
  source only → Expense, destination only → Income, both → Transfer. A transfer **must not** carry
  a category or merchant, and may carry a `Reason`.
- A transfer requires matching currencies on both accounts and sufficient funds in the source.
- Amount crosses as `MoneyRequest(long MinorUnits, string Currency)`; the currency must satisfy
  `CurrencyParser`.
- `OccurredAt` is optional and defaults to now.

The parser therefore infers kind by exactly the rule the domain uses. `Transaction.Kind` derives
from the same two fields, so the client and the domain cannot disagree.

## Adapter 1 — the form

The baseline. Always present, works with no token and no network beyond the API.

Smart defaults, all from the user's own history — a query, not a model:

- Merchant predicts category. Albert Heijn has been Groceries the last forty times.
- Last-used account preselected, falling back to the account flagged `IsDefault`.
- Recent merchants offered as quick picks.
- One-tap repeat of any past transaction, prefilled with today's date.

These need one small API addition: recent transactions with their category, which
`ListTransactions` already returns. No new endpoint.

## Adapter 2 — quick-add grammar

Deterministic. No AI, no token, works offline, fully unit-testable. Highest priority of all the
adapters for exactly those reasons.

The grammar is a hybrid. Natural phrases cover the common path, while explicit tokens remove
ambiguity. These inputs are equivalent:

```
Spent 5 euros at Albert Heijn #shopping
5 EUR Albert Heijn #shopping @Cash
```

The first uses the default source account. The second names it. Both produce an expense draft for
5 EUR, merchant Albert Heijn, category Shopping and today's date. `#shopping` is a category, not a
tag. Tags use `~shopping` so categories and tags never compete for the same token.

| Token | Meaning | Resolution |
|---|---|---|
| `42.18`, `€42.18`, `42.18 usd` | amount and currency | minor units via the currency's exponent; currency via `CurrencyParser` |
| `#groceries` | category | matched against existing categories; see inline creation below |
| `@ing` | source account | prefix match on label or account number |
| `>savings` | destination account | same |
| `~work` | tag, repeatable | created if new |
| `today`, `yesterday`, `mon`–`sun`, `5 aug`, `2026-08-05` | date | `dayjs`, already a dependency |
| everything left over | merchant | created if new |

### Natural phrases and explicit tokens

Parsing is case-insensitive, whitespace-tolerant and independent of field order. Quotation marks
keep multi-word names together: `#"Eating out"`, `~"family trip"`, `@"Joint account"` and
`>"Rainy day"`.

| Concept | Natural forms | Explicit form |
|---|---|---|
| Expense | `spent`, `paid`, `bought`, `purchased`, `charged`, `withdrew` | source account without a destination |
| Income | `received`, `earned`, `deposited`, `was paid`, `refund` | destination account without a source |
| Transfer | `moved`, `transferred`, `sent … from … to …` | both source and destination accounts |
| Amount | `5`, `5.20`, `5,20` in comma-decimal locales | none |
| Currency | `euro`, `euros`, `EUR`, `€`, `dollar`, `dollars`, `USD`, `$`, or any supported ISO code | none |
| Merchant | `at Albert Heijn`, `from Employer`, or remaining text for a one-sided transaction | `merchant:"Albert Heijn"` |
| Category | `for shopping`, when Shopping is an exact known category | `#shopping` |
| Source account | `from Cash`, when Cash is an exact known account | `@Cash` |
| Destination account | `to Savings`, when Savings is an exact known account | `>Savings` |
| Tag | none | `~shopping`, repeatable |
| Date | `today`, `yesterday`, weekday, `last Friday`, `5 Aug`, `Aug 5`, ISO date | `date:2026-08-05` |
| Time | `this morning`, `this afternoon`, `this evening`, `14:30`, `2pm` | `time:14:30` |
| Transfer reason | remaining text after both accounts resolve | `reason:"rent buffer"` |

Natural `for <name>` becomes a category only when the complete name matches an existing category.
Otherwise it stays unresolved rather than stealing words from the merchant. Explicit tokens may
name an existing value or request inline creation where creation is allowed.

### Defaults

Defaults reduce typing but never invent analytical data:

- no kind verb defaults to expense unless the account tokens prove income or transfer;
- no currency uses the chosen account's currency;
- no account uses the active space's default account for an expense or income;
- no date uses now;
- merchant and category never default, except when the user's own merchant history supplies an
  exact, visible category suggestion that the user confirms;
- tags and transfer reason remain empty.

An explicit value always wins over a default. Two conflicting explicit values do not use
last-write-wins; both become a visible conflict that the user must resolve.

### Meaningful combinations

Word order does not create different combinations. The parser normalises every permutation into
the same draft, so the test matrix covers field combinations rather than every possible sentence
ordering.

| Kind | Minimum valid information after defaults | Optional information | Forbidden information |
|---|---|---|---|
| Expense | amount, source account, merchant, category | explicit currency, date/time, tags | destination account |
| Income | amount, destination account, merchant, category | explicit currency, date/time, tags | source account |
| Transfer | amount, source account, destination account | explicit currency, date/time, tags, reason | merchant, category |

Representative accepted combinations:

| Input | Result |
|---|---|
| `Spent 5 euros at Albert Heijn #shopping` | expense, default account, EUR, merchant, category, today |
| `5 Albert Heijn #shopping` | expense, default account and currency, today |
| `€5 at Albert Heijn #shopping` | expense with symbol currency |
| `5,20 EUR at Albert Heijn #shopping` | locale decimal amount |
| `paid Albert Heijn 5 EUR #shopping` | order-independent expense |
| `bought coffee for 4.50 #"Eating out"` | expense with multi-word category |
| `spent 18 @Cash at Cinema #Entertainment` | explicit source account |
| `spent 18 from Cash at Cinema #Entertainment` | natural source account |
| `spent 18 at Cinema #Entertainment yesterday` | relative date |
| `spent 18 at Cinema #Entertainment last Friday` | relative weekday |
| `spent 18 at Cinema #Entertainment 2026-08-05` | ISO date |
| `spent 18 at Cinema #Entertainment 14:30` | today at an explicit time |
| `spent 18 at Cinema #Entertainment ~family ~weekend` | multiple tags |
| `received 2400 EUR from Employer #Salary` | income to the default account |
| `earned 2400 from Employer #Salary >Checking` | explicit income destination |
| `refund 12 from Albert Heijn #Refunds >Cash` | refund represented as income |
| `deposited $100 from Client #Freelance yesterday` | income with currency and date |
| `moved 500 from Checking to Savings` | transfer with natural accounts |
| `transferred 500 @Checking >Savings` | transfer with explicit accounts |
| `500 @Checking >Savings` | kind inferred entirely from account tokens |
| `moved 500 EUR @Checking >Savings yesterday` | dated transfer |
| `moved 500 @Checking >Savings reason:"rent buffer"` | transfer reason |
| `moved 500 @Checking >Savings ~monthly ~saving` | tagged transfer |
| `moved 500 @"Joint checking" >"Rainy day"` | multi-word accounts |

The parser must also handle the cross-product of optional currency, account, date/time and tags for
expenses and income, and optional currency, date/time, tags and reason for transfers. Adding one
optional field cannot change any already-resolved field.

### Rejected and unresolved combinations

| Input pattern | Outcome |
|---|---|
| no amount | unresolved amount; cannot save |
| zero or negative amount | amount error; kind carries direction, not the sign |
| two unrelated amounts | conflict; the user chooses one |
| unsupported or conflicting currencies | currency conflict |
| unknown account | unresolved account; accounts are never created inline |
| ambiguous account prefix | unresolved list of matching accounts |
| expense or income without merchant | unresolved merchant |
| expense or income without category | unresolved category |
| transfer with merchant text | parse error |
| transfer with `#category` | parse error |
| transfer to the same account | validation error |
| transfer between different currencies | validation error; no conversion |
| impossible or ambiguous date | unresolved date |
| future date | validation error under the current form contract |
| duplicate category, account, date or kind tokens | conflict even when later tokens differ |
| text after all supported fields resolve | unresolved text; never silently discarded |

### Parse order and precedence

The implementation consumes tokens in this order:

1. quoted explicit tokens;
2. sigils and named explicit tokens;
3. amount and currency;
4. date and time;
5. kind verbs and natural account phrases;
6. natural category and merchant phrases;
7. transfer reason or unresolved remainder.

This order is an implementation detail with a user-visible guarantee: explicit syntax beats
natural inference, and inference beats defaults. Nothing silently overwrites an explicit value.

### Todoist-style parse feedback

The Quick Add control remains one editable sentence, but every recognised text range is decorated
as the user types:

- amount and currency;
- kind;
- merchant;
- category;
- source and destination account;
- each tag;
- date and time;
- transfer reason.

Decoration uses Fluent semantic colours, a subtle background and an icon or text label; colour is
never the only signal. The active token can open its matching picker. Deleting or editing text
immediately re-parses the whole sentence. Unresolved text gets a neutral dotted underline;
conflicts and invalid values use the error role. A screen-reader-only live region announces concise
changes such as `Category Shopping recognised` without reading the full sentence after every
keystroke.

Below the field, compact chips mirror the decorated ranges and provide a reliable interaction
surface on touch screens. Selecting a chip focuses its source range. Correcting through a picker
rewrites that range to an explicit token, preserving the rest of the sentence. The full traditional
form remains the confirmation surface and updates live from the same draft.

Kind falls out of the account tokens, matching the API validator:

```
42.18 albert heijn #groceries @ing        → expense
2400 employer #salary >ing yesterday      → income
500 @ing >savings rent buffer             → transfer, reason "rent buffer"
```

When both accounts are named, a `#category` or leftover merchant text is a **parse error**, not a
silent drop — the API would reject it and the user should learn the rule from the parser, not from
a 400.

### Unresolved tokens

Anything the parser cannot place becomes a visible chip in the confirmation step. The save button
is disabled while any chip is unresolved. This is the mechanism that makes a fast, fuzzy input
safe: speed in, certainty out.

### Inline category creation

Allowed. `#coffee` with no matching category creates one, with priority **Medium** (seeded id 3).

Because priority drives the essential-vs-discretionary analytic (Q5 in the IA spec), a guessed
priority would quietly skew a report. So creation is never silent: the confirmation step shows
`new category "coffee" · priority Medium` with the priority editable inline before saving. The
user chose speed here; the explicit chip is what keeps it from costing accuracy.

Merchants and tags need no such treatment — they carry no analytical weight.

## Adapter 3 — voice

The browser's Web Speech API transcribes speech to text, and the text feeds the **same grammar
parser**. No LLM, no token, no backend involvement.

Caveat: Web Speech support is good in Chrome and Safari and limited in Firefox. The button is
hidden when `SpeechRecognition` is absent rather than failing on click.

Spoken input will not contain `#` or `@`, so the parser needs spoken equivalents — "groceries
category", "from ING" — mapping onto the same tokens. Where the mapping is ambiguous, the tokens
land as unresolved chips, which is the correct outcome.

## Adapter 4 — LLM free-text parse

For input too loose for the grammar: "spent forty two euros at the supermarket this morning".

**Called through the backend only.** The browser posts text to Xpense's own API; the API calls the
provider with the user's stored token. The token never reaches the browser, and no endpoint ever
returns it. See the auth spec.

Output is a `TransactionDraft`, never a saved transaction. A parse the model got wrong is a chip
the user fixes, not a row in the database.

## Adapter 5 — receipt photo

A vision model extracts merchant, amount and date from an uploaded image. Same backend-proxied
path, same draft output.

Higher stakes than text: a misread total is worse than a misread word. So the extracted amount is
always shown beside the image at confirmation, and the amount field is focused by default.

## Adapter 6 — CSV import

The highest-coverage adapter, because it captures spending the user would never have logged.

1. Upload a bank CSV. Column mapping is chosen once per bank layout and remembered.
2. Rows become drafts. Category and merchant are predicted — from history first, from the LLM
   only for rows history cannot place.
3. **Duplicate detection** against existing transactions on amount, currency, date and account.
   Suspected duplicates are pre-deselected, not hidden.
4. A batch review table, using the `compact` density token, confirms or rejects per row.
5. One save per confirmed row through the existing endpoint.

## Confirmation rules

These apply to every adapter without exception:

- No save while any token is unresolved.
- Inline category creation is always visible with its priority named and editable.
- A transfer never carries a category or merchant; the UI removes those fields rather than
  letting the API reject them.
- Currency mismatch on a transfer is caught before the call. The domain throws, but the user
  should see it as a form error, not a failed request.
- Insufficient funds on a transfer is a domain error the UI surfaces on the amount field.

## Testing

The grammar parser is a pure function from string to draft. It gets a table-driven unit test suite
and is written test-first — every row in the grammar table above is a test case, plus the failure
cases: both accounts with a category, unparseable amount, unknown currency, ambiguous date.

Adapters that call a model are tested against recorded fixtures, never a live provider.

## API delta

Nothing new for the form, the grammar parser, or voice. They use `ListTransactions`,
`ListAccounts`, `ListCategories`, `ListMerchants`, `ListTags`, `CreateCategory` and
`CreateTransaction` as they exist.

New, all for the AI adapters:

1. `POST /api/v1/entry/parse` — text in, draft out. Calls the provider with the stored token.
2. `POST /api/v1/entry/receipt` — image in, draft out.
3. `POST /api/v1/entry/import/preview` — CSV in, drafts plus duplicate flags out.

None of them write. Every save still goes through `POST /api/v1/transactions`, so there is exactly
one path into the database.

## Phasing

Steps 1–3 need no token, no LLM and no provider account.

1. Form hardening and history-driven defaults.
2. Quick-add grammar parser and chip resolution.
3. Voice into the same parser.
4. LLM free-text parse.
5. Receipt photo.
6. CSV import and batch review.

## Open questions

- **Which provider, and which model.** Not decided. The backend proxy makes this swappable, so it
  need not be decided until step 4.
- **Recurring transactions.** The IA spec leaves Q8 open between heuristic inference and a
  `Recurrence` field on `Transaction`. If the field wins, marking something recurring becomes a
  grammar token and a form field, which belongs here. Deferred until Q8 is settled.
- **Spoken grammar vocabulary.** The mapping from spoken phrases to tokens needs real testing with
  real speech; the table above is a starting point, not a finished grammar.
