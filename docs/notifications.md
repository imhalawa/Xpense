# Notifications

The bell in the sidebar shows what Xpense wants to tell you. Notifications are produced by a
separate worker, not by the request that triggered them.

## What raises one

Today there is exactly one kind of in-app notification.

### Budget exceeded

Raised when an expense pushes a budget past its limit. Two details matter:

- It fires on the **crossing only**. The transaction that takes you from under to over produces a
  notification; every later transaction while you stay over does not. You are told once, not
  nagged.
- It compares only spending in the **budget's own currency**. Spending in another currency is
  reported as *uncounted* on the budget itself rather than triggering an alert. See
  [Budgets](budgets.md).

The message reads like this:

> You have spent €157.50 of your €100.00 budget for Coffee in 2026-08, which is €57.50 over.

It carries the budget, category, period, limit, spent and over-by amounts, so the client can link
straight to what changed.

### Invitation email

Creating a group invitation sends an email, if SMTP is configured. It produces **no** in-app
notification — the recipient is not a user yet. With email disabled the delivery is marked
`Disabled` and the owner passes the link on by hand.

## What does not raise one

- **Alert thresholds.** A budget stores an alert threshold, defaulting to 75%, and reports against
  it, but no rule turns it into a notification yet. Only the over-limit crossing notifies.
- **Income, transfers, and budgets you are merely close to.**

## Reading them

| Action | Where |
| --- | --- |
| See recent notifications | The bell in the sidebar |
| Unread count | The badge on the bell |
| Mark one read | Select it |
| Mark everything read | The action in the notification list |

The list pages at 25 by default and can be filtered to unread only.

## How delivery works

There is no message broker. **The events table is the queue.** See
[ADR 0008](../api/docs/adr/0008-the-events-table-is-the-queue.md).

1. A slice that changes something emits an event through the same `DbContext` and does **not**
   save it separately. The event and the fact it describes commit in one transaction, so an event
   can never describe something that did not happen.
2. The notifications worker polls the table, waiting a second when idle and ten seconds after an
   error. A full batch skips the wait, so bursts drain quickly.
3. Each rule decides whether the event means anything to it.

Because the pump polls, a notification can arrive up to a second after the transaction. That is
deliberate: the write path stays fast and never waits on notification work.

**Deduplication** is by event and payload hash together, which is why a payload must contain
nothing time-varying — a timestamp inside it would defeat the hash and produce duplicates.

**Failures** are retried, with the attempt count and last error kept on the row. After the maximum
attempts the event is marked processed with the error left in place for inspection. There is no
dead-letter table.

The worker pauses entirely while a legacy claim is in progress, and it serves no HTTP, so it has no
health endpoint.

## Notifications and the encrypted vault

Once an installation is in encrypted mode the server cannot read budgets or amounts, so it cannot
decide that a budget was exceeded. In that mode budget alerts are computed by an unlocked client
and the resulting notification is encrypted and synced like any other record.

The practical consequence: in encrypted mode notifications appear when a client is open and
unlocked, not while everything is closed. There are no server-generated budget emails.
