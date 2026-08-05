# Multi-currency

> The rules below still hold. The code and payload examples do not: they predate the model rename
> pass, so they show `MoneyTransfer`, `BalanceCents`, `Money.OfCents`, `POST /api/v1/transfers` and
> a `cents` wire field. Current names are `Transaction.Transfer`, `BalanceMinorUnits`,
> `Money.OfMinorUnits`, `POST /api/v1/transactions` and `minorUnits`. See
> [model-rename-pass.md](model-rename-pass.md).

Xpense holds money in several currencies. It does **not** convert between them.

That distinction is the whole design. Every currency-mixing operation is rejected with a 400
rather than guessed at, because guessing means moving the wrong quantity of money.

## The rules

| Operation | Rule |
|---|---|
| Create an account | The opening balance carries a currency; that currency denominates the account for life |
| Income / expense | The amount's currency must equal the account's, else 400 |
| Transfer | Both accounts **and** the amount must share one currency, else 400 |
| Compare two `Money` values | Throws if the currencies differ — a comparison across currencies has no meaning |

There is no rate table, no provider and no rounding policy, because there is no conversion.

## What it replaced

`Account.Balance` was a bare `decimal` with no currency at all. `MoneyTransfer` called
`amount.ToDecimal()` and applied the number to the balance, so:

```
POST /api/v1/transfers  {"cents": 10000, "currency": "USD"}   between two EUR accounts
  -> 201, moved 100.00 EUR, recorded "USD" on the row
```

Three tests asserted that behaviour, which is how it survived. They now seed accounts whose
currency matches the amount, and separate tests assert the rejection.

## How it is enforced

Balance moved from `decimal` to `long BalanceCents` plus a `Currency`, so it shares a
representation with `Money`:

```csharp
public long BalanceCents { get; set; }
public Currency Currency { get; set; }
public Money Balance => Money.OfCents(BalanceCents, Currency);   // not mapped

public void Deposit(Money amount)
{
    RequireMatchingCurrency(amount);
    BalanceCents += amount.Cents;
    Touch();
}
```

`Deposit` and `Withdraw` take `Money`, not `decimal`. A currency mismatch is therefore
impossible to express at the call site rather than merely discouraged — the entity itself
refuses. `MoneyTransfer` adds the account-to-account check on top.

Minor units also remove the decimal-rounding question entirely: cents are integers.

## The wire contract

Money crosses the boundary as `{cents, currency}`, per
[`contract/api-v1-contract-design.md`](contract/api-v1-contract-design.md). Account balances
used to be a bare decimal, which is now fixed:

```jsonc
// before
{ "balance": 100.50 }

// now
{ "balance": { "cents": 10050, "currency": "EUR" } }
```

Creating an account takes the same shape:

```json
POST /api/v1/accounts
{ "name": "Euro", "balance": { "cents": 10000, "currency": "EUR" } }
```

`Contracts/MoneyResponse` is shared across accounts, transactions, transfers and analytics.
Four private copies of that record existed before this change.

## Adding FX later

If conversion is ever wanted, it belongs **before** the domain, never inside it. `Account`
should keep refusing a mismatched amount; a converting layer would turn 100 EUR into the
destination's currency and hand the domain an amount that already matches. The transfer would
then need to record the rate used and the original amount for audit.

Open product questions that conversion would raise, none of which apply today: which rate
provider, spot versus historical-at-`occurredAt`, rounding policy, and behaviour when the
provider is unreachable.
