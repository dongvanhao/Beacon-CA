---
name: Auth feature implementation status
description: Register/Login/Logout auth feature (User + Admin) — what was built and key issues found
type: project
---

Auth feature (Register, Login, Logout) for both User and Admin was reviewed on 2026-04-12.

## User auth (reviewed first)

Key patterns confirmed working:
- MediatR CQRS with ValidationBehavior pipeline (FluentValidation throws custom ValidationException before handler)
- BaseController.HandleResult / CreatedResult properly wraps Result<T> → ApiResponse<T>
- Repository pattern: IUserRepository in Domain, UserRepository in Infrashtructure/Repository/Identity/
- BCrypt password hashing, custom JwtService

Known issues found in User auth review:
- Email stored and queried case-sensitively (no .ToLower() normalization in user flow)
- AccessTokenExpiresAt hardcoded as DateTime.UtcNow.AddMinutes(15) in handler — drifts from actual token expiry read from config
- RefreshToken expiry also hardcoded AddDays(7) in handlers instead of reading from config
- No [ApiController] on AuthController (only on BaseController which has default route)
- Swagger ProducesResponseType attributes missing on all AuthController actions
- LogoutRequest (User) has no FluentValidation validator
- appsettings.json has SecretKey in plaintext — acceptable for dev, must use secrets/env in prod

## Admin auth (reviewed 2026-04-12)

Structurally stronger than user auth. Key issues:

- AdminLogoutRequest has no FluentValidation validator (same gap as User LogoutRequest)
- AdminAuthController has no [ApiController] attribute
- No ProducesResponseType Swagger attributes on either action
- AdminAuthResponse.AccessTokenExpiresAt is a plain DateTime (not DateTimeOffset) — same tz ambiguity risk as user auth
- JwtService.GenerateAdminAccessToken shares same JwtSettings section as user tokens (same secret, same expiry config) — no isolation between user and admin token issuers; a stolen user token cannot pass AdminOnly but the signing secret is shared
- HasPermissionAttribute(permission) passes the permission string directly as the policy name — works only if every permission string is pre-registered as a named policy in AddAuthorization; adding a new permission without registering its policy will silently fail (403 always)
- PermissionAuthorizationHandler is registered as Singleton but depends on no scoped services — correct, but worth noting it must stay stateless
- AdminRepository.GetByEmailWithRolesAsync: no .AsNoTracking() on a read-only query used only to produce a JWT; tracking all those nav-property graphs unnecessarily
- Program.cs line 125: db.Database.Migrate() is synchronous on startup thread — blocks startup; also uses Thread.Sleep (line 135) in a potentially async context; acceptable for Docker bootstrap but should use async equivalent (MigrateAsync + Task.Delay) if this ever moves to a proper hosted service

**Why:** Capturing for future feature reviews.
**How to apply:** When reviewing future Admin features, check: policy registration completeness for permission attributes, email normalization, validator coverage for all input DTOs, ProducesResponseType, ApiController attribute.
