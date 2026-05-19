# ADR-005: Domain Events via Channel&lt;T&gt; + BackgroundService

**Status:** Accepted  
**Date:** 2025-05-01

## Context

Domain events (e.g., `TicketCreated`, `PriorityChanged`, `SlaBreached`) need to trigger side effects — SLA deadline calculation, audit logging, notification dispatch — without coupling the aggregate to infrastructure. Publishing events synchronously inside the domain transaction risks cascading failures; using an external message broker adds operational complexity inappropriate for a portfolio project.

## Decision

- Aggregates **raise events** internally (stored in a `DomainEvents` list on the entity).
- After EF Core `SaveChangesAsync`, the persistence layer **dispatches** raised events into a `Channel<IDomainEvent>` (in-process, bounded, async).
- A `BackgroundService` reads from the channel and invokes registered `IDomainEventHandler<T>` implementations (SLA handler, audit handler, notification handler).

Events are not persisted to an outbox — if the process crashes between `SaveChanges` and channel dispatch, events are lost. This is an accepted tradeoff for a portfolio project; production would require an outbox pattern.

## Consequences

- Aggregates have zero knowledge of handlers — they only raise events.
- Handlers run asynchronously outside the request pipeline — a slow handler does not affect the HTTP response time.
- The `Channel<T>` is in-process: no broker dependency, no serialization overhead for same-process consumers.
- Event loss on crash is a known limitation, documented here. Adding an outbox table is the migration path if this moves to production.
