# Domain language and glossary

Xpense is a personal-finance ledger: it records money moved elsewhere and reports on it. It does not hold or transfer money.

| Term | Meaning |
| --- | --- |
| Money | An integer number of minor units in one currency. |
| Currency | The denomination of money and an account; amounts in different currencies are not converted or added together. |
| Account | A named store of money in one fixed currency. Its account number is its public identifier. |
| Default account | The account offered first when entering a transaction. |
| Balance | The money currently recorded in an account. |
| Transaction | One recorded movement of money, classified by its source and destination accounts. |
| Income | A transaction from outside Xpense into an account. |
| Expense | A transaction from an account to a party outside Xpense. |
| Transfer | A transaction between two accounts, in the same currency. |
| Occurred at | When money moved; distinct from when the record was created. |
| Merchant | The outside party in an income or expense transaction. |
| Category | A classification for income or expense; its priority expresses necessity. |
| Tag | An optional label attached to a transaction. |
| Budget | A spending limit for a category, currency and period. It reports and does not block transactions. |
| Spent | Expenses counted against a budget in its period and currency. |
| Remaining | A budget's limit less its spending; it is not an account balance. |
| User | A person with an Xpense identity. |
| Group | A group of users with an owner and members. |
| Grant | Permission given to a group for a particular resource and role. |
| Invitation | A time-limited invitation to join a group. |
| Event | A fact recorded when something happens. |
| Notification | Information selected for a recipient by a rule or derived by the client. |

See [money](money.md), [feature guides](../README.md#features), and [architecture](../architecture/overview.md).
