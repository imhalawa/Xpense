# Merchants

A merchant is who was on the other side of the money — the shop you paid, the employer who paid
you. It is the part of a transaction that sits outside Xpense.

## Fields

| Field | Notes |
| --- | --- |
| Label | Required, up to 100 characters. Trimmed before saving |

That is the whole record. A merchant carries no address, no category and no default; it exists so
that spending can be grouped and searched by who received it.

## When a merchant is required

| Kind | Merchant |
| --- | --- |
| Income | Exactly one — the payer |
| Expense | Exactly one — the payee |
| Transfer | None, and one is rejected |

A transfer moves money between two of your own accounts, so there is no outside party to name.

## Creating one

Two ways:

- The **+** beside *Merchants* in the sidebar, for planning ahead.
- Straight from the transaction form. Type a name that does not exist yet and it is created along
  with the transaction, in the same request. You never have to leave the form to define one first.

The same inline creation applies to tags. Internally both are handled as an *option*: an input that
either points at something that exists or creates it.

## Typeahead

The merchant field searches as you type — a case-insensitive match anywhere in the label, capped at
20 suggestions by default. With a large history you get the merchant you meant in a few keystrokes
instead of scrolling a list.

## Deleting a merchant

Deletion is soft, so past transactions keep their history. A merchant filter pointing at a deleted
merchant is dropped and reported rather than quietly returning nothing.
