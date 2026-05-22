# ASP.NET Core API Conventions

## 1. Error Code Naming

All error codes follow the pattern `{module}.{snake_case_error}`. This ensures codes are:
- Unique across modules (no `not_found` collision)
- Machine-readable by clients
- Traceable back to the source module

| Module | Prefix | Examples |
|---|---|---|
| Identity | `identity.` | `identity.invalid_credentials`, `identity.email_not_verified` |
| Tickets | `ticket.` | `ticket.not_found`, `ticket.forbidden`, `ticket.invalid_transition` |
| Attachments | `attachment.` | `attachment.not_found`, `attachment.file_too_large` |
| Validation | `validation_error` | Model binding / data annotation failures |

Never use a bare error code (`not_found`, `forbidden`) — always prefix with the module.

## 2. HTTP Status Mapping

| Scenario | Status |
|---|---|
| Successful creation | 201 Created |
| Successful state transition (resolve, cancel, etc.) | 204 No Content |
| Successful query | 200 OK |
| Business rule violation (invalid transition, max changes) | 409 Conflict |
| Authorization failure | 403 Forbidden |
| Authentication failure | 401 Unauthorized |
| Not found | 404 Not Found |
| Rate limit exceeded | 429 Too Many Requests |
| Validation error | 400 Bad Request |

## 3. Response Envelope

All responses (success and error) carry the standard envelope defined in `ApiControllerBase`:

```json
// Success
{ "data": {}, "correlationId": "uuid", "timestamp": "2024-01-01T00:00:00Z" }

// Error
{ "success": false, "error": { "code": "ticket.not_found", "message": "Ticket not found" }, "correlationId": "uuid", "timestamp": "..." }
```

The `ApiControllerBase.Success<T>()` wraps all non-error responses. Error responses are set by `GlobalExceptionHandlerMiddleware` and the `MapResult()` helper.

## 4. REST Action Endpoints

State transitions use `POST` on a sub-resource, never `PUT` on the root resource:

```
POST /api/tickets/{id}/resolve     ✅
POST /api/tickets/{id}/cancel      ✅
PUT  /api/tickets/{id}             ❌ (breaks idempotency of state transitions)
```

Field-level partial updates use `PATCH`:

```
PATCH /api/tickets/{id}/priority   ✅
PATCH /api/auth/email-verifications ✅
```

## 5. Rate Limiting

Named policies configured in `Program.cs` → `AddRateLimiter()`:

| Policy name | Limit | Window | Applied to |
|---|---|---|---|
| `login` | 5 requests | 60 seconds | `POST /api/auth/sessions` |
| `password-reset` | 3 requests | 1 hour | `POST /api/auth/password-resets` |
| `upload` | 10 requests | 1 hour | `POST /api/tickets/:id/attachments` |

All policies key by resolved IP (after `ForwardedHeaders` processing — never by raw `X-Forwarded-For` header).

Rate limiting is disabled in the `Test` environment. `AuthRateLimitTests` uses a separate factory that runs under `Development` where rate limiting is active.

## 6. Pagination

List endpoints accept `?page=1&limit=20` query parameters. Default: page=1, limit=20. Max limit: 100. Response includes `total`, `page`, `limit`, and `items`.

## 7. Naming Conventions

```
Use Cases:     CreateTicketUseCase, ResolveTicketUseCase    ❌ TicketService
Repositories:  ITicketRepository                            ❌ IRepository<T>
Requests:      CreateTicketRequest, ResolveTicketRequest    ❌ TicketDto
Responses:     TicketSummaryResponse, TicketResponse        ❌ ResponseModel
Domain Events: TicketResolved, PriorityChanged              ❌ OnTicketResolved
Tests:         Should_Not_Resolve_Ticket_Without_Assignee   ❌ TestResolve
```
