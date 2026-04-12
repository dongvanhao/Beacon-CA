---
name: Auth feature implementation status
description: Register/Login/Logout auth feature — what was built and key issues found
type: project
---

Auth feature (Register, Login, Logout) was reviewed on 2026-04-12. Implementation is structurally sound but has several issues:

Key patterns confirmed working:
- MediatR CQRS with ValidationBehavior pipeline (FluentValidation throws custom ValidationException before handler)
- BaseController.HandleResult / CreatedResult properly wraps Result<T> → ApiResponse<T>
- Repository pattern: IUserRepository in Domain, UserRepository in Infrashtructure/Repository/Identity/
- BCrypt password hashing, custom JwtService

Known issues found in review:
- Email stored and queried case-sensitively (no .ToLower() normalization)
- AccessTokenExpiresAt hardcoded as DateTime.UtcNow.AddMinutes(15) in handler — drifts from actual token expiry read from config
- RefreshToken expiry also hardcoded AddDays(7) in handlers instead of reading from config
- No [ApiController] on AuthController (only on BaseController which has default route — AuthController does not re-declare it)
- Swagger ProducesResponseType attributes missing on all AuthController actions
- LogoutRequest has no FluentValidation validator
- LoginCommandHandler references full namespace for RefreshToken.Create (minor, not a bug)
- appsettings.json has SecretKey in plaintext — acceptable for dev, must use secrets/env in prod
- UserRepository email queries are case-sensitive at application layer (DB collation may compensate but no guarantee)

**Why:** Capturing for future feature reviews to avoid re-finding same issues.
**How to apply:** When reviewing future features, check these same patterns — token expiry config, email normalization, validator coverage for all input DTOs.
