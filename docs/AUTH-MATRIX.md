# Authorization Matrix

Authorization is enforced at the Use Case layer (not at the controller). This document provides a single-view audit of who can call what.

Role values from the JWT payload: `Customer`, `Agent`, `Manager`.

---

## Identity

| Endpoint | Customer | Agent | Manager | Notes |
|---|---|---|---|---|
| `POST /api/auth/register` | ✅ | ✅ | ✅ | Public |
| `PATCH /api/auth/email-verifications` | ✅ | ✅ | ✅ | Public |
| `POST /api/auth/sessions` | ✅ | ✅ | ✅ | Public — rate limited (5/min/IP) |
| `PUT /api/auth/sessions/current` | ✅ | ✅ | ✅ | Requires valid refresh token cookie |
| `DELETE /api/auth/sessions/current` | ✅ | ✅ | ✅ | Idempotent — no error if no session |
| `POST /api/auth/password-resets` | ✅ | ✅ | ✅ | Public — rate limited (3/hour/IP) |
| `PATCH /api/auth/password-resets` | ✅ | ✅ | ✅ | Public |

---

## Tickets

| Endpoint | Customer | Agent | Manager | Notes |
|---|---|---|---|---|
| `POST /api/tickets` | ✅ own | ❌ | ❌ | Only customers can create |
| `GET /api/tickets` | ✅ own | ✅ all | ✅ all | Customers see only their tickets; paginated (page, limit) |
| `GET /api/tickets/:id` | ✅ own | ✅ | ✅ | Customers restricted to own ticket |
| `POST /api/tickets/:id/assign` | ❌ | ❌ | ✅ | Manager only |
| `POST /api/tickets/:id/transfer` | ❌ | ✅ current-assignee | ✅ current-assignee | Must be currently assigned |
| `POST /api/tickets/:id/resolve` | ❌ | ✅ current-assignee | ✅ current-assignee | Non-empty resolutionDescription required |
| `POST /api/tickets/:id/cancel` | ✅ own | ❌ | ✅ | Manager must provide reason |
| `PATCH /api/tickets/:id/priority` | ❌ | ✅ current-assignee | ✅ current-assignee | Max 3 changes; only while InProgress |

---

## Comments

| Endpoint | Customer | Agent | Manager | Notes |
|---|---|---|---|---|
| `POST /api/tickets/:id/comments` | ✅ own | ✅ | ✅ | Internal visibility restricted to Agent/Manager |
| `GET /api/tickets/:id/comments` | ✅ own (Public only) | ✅ all | ✅ all | Customers cannot see Internal comments |

---

## Attachments

| Endpoint | Customer | Agent | Manager | Notes |
|---|---|---|---|---|
| `POST /api/tickets/:id/attachments` | ✅ own | ✅ | ✅ | Rate limited (10/hour/IP) |
| `GET /api/tickets/:id/attachments` | ✅ own (Public only) | ✅ all | ✅ all | Customers cannot see Internal attachments |
| `GET /api/tickets/:id/attachments/:attachmentId` | ✅ own (Public only) | ✅ all | ✅ all | Same visibility rules as list |

---

## Ticket History

| Endpoint | Customer | Agent | Manager | Notes |
|---|---|---|---|---|
| `GET /api/tickets/:id/history` | ✅ own | ✅ | ✅ | Full audit trail |

---

## SLA

| Endpoint | Customer | Agent | Manager | Notes |
|---|---|---|---|---|
| `GET /api/sla/scores` | ❌ | ✅ | ✅ | Agent and Manager only |

---

## Observability

| Endpoint | Auth required | Notes |
|---|---|---|
| `GET /health` | ❌ | Public — used by Kubernetes readiness probe; returns 503 if DB unreachable |
| `GET /metrics` | ❌ | Prometheus scrape endpoint — restrict at network level in production |
