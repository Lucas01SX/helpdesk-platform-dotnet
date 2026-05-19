# ADR-008: RoleNames Static Class — No Magic Strings

**Status:** Accepted  
**Date:** 2025-05-01

## Context

Role names (`"Customer"`, `"SupportAgent"`, `"Manager"`) appeared as string literals in use cases, query services, controllers, and JWT configuration. A typo or a rename in one place would silently break authorization checks at runtime — no compile error, no test failure unless the specific path was exercised.

## Decision

Centralize all role name constants in `Helpdesk.Shared.Security.RoleNames`:

```csharp
public static class RoleNames
{
    public const string Customer = "Customer";
    public const string Manager = "Manager";
    public const string SupportAgent = "SupportAgent";
}
```

All usages across use cases, query services, controllers, and `[Authorize(Roles = ...)]` attributes reference this class.

## Consequences

- Renaming a role requires a single change in `RoleNames` — all usages update automatically.
- Typos produce a compile error (`RoleNames.Custumer` → `CS0117`).
- The class lives in `Helpdesk.Shared`, accessible to all modules without creating cross-module dependencies.
- Discovered and enforced during the M3 security review — 9 magic string occurrences replaced in a single refactoring pass.
