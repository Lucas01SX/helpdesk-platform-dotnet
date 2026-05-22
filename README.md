# Helpdesk Platform — .NET

Helpdesk ticket management API built with C# .NET 10, EF Core, and PostgreSQL. Part of a multi-stack portfolio demonstrating architecture depth across .NET, NestJS, and Java.

**Stack:** C# .NET 10 · ASP.NET Core · EF Core 10 · PostgreSQL 17 · Argon2id · Serilog · xUnit · TestContainers

---

## Architecture

Modular Monolith with Clean Architecture layers enforced per module:

```
src/
├── Helpdesk.API/                    ← entry point, controllers, middleware, AppDbContext
├── Helpdesk.Shared/                 ← Result<T>, base errors, base entities, RoleNames
└── Modules/
    ├── Tickets/                     ← ticket lifecycle state machine, comments, attachments
    │   ├── Domain/                  ← entities, value objects, domain events, interfaces
    │   ├── Application/             ← use cases (CreateTicket, Resolve, Transfer, etc.)
    │   └── Infrastructure/          ← EF configurations, repository implementations
    ├── Identity/                    ← auth, JWT, refresh tokens, roles, sessions
    ├── SLA/                         ← deadline calculation, team scoring, breach detection
    └── Notifications/               ← event contracts
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

See [`docs/adr/`](docs/adr/) for all 10 Architecture Decision Records.

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
# 1. Create .env file with secrets (see docs/ENV.md)
cp .env.example .env   # edit values before running

# 2. Start API + PostgreSQL
docker compose up
```

API available at `http://localhost:5000`.  
Interactive API docs (Scalar UI): only available when `ASPNETCORE_ENVIRONMENT=Development`.

### Local

Requires PostgreSQL running locally. Create `src/Helpdesk.API/appsettings.Development.json` with your connection string (see [`docs/ENV.md`](docs/ENV.md)).

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

# By suite
dotnet test tests/Helpdesk.Tests.Unit
dotnet test tests/Helpdesk.Tests.Integration    # requires Docker
dotnet test tests/Helpdesk.Tests.Architecture

# Single test
dotnet test --filter "FullyQualifiedName~Should_Not_Resolve_Without_Assignee"
```

**Counts:** 59 unit · 6 architecture · 128 integration — 193 total, all green.

Integration tests use TestContainers — Docker must be running. Each fixture class gets an isolated PostgreSQL container.

---

## API Endpoints

Response envelope:
```json
{ "data": { ... }, "correlationId": "uuid", "timestamp": "2025-01-01T00:00:00Z" }
```

All tokens go in the request body or cookie — never in the URL path.

### Auth (`/api/auth`)

| Method | Path | Description | Roles | Status codes |
|---|---|---|---|---|
| `POST` | `/api/auth/register` | Create a new Customer account | — | `201` · `400` · `409` |
| `POST` | `/api/auth/sessions` | Login — sets HttpOnly refresh token cookie | — | `200` · `401` · `429` |
| `DELETE` | `/api/auth/sessions/current` | Logout — revokes session, clears cookie | Any | `204` |
| `PUT` | `/api/auth/sessions/current` | Token rotation — replaces session with a new one | Any | `200` · `401` |
| `PATCH` | `/api/auth/email-verifications` | Confirm email ownership | — | `204` · `400` |
| `POST` | `/api/auth/password-resets` | Request a password reset email | — | `204` · `429` |
| `PATCH` | `/api/auth/password-resets` | Apply password reset | — | `204` · `400` |

**Rate limits:** Sessions: 5/min/IP · Password resets: 3/hour/IP

### Tickets (`/api/tickets`)

| Method | Path | Description | Roles | Status codes |
|---|---|---|---|---|
| `POST` | `/api/tickets` | Open a new ticket | Customer | `201` · `400` |
| `GET` | `/api/tickets` | List tickets (scoped by role) | Any | `200` |
| `GET` | `/api/tickets/{id}` | Get ticket details | Any | `200` · `404` |
| `POST` | `/api/tickets/{id}/assign` | Assign ticket to self | Agent · Manager | `204` · `404` · `409` |
| `POST` | `/api/tickets/{id}/resolve` | Resolve ticket | Agent · Manager | `204` · `403` · `404` · `409` |
| `POST` | `/api/tickets/{id}/cancel` | Cancel ticket | Customer · Manager | `204` · `403` · `404` · `409` |
| `POST` | `/api/tickets/{id}/transfer` | Transfer to another agent | Agent · Manager | `204` · `403` · `404` · `409` |
| `PATCH` | `/api/tickets/{id}/priority` | Change ticket priority | Agent · Manager | `204` · `403` · `404` · `409` |
| `POST` | `/api/tickets/{id}/comments` | Add a comment | Any | `201` · `400` · `403` · `404` |
| `GET` | `/api/tickets/{id}/comments` | List comments (filtered by visibility) | Any | `200` · `404` |
| `POST` | `/api/tickets/{id}/attachments` | Upload attachment | Any | `201` · `400` · `403` · `404` |
| `GET` | `/api/tickets/{id}/attachments` | List attachments (filtered by visibility) | Any | `200` · `404` |
| `GET` | `/api/tickets/{id}/attachments/{aid}` | Download attachment file | Any | `200` · `403` · `404` |

---

## Security

- Access token: JWT, 15-minute expiry — client stores in memory only (never LocalStorage)
- Refresh token: Argon2id-hashed, `HttpOnly; Secure; SameSite=Strict` cookie scoped to `/api/auth`
- Refresh token reuse detection: presenting a revoked token invalidates the entire session family
- Session limit: max 5 active sessions per user
- IDOR protection: Customer actors can only access their own tickets and attachments
- Internal comments/attachments are hidden from Customer role
- ForwardedHeaders middleware applied first — `X-Forwarded-For` and `X-Forwarded-Proto` honored behind reverse proxy
- Correlation ID sanitization: client-supplied `X-Correlation-Id` values are rejected if they exceed 64 chars or contain characters outside `[a-zA-Z0-9\-_]`

---

## Architecture Decision Records

| ADR | Decision |
|---|---|
| [ADR-001](docs/adr/ADR-001-clean-architecture-modular-monolith.md) | Clean Architecture + Modular Monolith |
| [ADR-002](docs/adr/ADR-002-no-mediator-direct-use-case-injection.md) | No Mediator — Direct Use Case Injection |
| [ADR-003](docs/adr/ADR-003-result-type-instead-of-exceptions.md) | Result&lt;T&gt; Instead of Exceptions |
| [ADR-004](docs/adr/ADR-004-jwt-refresh-token-argon2id.md) | JWT + Refresh Token with Argon2id |
| [ADR-005](docs/adr/ADR-005-domain-events-channel-background-service.md) | Domain Events via Channel&lt;T&gt; + BackgroundService |
| [ADR-006](docs/adr/ADR-006-no-automapper-manual-mapping.md) | No AutoMapper — Manual Mapping |
| [ADR-007](docs/adr/ADR-007-testcontainers-integration-tests.md) | TestContainers for Integration Tests |
| [ADR-008](docs/adr/ADR-008-rolenames-no-magic-strings.md) | RoleNames — No Magic Strings |
| [ADR-009](docs/adr/ADR-009-idatetimeprovider-deterministic-sla.md) | IDateTimeProvider for Deterministic SLA Tests |
| [ADR-010](docs/adr/ADR-010-append-only-audit-events.md) | Append-Only Audit Events Table |

---

## Non-Negotiable Architecture Rules

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
