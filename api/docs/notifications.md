# Notifications

Something happens, Xpense states it as an **event**, and a separate worker decides whether anyone
should be told. Those are two different jobs on purpose: the part that records a transaction has no
idea what is worth saying, and nothing it does depends on the answer.

```
POST /api/v1/transactions
  └─ writes the Transaction and a TransactionRecorded event, one transaction

Xpense.Notifications  (its own container)
  └─ claims the event, asks every rule, stores what they decided

GET /api/v1/notifications
  └─ the API serves them
```

There is no message broker. The `Xpense.Events` table *is* the queue — see
[ADR 0008](adr/0008-the-events-table-is-the-queue.md) for why, including why MassTransit and RabbitMQ
were planned and then dropped.

## Event, or notification?

They are not the same thing and the words are not interchangeable.

| | **Event** | **Notification** |
|---|---|---|
| What it is | A fact that something happened | A judgement that somebody should be told |
| Who states it | Whichever part of Xpense it happened in | A rule, afterwards |
| When | Always, whether or not anyone cares | Only when a rule says so |
| Changes? | Never. A correction is a new event | Only its read state |

Most events produce no notification at all.

## Emitting one

```csharp
public sealed class CreateSomething(XpenseDbContext dbContext, IEventBus events)
{
    await events.Emit(Event.Of(new SomethingHappened(id, amount)), cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);   // the event becomes durable here, not before
}
```

`Emit` inserts through the caller's `DbContext` and **does not save**. The event and the thing it
describes therefore commit together: there is no moment where Xpense has announced something it did not
record, and none where it recorded something it will never announce. A caller that never saves has
emitted nothing.

Bodies derive from `EventBody` and live in `Xpense.Domain/Events/`. They hold **primitives, enums and
ids only** — no entity, no `Money`. A body is a wire format that may be read back long after it was
written, so a property typed as an entity ties unprocessed events to today's schema. `EventContractTests`
enforces this.

## Writing a rule

One notification kind, one file, and no knowledge of any other rule. Rules are found by scanning the
assembly, so adding a file is the whole registration step.

```csharp
public sealed class SomethingWorthSayingRule(XpenseDbContext dbContext)
    : INotificationRule<TransactionRecorded>
{
    public async Task<IReadOnlyList<NotificationDraft>> Evaluate(
        Event<TransactionRecorded> @event, CancellationToken cancellationToken)
    {
        if (nothing worth saying) return [];

        return [new NotificationDraft(
            NotificationKind.SomethingHappened,
            "One line, enough on its own",
            "The detail behind it, still a sentence.",
            new SomethingPayload(...))];
    }

    private sealed record SomethingPayload(int Id, long AmountMinorUnits, string Currency);
}
```

Three rules that constrain how you write one:

**A rule owns its queries.** Two rules wanting the same data query it twice, and that is the intended
state — the same trade AGENTS.md makes for slices. `NotificationRuleIsolationTests` fails the build if
one rule references another, so a shared helper is not an option.

**A payload must contain nothing time-varying.** It is hashed, and that hash is half of what makes a
notification unique. Put a "generated at" inside one and every redelivery produces a fresh hash, so
duplicates stop being caught.

**Fire on the crossing, not the state.** The event carries the amount, so a rule can compute the total
before and after it and act only on the transition. That is what makes "you went over" say itself once
rather than on every subsequent purchase.

## What stops duplicates

`(EventId, PayloadHash)` is unique. One event may warrant several notifications — an expense crossing
both a weekly and a monthly budget is two, with different payloads — so the event id alone cannot be the
key, and hashing the facts avoids inventing a naming convention for what each notification is "about".

The processor also checks before inserting, but the index is the guarantee: it holds against a replay, a
bug, or a second worker, none of which an in-process check would catch.

## When something goes wrong

A failing event keeps its `Attempts` and `LastError` and is retried. After `EventRecord.MaxAttempts` it
is marked processed with the error left in place, so one poisonous row cannot block the queue. There is
no dead-letter table — such a row is recognisable by having both a `ProcessedAt` and a `LastError`.

```bash
# outstanding
docker compose exec -T postgres psql -U xpense -d xpense \
  -c 'select "EventId", "Type", "Attempts", "LastError" from "Xpense"."Events" where "ProcessedAt" is null;'

# gave up
docker compose exec -T postgres psql -U xpense -d xpense \
  -c 'select "EventId", "Type", "Attempts", "LastError" from "Xpense"."Events" where "LastError" is not null;'

# replay one, by hand
docker compose exec -T postgres psql -U xpense -d xpense \
  -c 'update "Xpense"."Events" set "ProcessedAt" = null, "Attempts" = 0 where "EventId" = '"'"'...'"'"';'
```

Replaying is safe: the unique index means anything already said is not said again.

## Things that will bite

**`libgssapi_krb5.so.2` in the worker log is not an error.** Npgsql probes for Kerberos on its first
connection and does not find it on Alpine. Same line the API logs; see `docs/docker.md`.

**Notifications arrive up to a second late.** The pump polls. A full batch skips the wait, so a burst
drains at speed; an idle queue costs one indexed query per second.

**The worker has no healthcheck.** It serves no HTTP. Whether it is working shows in its log and in
whether `Events` drains — adding an endpoint purely to be probed would mean carrying ASP.NET Core in a
worker with no other use for it.

**Timestamps returned to a client are microsecond-precision.** Postgres `timestamptz` cannot hold .NET's
100-nanosecond ticks, so `Notification.MarkAsRead` truncates deliberately. Without that, the response to
the request that marked something read would be more precise than every later read of the same row, and
a client comparing them would see a value change on a call that changed nothing.
