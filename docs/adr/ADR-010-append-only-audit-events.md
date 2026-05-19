# ADR-010: Append-Only Audit Events Table

**Status:** Accepted  
**Date:** 2025-05-01

## Context

Helpdesk systems require a tamper-evident history of all significant actions: ticket creation, assignments, status transitions, priority changes, comments, and authentication events. Mutable audit logs (where records can be updated or deleted) do not satisfy compliance or forensic requirements.

## Decision

Maintain an append-only `audit_events` table. Records are written by `AuditService` from domain events — never updated or deleted by application code. The table schema:

| Column | Type | Notes |
|---|---|---|
| `Id` | UUID | Primary key |
| `EventType` | string | e.g., `"TicketCreated"`, `"UserLoggedIn"` |
| `ActorId` | UUID | Who triggered the event |
| `EntityId` | UUID | The affected entity |
| `Payload` | JSONB | Event-specific data (may contain PII — see retention note) |
| `OccurredAt` | timestamp UTC | When the event occurred |

Database-level `UPDATE` and `DELETE` privileges on `audit_events` are revoked from the application user in production.

**Retention note:** The `Payload` field may contain PII (e.g., email addresses in `UserRegistered` events). LGPD/GDPR retention: rows must be purged after the applicable retention period. A scheduled cleanup job must be implemented before production go-live.

## Consequences

- The audit log is a reliable, chronological record of all domain events.
- No soft-delete or update columns are needed — the log is the source of truth for history.
- Payload JSONB is flexible: each event type can carry different fields without schema migrations.
- PII in payloads creates a retention obligation — addressed by the scheduled cleanup requirement above.
- Application code cannot corrupt the audit log; only append is permitted at the application layer.
