# Helpdesk Platform — .NET

Helpdesk ticket management API built with C# .NET 10, EF Core, and PostgreSQL. Part of a multi-stack portfolio demonstrating architecture depth across .NET, NestJS, and Java.

**Stack:** C# .NET 10 · ASP.NET Core · EF Core 10 · PostgreSQL · Serilog · xUnit · TestContainers

---

## Architecture

Modular Monolith with Clean Architecture layers enforced per module:

```
src/
├── Helpdesk.API/                    ← entry point, controllers, middleware, AppDbContext
├── Helpdesk.Shared/                 ← Result<T>, base errors, base entities
└── Modules/
    ├── Tickets/                     ← ticket lifecycle state machine, comments, attachments
    │   ├── Domain/                  ← entities, value objects, domain events, interfaces
    │   ├── Application/             ← use cases (CreateTicket, Resolve, Transfer, etc.)
    │   └── Infrastructure/          ← EF configurations, repository implementations
    ├── Identity/                    ← auth, JWT, refresh tokens, roles, sessions
    ├── SLA/                         ← deadline calculation, team scoring, breach detection
    └── Notifications/               ← event contracts (Phase 1: structure only)
tests/
├── Helpdesk.Tests.Unit/             ← domain invariants, SLA logic, state machine
├── Helpdesk.Tests.Integration/      ← endpoints + auth (TestContainers + PostgreSQL)
└── Helpdesk.Tests.Architecture/     ← Domain must not depend on Infrastructure
```

### Key Design Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Architecture | Modular Monolith | Clean boundaries without distributed complexity |
| Error handling | `Result<T>` | No exceptions for expected business failures |
| Auth | JWT 15min + Refresh Token (Argon2id, 7d, rotation) | Security without statefulness |
| Events | Domain events dispatched post-persistence via `Channel<T>` | Decoupled async processing |
| Reads | `AsNoTracking()` + Query Services | Performance + explicit intent |
| Logging | Serilog structured JSON + `correlationId` per request | Production observability |
| Tests | 100% per endpoint — all happy and error paths | No endpoint without tests |

---

## Domain

**Ticket state machine:** `Open → In Progress → Resolved | Cancelled`

**Roles:** Customer · SupportAgent · Manager

**SLA deadlines:** Low 4h · Medium 2h · High 1h · Transfer +1h each

**Business rules:**
- Priority change: only while `In Progress`, by assignee only, max 3 times
- Cancellation: Customer (optional reason), Manager (mandatory reason)
- Resolution: requires description + assignee
- Auto-assign: Manager with lowest active load (tiebreaker: oldest account)
- Auto-cancel: after 10h without resolution

---

## Running

### Docker (recommended)

```bash
docker compose up
```

API available at `http://localhost:5000`. OpenAPI at `http://localhost:5000/openapi/v1.json`.

### Local

Requires PostgreSQL running locally:

```bash
# Apply migrations
dotnet ef database update --project src/Helpdesk.API

# Run API
dotnet run --project src/Helpdesk.API
```

---

## Testing

```bash
# All tests
dotnet test

# Unit tests only
dotnet test tests/Helpdesk.Tests.Unit

# Integration tests (requires Docker for TestContainers)
dotnet test tests/Helpdesk.Tests.Integration

# Architecture tests
dotnet test tests/Helpdesk.Tests.Architecture

# Single test by name
dotnet test --filter "FullyQualifiedName~Should_Not_Resolve_Without_Assignee"
```

---

## API Endpoints

REST conventions: resources + proper HTTP verbs. Tokens always in the request body — never in the URL path (prevents server log exposure).

### Auth (`/api/auth`)

| Method | Path | Description | Status codes |
|---|---|---|---|
| `POST` | `/api/auth/register` | Create a new Customer account | `201` · `400` |
| `POST` | `/api/auth/sessions` | Login — creates a session, sets HttpOnly refresh token cookie | `200` · `401` · `429` |
| `DELETE` | `/api/auth/sessions/current` | Logout — deletes current session, clears cookie | `204` |
| `PUT` | `/api/auth/sessions/current` | Token rotation — replaces current session with a new one | `200` · `401` |
| `PATCH` | `/api/auth/email-verifications` | Confirm email ownership (token in body) | `204` · `400` |
| `POST` | `/api/auth/password-resets` | Request a password reset email (always `204` — enumeration protection) | `204` · `429` |
| `PATCH` | `/api/auth/password-resets` | Apply password reset (token in body) | `204` · `400` |

**Response envelope:**
```json
// Success
{ "data": { ... }, "timestamp": "2025-01-01T00:00:00Z" }

// Error
{ "success": false, "error": { "code": "identity.invalid_credentials", "message": "..." }, "timestamp": "..." }
```

**Rate limits:**
- `POST /api/auth/sessions`: 5 requests / minute / IP
- `POST /api/auth/password-resets`: 3 requests / hour / IP

**Security:**
- Access token returned in response body only — client keeps in memory, never LocalStorage
- Refresh token in HttpOnly `Secure SameSite=Strict` cookie scoped to `/api/auth`
- Refresh token rotation: every `PUT /sessions/current` issues a new token and revokes the old one
- Token reuse detection: if a revoked refresh token is presented, the entire session family is immediately invalidated

---

## Non-Negotiable Rules

1. No Mediator / Command Bus — Use Cases injected directly via DI
2. No Generic Repository — `ITicketRepository`, not `IRepository<T>`
3. No Lazy Loading — explicit eager loading
4. Domain has zero infrastructure dependencies
5. State transitions enforced at Domain layer only
6. Authorization at Use Case layer, not only at Controller
7. No physical deletion of tickets, comments, or audit events
8. Refresh token reuse invalidates the entire session family
9. Access Token in memory only — never persisted client-side
10. No AutoMapper — explicit mapping via extension methods
