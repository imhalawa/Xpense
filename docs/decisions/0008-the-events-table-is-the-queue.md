---
status: accepted
date: 2026-08-06
---

# The events table is the queue

## Decision

Store events in PostgreSQL through the producer's unit of work. A separate worker claims pending rows and processes them. No message broker is required.

## Context

The system needs durable events, independent consumers, retries, and deduplication. The existing database can provide these with a shared transaction, row locking, and processing state. The original broker plan added operating and licensing costs for a small deployment.

## Alternatives considered

- **RabbitMQ with MassTransit:** rejected because the broker and framework were disproportionate to the deployment, and licensing and support affected the choice.
- **RabbitMQ with Rebus:** rejected because it still added a broker and dependency.
- **RabbitMQ with a custom outbox:** rejected because it added both a broker and substantial retry and delivery code.
- **LISTEN/NOTIFY:** deferred as a latency optimization; its signal is not durable and the table would still be needed.

## Consequences

The worker polls and processes batches. Failures retain attempts and errors; exhausted rows are marked processed with their error. Processed events remain available for inspection. There is no broker management UI. Emitting an event does not save it; the caller must commit.
