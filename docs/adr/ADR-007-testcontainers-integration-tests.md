# ADR-007: TestContainers for Integration Tests

**Status:** Accepted  
**Date:** 2025-05-01

## Context

Integration tests need a real database to be meaningful — mocked repositories can mask schema mismatches, migration issues, and query behavior differences. Using a shared development database creates test isolation problems (parallel runs, leftover state). SQLite in-memory is faster but does not match PostgreSQL behavior (different type system, different constraint enforcement).

## Decision

Use **TestContainers for .NET** to spin up an isolated PostgreSQL container per test fixture class. Each `IClassFixture<HelpdeskWebAppFactory>` gets its own container and database, automatically started before the test class and stopped after.

`HelpdeskWebAppFactory` overrides:
- `IEmailService` → `InMemoryEmailService` (captures raw tokens for verification/reset flows)
- `DbContext` connection string → TestContainers PostgreSQL instance
- `ASPNETCORE_ENVIRONMENT` → `"Test"` (disables rate limiting)

EF Core migrations run on container startup via `db.Database.MigrateAsync()`.

## Consequences

- Tests run against a real PostgreSQL 17 instance with the actual schema — no behavioral surprises in production.
- Each fixture class gets an isolated database — parallel test class execution is safe.
- Container startup adds ~5–10 seconds per fixture class (one-time cost per test run, amortized across all tests in the class).
- Requires Docker to be running on the CI machine — documented as a prerequisite.
- `InMemoryEmailService` captures raw tokens before hashing, making the full email verification and password reset flows testable end-to-end.
