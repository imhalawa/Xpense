# Accounts

An account represents a place money is held, such as a bank account or cash wallet. Each account uses one fixed currency.

## Create and edit

Manage accounts on **Settings**. The sidebar lists accounts for filtering.

| Field | Validation rules |
| --- | --- |
| Label | Required; up to 200 characters |
| Currency | Set by the opening balance; cannot be changed |
| Opening balance | Required; zero or more |
| Default | One account is selected by default for new transactions |

The first account becomes the default. Selecting another default clears the previous one. Create a separate account for each currency.

## Balance

The balance starts with the opening balance. Income adds to it; expenses subtract from it. A transfer subtracts from the source account and adds to the destination. The overview groups balances by currency.

## Rules

- Amounts must match the account's currency.
- A transfer's accounts must use the same currency.
- A transfer cannot exceed the source balance.
- The API identifies an account by its generated account number, starting at `1000000000`.
- Deletion is soft, preserving transaction references.
