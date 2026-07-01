# Beacon

**Beacon** is the backend for a personal-safety & social check-in platform. Users schedule safety check-ins, register emergency contacts, and broadcast alerts when they miss a check-in — while staying connected to friends through posts, reactions, group messaging, and real-time notifications.

The service is built on **.NET 8** following **Clean Architecture** with **CQRS (MediatR)**, the **Result pattern**, and a strict layering discipline. It ships with JWT authentication, fine-grained RBAC, object storage, background jobs, and structured logging — fully containerized for one-command local bring-up.

> **Note on naming:** Some folders/namespaces contain historical typos (`Infrashtructure`, `Presistence`, `Dependencyinjection`, `Intergration`). These are intentionally frozen and tracked as tech debt pending a dedicated rename — they do not affect behavior.

---

## Table of Contents

- [Key Features](#key-features)
- [Architecture](#architecture)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Core Design Decisions](#core-design-decisions)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [API Conventions](#api-conventions)
- [Testing](#testing)
- [Observability](#observability)
- [Roadmap / Tech Debt](#roadmap--tech-debt)

---

## Key Features

| Domain | Capabilities |
|---|---|
| **Identity & Access** | User auth (JWT access + refresh, single-session, token rotation), device registration, separate Admin auth with **RBAC** (roles, permissions, super-admin), admin audit logging |
| **Safety** | Emergency contacts, scheduled check-ins, missed-check-in escalation, alert incidents, daily safety records |
| **Social** | Friend requests & pairs, posts with reactions, post reporting & moderation |
| **Messaging** | Group messaging with per-member settings |
| **Notifications** | Real-time delivery via **SignalR**, push via **Firebase Cloud Messaging (FCM)**, per-user notification preferences |
| **Storage** | Media upload/serving via **MinIO** (S3-compatible), presigned URLs, image processing |
| **Administration** | User/admin account management, post moderation, reports, statistics dashboard |

---

## Architecture

Clean Architecture with dependencies pointing **inward**. The Domain layer has zero framework dependencies; the Application layer never references EF Core.

```
┌─────────────────────────────────────────────────────────────┐
│  Beacon.Api            Controllers, Middleware, SignalR Hubs,│
│                        Filters, DI wiring, Background jobs    │
├─────────────────────────────────────────────────────────────┤
│  Beacon.Application    MediatR handlers (CQRS), DTOs,         │
│                        Validators, manual Mappers            │
├─────────────────────────────────────────────────────────────┤
│  Beacon.Domain         Entities, Enums, Repository interfaces │
│                        (ZERO framework dependencies)          │
├─────────────────────────────────────────────────────────────┤
│  Beacon.Infrashtructure  EF Core DbContext, Repositories,    │
│                          JWT, MinIO, FCM, Hangfire           │
├─────────────────────────────────────────────────────────────┤
│  Beacon.Shared         Result<T>, ApiResponse<T>, Pagination,│
│                        Error codes (cross-cutting)           │
└─────────────────────────────────────────────────────────────┘

Dependency direction:  Api → Application → Domain ← Infrashtructure
                                              ↑
                                          Shared
```

**Request lifecycle:**

```
HTTP → Controller → MediatR.Send(Command/Query)
     → ValidationBehavior (FluentValidation)
     → Handler → Repository (interface) → EF Core → SQL Server
     → Result<T> → BaseController maps ErrorType → HTTP → ApiResponse<T>
```

---

## Tech Stack

| Concern | Technology |
|---|---|
| Runtime | .NET 8 (ASP.NET Core Web API) |
| Mediation / CQRS | MediatR |
| Validation | FluentValidation (MediatR pipeline behavior) |
| ORM / Database | EF Core 8 + SQL Server (InMemory provider for tests/dev) |
| AuthN / AuthZ | JWT Bearer, custom RBAC attributes (`[HasPermission]`, `[AdminOnly]`) |
| Password hashing | BCrypt |
| Object storage | MinIO (S3-compatible) + SixLabors.ImageSharp |
| Real-time | SignalR (Redis backplane-ready) |
| Push notifications | Firebase Admin SDK (FCM) |
| Background jobs | Hangfire (SQL Server storage + dashboard) |
| Rate limiting | ASP.NET Core built-in rate limiter |
| Logging | Serilog (structured, console sink, request logging) |
| API docs | Swashbuckle / Swagger (OpenAPI) |
| Containerization | Docker + Docker Compose |

---

## Project Structure

```
src/
├── Beacon.Api/               # Entry point, controllers, middleware, hubs, DI extensions
│   ├── Controllers/          # Grouped by module (Identity, Safety, Posts, Management, …)
│   ├── Extensions/           # AddInfrastructure / AddApplication / AddApiAuth / …
│   ├── Middleware/           # ExceptionHandlingMiddleware, request logging
│   ├── Hubs/                 # SignalR (BeaconHub)
│   └── Program.cs            # Extension-method-only composition root
├── Beacon.Application/       # Features/{Module}/Commands|Queries, Dtos, Validators, Mappings
├── Beacon.Domain/            # Entities/{Module}, Enums, IRepository
├── Beacon.Infrashtructure/   # EF DbContext, Repositories, Services (JWT, MinIO, FCM)
├── Beacon.Shared/            # Result<T>, ApiResponse<T>, Pagination, ErrorCodes
└── tests/
    ├── Beacon.UnitTests/         # Handler-level unit tests
    └── Beacon.IntergrationTests/ # WebApplicationFactory end-to-end tests
```

---

## Core Design Decisions

**Result pattern over exceptions for business flow.** Handlers and services return `Result<T>`; exceptions are reserved for unrecoverable/infrastructure errors. `BaseController` maps `ErrorType → HTTP status`, and `ExceptionHandlingMiddleware` catches the rest — so controllers never contain `try/catch`.

**One handler = one use case.** No god-services. Commands mutate; Queries read. A Query never invokes a Command and vice versa.

**Repository abstraction.** Handlers depend on domain-meaningful repository interfaces (`IUserRepository`), never on `DbContext` directly. `SaveChangesAsync` is orchestrated by the handler.

**Manual, explicit mapping.** Each mapper is a `sealed`, pure, synchronous class registered as a singleton — no reflection-based mapping library, keeping mappings debuggable and allocation-friendly.

**Uniform response envelope.** Every response is an `ApiResponse<T>` (`{ success, message, code, data, errors }`) — including failures and deletes (no bare `204`).

**Composition via extension methods.** `Program.cs` only calls `AddInfrastructure`, `AddApplication`, `AddApiAuth`, etc. — no service is registered inline.

**Soft delete by default** for recoverable/auditable entities, enforced via global EF query filters in `OnModelCreating`.

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for SQL Server, MinIO, and the full stack)

### Option A — Full stack via Docker Compose (recommended)

Spins up SQL Server, MinIO (+ nginx front), and the API. Migrations are applied automatically on startup with retry/back-off.

```bash
cp .env.example .env      # then fill in the secrets below
docker compose up -d --build
```

| Service | URL |
|---|---|
| API | http://localhost:5000 |
| Swagger UI | http://localhost:5000/swagger |
| MinIO Console | http://localhost:9001 |
| Hangfire Dashboard | http://localhost:5000/hangfire |

### Option B — Run the API locally

Fast inner loop using the **InMemory** database provider and no external dependencies:

```bash
cd src
dotnet restore && dotnet build

# InMemory + seeded dev data — no SQL Server / MinIO required
DATABASE_PROVIDER=InMemory \
DEV_SEED_ENABLED=true \
EXTERNAL_SERVICES_USE_NOOP_STORAGE=true \
ASPNETCORE_ENVIRONMENT=Development \
dotnet run --project Beacon.Api
```

For SQL Server locally, apply migrations manually:

```bash
dotnet ef database update \
  --project Beacon.Infrashtructure \
  --startup-project Beacon.Api
```

---

## Configuration

Secrets are supplied via **environment variables** (`.env` for Docker) or **User Secrets** (local dev) — never committed to `appsettings.json`.

| Variable | Description |
|---|---|
| `DB_PASSWORD` | SQL Server `sa` password |
| `JWT_SECRET_KEY` | 64-char hex signing key for JWT |
| `SEED_ADMIN_EMAIL` / `SEED_ADMIN_PASSWORD` | Initial super-admin credentials |
| `MINIO_ROOT_USER` / `MINIO_ROOT_PASSWORD` | MinIO access & secret keys |
| `MINIO_PUBLIC_ENDPOINT` / `MINIO_PUBLIC_HOST` | Public URL clients use to fetch media |
| `FIREBASE_CREDENTIAL_JSON` | FCM service-account JSON (leave empty to disable push; SignalR still works) |
| `DATABASE_PROVIDER` | `SqlServer` (default) or `InMemory` |
| `DEV_SEED_ENABLED` | Seed demo data on startup (Development only) |
| `EXTERNAL_SERVICES_USE_NOOP_STORAGE` | Stub out MinIO for offline dev |
| `ASPNETCORE_ENVIRONMENT` | `Development` / `Production` |

See [`.env.example`](.env.example) for the full template.

---

## API Conventions

- **Base path:** `/api/v1/{resource}` (kebab-case, plural nouns). Admin routes under `/api/v1/admin/...`.
- **Route params:** typed — `{id:guid}`.
- **Envelope:** all responses are `ApiResponse<T>`:

```jsonc
// success
{ "success": true,  "message": "...", "code": null,             "data": { }, "errors": null }
// failure
{ "success": false, "message": "...", "code": "USER_NOT_FOUND", "data": null, "errors": ["..."] }
```

- **Error codes:** `SCREAMING_SNAKE_CASE`, centrally defined in `Beacon.Shared/Constants/ErrorCodes.cs`.
- **Pagination:** cursor-based for feeds/timelines (`?cursor=&limit=`); offset-based for admin lists needing a total count.
- **AuthZ:** endpoints are guarded by `[AllowAnonymous]`, `[Authorize]`, `[AdminOnly]`, or `[HasPermission("resource:action")]`.
- Every endpoint is documented with XML doc comments surfaced in Swagger.

Full interactive documentation is available at `/swagger` when the API is running.

---

## Testing

```bash
cd src
dotnet test                                   # all tests
dotnet test tests/Beacon.UnitTests            # handler unit tests
dotnet test tests/Beacon.IntergrationTests    # WebApplicationFactory integration tests
```

- **Unit tests** cover each handler in isolation (mocked repositories/services).
- **Integration tests** exercise controllers end-to-end via `WebApplicationFactory` against the InMemory provider.
- Bug fixes follow a **prove-it** workflow: a failing test is written first, then the fix.

---

## Observability

- **Structured logging** via Serilog with automatic request summaries (`UseSerilogRequestLogging`) — human-readable in Development, compact JSON in Production.
- **Correlation** via `TraceId` from `Activity` (OpenTelemetry-ready).
- **Privacy-aware:** only `UserId` (GUID) is logged for identity; no PII, tokens, or secrets. Sensitive query params are masked.
- **Health checks:**

```
GET /health         # liveness
GET /health/ready   # readiness (SQL Server + MinIO)
GET /health/db
GET /health/minio
```

---

## Roadmap / Tech Debt

- **Namespace rename** — retire the historical typos (`Infrashtructure`, `Presistence`, `Intergration`) in a dedicated sprint.
- **Distributed caching** — introduce Redis with the cache-aside pattern (`IDistributedCache`).
- **Idempotency** — `Idempotency-Key` support for resource-creating endpoints (mobile retry safety).
- **Resilience** — Polly retry / circuit breakers around MinIO and outbound HTTP calls.
- **Observability** — OpenTelemetry traces/metrics; centralized log sink (Seq/Loki).
- **CI/CD** — automated build, test, and deployment pipeline.

---

<p align="center"><sub>Built with .NET 8 · Clean Architecture · CQRS · Result Pattern</sub></p>
