---
name: spec
description: Spec before code — structured PRD creation for new features
---

# /spec — Specification-Driven Development

> "Plan the work, then work the plan."

## Purpose

Create a comprehensive specification document **before** writing any code. This ensures alignment on requirements, architectural constraints (Clean Architecture, CQRS), and acceptance criteria within the Beacon project.

## Workflow

### Phase 1: Discovery (Ask Questions)

Before generating a spec, gather requirements by asking:

**Scope**
- What is the objective of this feature?
- Who are the target users/roles (e.g., Admin, SuperAdmin, User)?
- What problem does this solve in the Beacon ecosystem?

**Features**
- What are the core use cases (MVP)?
- What are the acceptance criteria for each?
- What is explicitly out of scope?

**Technical**
- Are there specific MediatR Commands/Queries needed?
- What Entities or Value Objects need to be created or modified in the Domain?
- How does this impact the EF Core Database Schema/Migrations?

### Phase 2: Generate Specification

After discovery, produce `SPEC.md` with these sections:

```markdown
# Feature: [Name]

## Objective
[1-2 sentences describing the goal]

## Target Users
[Who will use this and their required Roles/Permissions]

## Core Features & Use Cases
1. [Use Case A] — [Acceptance criteria]
2. [Use Case B] — [Acceptance criteria]

## Out of Scope
- [What we're NOT building in this iteration]

## Technical Approach (Clean Architecture)
- **Domain Layer**: New Entities, Domain Events, Enums, or modifications to existing ones.
- **Application Layer**: 
  - Commands & Queries (MediatR)
  - DTOs & Validation Rules (FluentValidation targeting Command/Query objects)
  - Handlers implementing core business logic
- **Infrastructure Layer**: Entity Framework Core mappings (Configurations), new repositories, or external services integration.
- **Presentation/API Layer**: Controller endpoints, Routing, Swagger response types, and Localized error messages (Vietnamese).

## Code Style & Architecture
- Follow strict Clean Architecture and CQRS patterns.
- Validators MUST target MediatR `Command`/`Query` objects to ensure interception in the MediatR Pipeline.
- Use `BaseController` for standard API responses.

## Testing Strategy
- **Unit Tests (xUnit, Moq, FluentAssertions)**: Domain logic, Application Handlers, and FluentValidation rules.
- **Integration Tests**: API endpoints and database operations (EF Core).

## Boundaries
### Always Do
- Keep Controllers thin: primarily map HTTP requests to MediatR messages.
- Ensure all input validations return descriptive, localized messages.
- Preserve idempotency in DB migrations to avoid deployment conflicts.

### Ask First
- Adding new NuGet packages.
- Modifying core Auth mechanisms or Role-based access control (e.g., SuperAdmin bypass).
- Making breaking changes to existing database tables.

### Never Do
- Place business logic in API Controllers.
- Reference `Infrastructure` or `Presentation` layers directly from the `Application` or `Domain` layers.
```

### Phase 3: Review & Confirm

- Present the spec to the user
- Confirm before proceeding to `/plan`
- Save as `SPEC.md` in project root or `docs/specs/[feature].md`

## Output

- `SPEC.md` — The specification document customized for .NET Clean Architecture.
- Clear alignment on what to build.

## Next Step

After spec is approved, run `/plan` to decompose into tasks.
