# ADR-002: No Mediator — Direct Use Case Injection

**Status:** Accepted  
**Date:** 2025-05-01

## Context

Many .NET projects use MediatR or a similar command bus to decouple controllers from application logic. This introduces indirection: a `Send(command)` call that requires following a registration chain to understand which handler runs.

## Decision

Inject **Use Cases directly into controllers via constructor DI**. Each use case is a single-responsibility class (`CreateTicketUseCase`, `ResolveTicketUseCase`, etc.) registered as `Scoped` in the DI container.

```csharp
// Controller receives concrete use cases
public TicketsController(
    CreateTicketUseCase createTicket,
    ResolveTicketUseCase resolveTicket,
    ...) { }
```

## Consequences

- Navigation is direct: controller calls use case, use case calls repository. No handler registry to consult.
- Cross-cutting concerns (logging, validation) are handled explicitly in middleware or in the use case itself — not hidden in pipeline behaviors.
- The dependency graph is explicit and verifiable at compile time.
- Adding a new use case requires adding a constructor parameter — deliberate friction that discourages controller bloat.
- No behavioral polymorphism (e.g., `IRequest<T>`) means less abstraction overhead for a portfolio codebase.
