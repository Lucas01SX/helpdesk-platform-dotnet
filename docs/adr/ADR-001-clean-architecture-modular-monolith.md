# ADR-001: Clean Architecture + Modular Monolith

**Status:** Accepted  
**Date:** 2025-05-01

## Context

The portfolio project requires a structure that demonstrates architectural discipline and boundary enforcement without introducing the operational complexity of microservices. The domain spans four bounded contexts: Tickets, Identity, SLA, and Notifications.

## Decision

Adopt **Modular Monolith with Clean Architecture** layers enforced per module. Each module (`Tickets`, `Identity`, `SLA`, `Notifications`) contains three explicit layers:

- `Domain/` — entities, value objects, domain events, repository interfaces; zero external dependencies
- `Application/` — use cases, contracts (requests/responses), application service interfaces
- `Infrastructure/` — EF Core configurations, repository implementations, external service adapters

The `Helpdesk.API` project acts as the composition root: it hosts controllers, middleware, and the `AppDbContext`.

Architecture tests (`Helpdesk.Tests.Architecture`) enforce that `Domain` never references `Infrastructure` at the assembly level.

## Consequences

- Module boundaries are enforced by project references and `ArchUnit`-style assertions, not by physical deployment.
- Adding a module requires creating a new project with the same three-layer structure — consistent and discoverable.
- Migrating to microservices later is possible by extracting a module; the boundary work is already done.
- All modules share a single database; cross-module queries happen via shared `DbContext`, not cross-service HTTP calls.
