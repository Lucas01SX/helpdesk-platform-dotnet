# ADR-004: JWT Access Token + Refresh Token with Argon2id Hashing

**Status:** Accepted  
**Date:** 2025-05-01

## Context

Stateless JWT tokens cannot be revoked. Long-lived JWTs are a security risk. Short-lived JWTs alone create a poor user experience (frequent re-authentication). A refresh token scheme balances security and usability, but refresh tokens stored in plain text are vulnerable to database breaches.

## Decision

- **Access token**: JWT, 15-minute expiry, returned in response body only (client keeps in memory, never LocalStorage).
- **Refresh token**: 128-byte cryptographically random token, 7-day expiry, stored as **Argon2id hash** in the database, sent and received as an `HttpOnly; Secure; SameSite=Strict` cookie scoped to `/api/auth`.
- **Session families**: each login creates a new family. Every `PUT /api/auth/sessions/current` rotates the token within the family.
- **Reuse detection**: if a revoked refresh token is presented, the entire family is immediately invalidated (all active sessions for that family are killed).
- **Session limit**: max 5 active sessions per user. The oldest is evicted when the limit is reached.

## Consequences

- A database breach exposes only Argon2id hashes — useless without the original random token.
- The `HttpOnly` cookie prevents JavaScript access to the refresh token — XSS cannot steal it.
- Reuse detection mitigates refresh token theft: if an attacker uses a stolen token, the legitimate user's next refresh invalidates the entire family, alerting to the breach.
- Access tokens in memory are lost on page reload — clients must call `PUT /sessions/current` on startup to obtain a new access token from the cookie.
