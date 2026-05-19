# ADR-003: Result&lt;T&gt; Instead of Exceptions for Business Failures

**Status:** Accepted  
**Date:** 2025-05-01

## Context

Expected business failures (ticket not found, invalid state transition, forbidden action) are control flow, not exceptional conditions. Throwing exceptions for these cases misuses the exception mechanism, pollutes stack traces, and forces controllers to use try/catch blocks for normal application logic.

## Decision

Use a custom **`Result<T>`** type in `Helpdesk.Shared.Results` for all use case return values. `Result<T>` carries either a success value or an `Error` (code + message), never both.

```csharp
// Use case returns Result<Guid>, not Guid; never throws for business rules
public async Task<Result<Guid>> ExecuteAsync(CreateTicketRequest request, CancellationToken ct)
{
    if (customer is null) return TicketAppErrors.CustomerNotFound; // implicit conversion
    // ...
    return ticket.Id; // implicit conversion from Guid
}

// Controller maps Result to HTTP response
var result = await createTicket.ExecuteAsync(request, ct);
return result.IsSuccess ? StatusCode(201, ...) : BadRequest(...);
```

Domain errors are centralized in static `*AppErrors` classes per module.

## Consequences

- Controllers contain no try/catch for business logic — only `result.IsSuccess` checks.
- Exceptions are reserved for unexpected infrastructure failures (DB connection lost, disk full).
- The `GlobalExceptionHandlerMiddleware` catches unhandled exceptions and returns 500 — the Result pattern keeps the 4xx path clean.
- All error codes are strings (`"ticket.not_found"`, `"identity.invalid_credentials"`) — consistent with the response envelope and easy to document.
