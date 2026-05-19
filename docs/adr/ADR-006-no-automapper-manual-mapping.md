# ADR-006: No AutoMapper — Explicit Manual Mapping

**Status:** Accepted  
**Date:** 2025-05-01

## Context

AutoMapper and similar libraries map between objects using reflection and convention-based rules. They reduce boilerplate but introduce implicit contracts: a property renamed in the domain silently stops mapping without a compile error, and the mapping configuration becomes a hidden dependency that must be maintained in sync with both sides.

## Decision

All mappings between domain entities and DTOs (request/response records) are written as **explicit extension methods** in `*Extensions` classes:

```csharp
public static class TicketMappingExtensions
{
    public static TicketDetailsResponse ToResponse(this Ticket ticket) => new(
        Id: ticket.Id,
        Status: ticket.Status.ToString(),
        Priority: ticket.Priority.ToString(),
        ...
    );
}
```

## Consequences

- Renaming a domain property causes a compile error in the mapping — the contract is explicit and type-safe.
- Mappings are readable without a separate configuration file — the transformation is visible at the call site.
- No runtime reflection cost.
- Boilerplate increases linearly with the number of mappings — acceptable for a domain with a bounded number of aggregates.
- New developers can navigate from the controller to the mapping to the domain entity without consulting a separate mapping profile.
