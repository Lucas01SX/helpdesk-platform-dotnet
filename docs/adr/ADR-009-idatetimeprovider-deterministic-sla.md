# ADR-009: IDateTimeProvider Abstraction for Deterministic SLA Tests

**Status:** Accepted  
**Date:** 2025-05-01

## Context

SLA deadline calculation and breach detection depend on `DateTime.UtcNow`. Calling `DateTime.UtcNow` directly in domain or application code makes tests non-deterministic: test results vary based on when the test runs. Mocking `DateTime` requires either invasive static wrappers or time-travel hacks.

## Decision

Introduce `IDateTimeProvider` in `Helpdesk.Shared.Abstractions`:

```csharp
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
```

Production implementation (`SystemDateTimeProvider`) returns `DateTime.UtcNow`. Test implementation (`FakeDateTimeProvider`) accepts a controlled value.

All SLA deadline calculations, ticket status timestamps, and audit timestamps obtain the current time via `IDateTimeProvider` — never from `DateTime.UtcNow` directly.

## Consequences

- SLA tests can set an exact "current time" and assert deadline values deterministically.
- Breach detection tests can simulate time passage without `Thread.Sleep`.
- The abstraction adds a single interface with a single property — negligible overhead.
- `IDateTimeProvider` must be injected wherever timestamps are needed; `DateTime.UtcNow` in domain or application code is a violation caught in code review.
