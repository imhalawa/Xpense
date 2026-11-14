# Money and currencies

Xpense records amounts in one currency and does not convert between currencies. The supported currencies are EUR and USD.

## Rules

- An account's opening balance sets its currency for its lifetime.
- Income and expense amounts must match the account currency.
- A transfer's amount and both accounts must use the same currency.
- Amounts in different currencies cannot be compared or combined.
- Reports show separate totals for each currency. A budget reports spending in other currencies as uncounted.

To track two currencies, create separate accounts and budgets for each.

## Minor units

Amounts are integers in minor units. One euro is 100 minor units. The current API response shape is:

```json
{ "minorUnits": 1250, "currency": "EUR" }
```

The repository instruction in `AGENTS.md` says `cents`; the implementation uses `minorUnits`. Resolve this discrepancy before changing the contract.
