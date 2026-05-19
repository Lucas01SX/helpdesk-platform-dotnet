# Environment Variables

All sensitive configuration is supplied via environment variables. The `appsettings.json` file contains only non-sensitive defaults and placeholder values — never real secrets.

---

## Required Variables

These must be set before running the application (Docker or local).

| Variable | Description | Example |
|---|---|---|
| `ConnectionStrings__Default` | PostgreSQL connection string | `Host=db;Port=5432;Database=helpdesk;Username=app;Password=secret` |
| `Jwt__SecretKey` | HMAC-SHA256 signing key for JWT access tokens. Must be at least 32 characters. | `your-256-bit-secret-key-here` |
| `DB_NAME` | Database name (Docker Compose only — passed to the PostgreSQL container) | `helpdesk` |
| `DB_USER` | Database user (Docker Compose only) | `app` |
| `DB_PASSWORD` | Database password (Docker Compose only) | `secure-password` |

---

## Optional Variables

These have defaults in `appsettings.json` and only need to be overridden when the default is not appropriate.

| Variable | Default | Description |
|---|---|---|
| `Jwt__Issuer` | `helpdesk-api` | JWT `iss` claim |
| `Jwt__Audience` | `helpdesk-clients` | JWT `aud` claim |
| `Jwt__ExpiryMinutes` | `15` | Access token lifetime in minutes |
| `ASPNETCORE_ENVIRONMENT` | `Production` (Docker) | Set to `Development` to enable Scalar UI at `/scalar/v1` |
| `Serilog__MinimumLevel__Default` | `Information` | Minimum log level (`Verbose`, `Debug`, `Information`, `Warning`, `Error`) |

---

## Docker Compose Setup

Create a `.env` file in the `helpdesk-platform-dotnet/` directory (never commit this file):

```env
DB_NAME=helpdesk
DB_USER=app
DB_PASSWORD=change-me
JWT_SECRET_KEY=change-me-to-a-long-random-string-at-least-32-chars
```

The `docker-compose.yml` references these values and passes them to both the API and PostgreSQL containers.

---

## Local Development Setup

Create `src/Helpdesk.API/appsettings.Development.json` (gitignored):

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=helpdesk_dev;Username=postgres;Password=your-local-password"
  },
  "Jwt": {
    "SecretKey": "local-dev-secret-key-at-least-32-characters"
  }
}
```

---

## Security Notes

- `Jwt__SecretKey` must be at least 256 bits (32 characters) for HMAC-SHA256. Use a cryptographically random value in production.
- `DB_PASSWORD` and `JWT_SECRET_KEY` must never appear in source code, Docker image layers, or CI logs.
- The `.env` file is listed in `.gitignore` — verify before committing.
- Refresh tokens are stored as **Argon2id hashes** — the raw token never touches the database.
