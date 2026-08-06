---
status: accepted
date: 2026-08-06
---

# The Events table is the queue

There is no message broker. `IEventBus.Emit` inserts a row into `Xpense.Events` through the caller's
`XpenseDbContext`, and the `notifications` worker claims outstanding rows with
`SELECT ... FOR UPDATE SKIP LOCKED` on a one-second tick. Producers and consumers are still separate
processes in separate containers; the thing between them is a table.

## Why

The plan was RabbitMQ with MassTransit, and MassTransit was chosen specifically for its Entity
Framework outbox — hand-rolling an outbox is the classic thing that ends up subtly wrong in the
ordering of commit, insert and retry.

Then the licence turned out to matter. MassTransit v9 [became
commercial](https://massient.com/license) in Q1 2026, at a minimum of $400 per month with a full
discount for organizations under $1M revenue, and v8 remains Apache 2.0 but loses support at the end
of 2026. So the choice was an unsupported version, or a licensed one, to get a framework that is
large for a project with one event type and one consumer.

That prompted the more useful question: what was the broker for? The requirements were that a
producer must not wait on a consumer, that nothing may be lost if the consumer is down, and that
redelivery must not duplicate work. Postgres does all three, and Xpense already runs exactly one
Postgres:

- **Durability** is inherent rather than bolted on. The event row is written in the same transaction
  as the thing it describes, so the fact and the record of it commit together. There is no window
  where Xpense has announced something it did not record, and none where it recorded something it
  will never announce — which is precisely what an outbox exists to achieve, except here the outbox
  *is* the queue rather than a staging area in front of one.
- **Claiming** is `FOR UPDATE SKIP LOCKED`, which is what the broker's competing-consumer behaviour
  amounts to. A second worker takes the next unlocked rows instead of blocking.
- **Retries** are a column. A failed event keeps its `Attempts` and `LastError` and is picked up
  again; after `EventRecord.MaxAttempts` it is marked processed with the error left in place, so one
  poisonous row cannot block the queue and the row itself is the dead letter.

The decisive argument is what is *not* here: no fourth container to run and back up, no second thing
that can be down, no library whose conventions decide queue names, and no licence to track. For a
system on one machine, RabbitMQ's real advantages — cross-language consumers, fan-out to many
independent subscribers, throughput far beyond one Postgres — are all things Xpense does not do.

## Considered options

**RabbitMQ with MassTransit.** The original plan, and the right answer at a scale with several
services and several teams. Rejected on the licensing above, and on being heavy for one event type.

**RabbitMQ with Rebus.** Free, much smaller than MassTransit, real queues, and a management UI worth
having. Rejected because it still adds a broker container and a library to get behaviour a table
already provides here; its outbox support is also less mature, which was the one thing worth taking a
dependency for.

**RabbitMQ with the raw client and a hand-rolled outbox.** No framework and nothing to relicense.
Rejected as the most code of any option — publisher, consumer, acks, retry, dead-lettering and the
outbox drain — where the outbox drain is the part that is easy to get wrong. Choosing this would mean
writing the risky component *and* operating a broker.

**LISTEN/NOTIFY instead of polling.** Would cut the latency from about a second to nearly nothing.
Rejected for now because a notification is not durable — a worker that is down misses the signal and
would still need the table as the source of truth, so it is an optimization on top of this design
rather than an alternative to it.

## Consequences

Notifications arrive up to about a second after the transaction, rather than immediately. Nothing in
this system cares.

There is no management UI. Diagnosing a stuck event is `select * from "Xpense"."Events" where
"ProcessedAt" is null` — arguably better than RabbitMQ's UI for this purpose, since the payload and
the error are right there, but it is a psql prompt rather than a web page.

The queue's throughput is one Postgres transaction per batch. That is orders of magnitude beyond what
a personal ledger produces, and the point at which it stops being true is the point at which a broker
becomes the right answer.

Processed events are kept, with `ProcessedAt` set, so what was published stays auditable and can be
replayed by hand. Nothing prunes them; the outstanding-events index is filtered on
`ProcessedAt IS NULL` so their accumulation does not slow the claim query down.

`Emit` deliberately does not call `SaveChanges`. A caller that never saves has emitted nothing, which
is the correct behaviour and also means the method is useless outside a unit of work — that is a
constraint on callers, stated here so it does not read as an oversight.
