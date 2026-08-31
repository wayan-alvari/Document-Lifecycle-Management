# Document Lifecycle Management

## 1. Project purpose

Build a small but complete portfolio application that demonstrates how documents move from draft to active use, revision, expiry, and archive. The application must be easy for a recruiter to run locally or try on a public host, while remaining independent from every employer-owned system.

This is a clean-room portfolio project. It is inspired only by common document-management workflows and must not reproduce company implementation details.

## 2. Product goals

The finished application must demonstrate:

- secure role-based authentication;
- document metadata, revisions, and protected file storage;
- lifecycle rules for draft, active, expiring, expired, and archived documents;
- search, filters, dashboard summaries, and exports;
- an immutable activity history;
- isolated demo data that resets after six hours of inactivity;
- automated tests and a reproducible manual publish process.

The application is intentionally a portfolio-sized modular monolith. It is not an enterprise records-management platform.

## 3. Clean-room and publication rules

These rules are mandatory:

1. Do not copy source code, database schemas, migrations, routes, class names, status IDs, business formulas, UI markup, CSS, JavaScript, reports, screenshots, text, configuration, secrets, or assets from any employer-owned system or repository.
2. Do not use an employer name, logo, branch name, customer name, document, screenshot, production-like identifier, or confidential terminology.
3. Use newly written code, generic English terminology, and obviously synthetic seed data.
4. Obtain AdminLTE and every other dependency from its official public distribution. Never copy a vendor folder from a company application.
5. Record every runtime and build dependency in `THIRD-PARTY-NOTICES.md`, including its pinned version, license, project URL, and purpose.
6. Add `CLEAN-ROOM-DECLARATION.md` and repeat a short independence statement in `README.md`.
7. Before publication, inspect the full Git history as well as the current tree for secrets, company references, copied assets, connection strings, uploaded files, and personal data.

Required README wording, which may be polished without changing its meaning:

> This independent portfolio project was built from scratch using generic document-lifecycle concepts. It contains no employer source code, confidential data, proprietary branding, or copied business assets.

The repository license is MIT:

`Copyright (c) 2026 Wayan Alvari`

The MIT license applies to original source in this repository. Third-party components retain their own licenses.

## 4. Fixed technical baseline

- ASP.NET Core MVC on .NET 8 and C# 12.
- Latest .NET 8 servicing SDK approved for the project: SDK `8.0.424`, runtime/package line `8.0.30`.
- MySQL Community Server 8.0.46.
- Entity Framework Core `8.0.30` with code-first migrations.
- Pomelo.EntityFrameworkCore.MySql `8.0.3`.
- ASP.NET Core Identity with cookie authentication.
- AdminLTE `4.1.0`, installed from the official npm package and locked in `package-lock.json`.
- Bootstrap 5 and vanilla JavaScript; do not introduce jQuery merely for the theme.
- ClosedXML `0.105.1` for XLSX export when implemented.
- PDFsharp/MigraDoc `6.2.4` for a compact PDF summary when implemented.
- xUnit plus built-in assertions for tests. Do not add a test assertion package with a restrictive/commercial license.

All packages must be stable, compatible with `net8.0`, free for a public personal portfolio, and listed in the third-party notice. Use central NuGet package management and exact versions. Commit the npm lockfile. Do not silently upgrade a major version during implementation.

`.NET 8` is a transitional baseline and reaches end of support on 10 November 2026. Keep it on the newest available 8.0 security patch while developing, add a tracked `.NET 10` upgrade note to the roadmap, and do not keep a public deployment on .NET 8 after end of support.

Do not use Docker, paid UI controls, Kendo UI, a commercial PDF library, or a dependency copied from a company project.

### SDK preparation

The workstation currently has an older .NET 8 patch. Install SDK `8.0.424` or a later approved .NET 8 servicing SDK before executing the build prompt. Add `global.json` and use a repository-local tool manifest containing `dotnet-ef` `8.0.30`; do not rely on a global EF 9/10 tool.

Useful preflight commands:

```powershell
dotnet --list-sdks
dotnet --list-runtimes
node --version
npm --version
git --version
Get-Service MySQL80
```

## 5. Solution shape

Use a pragmatic layered modular monolith:

```text
src/
  DocumentLifecycle.Web/
  DocumentLifecycle.Application/
  DocumentLifecycle.Domain/
  DocumentLifecycle.Infrastructure/
tests/
  DocumentLifecycle.UnitTests/
  DocumentLifecycle.IntegrationTests/
docs/
scripts/
DocumentLifecycle.sln
Directory.Build.props
Directory.Packages.props
global.json
.config/dotnet-tools.json
package.json
package-lock.json
README.md
LICENSE
THIRD-PARTY-NOTICES.md
CLEAN-ROOM-DECLARATION.md
```

Responsibilities:

- `Domain`: entities, value objects, lifecycle rules, and domain exceptions; no EF Core or web dependency.
- `Application`: use cases, DTOs, validation, authorization-facing abstractions, clock/storage/workspace interfaces.
- `Infrastructure`: EF Core, Identity, MySQL configuration, migrations, seeders, protected file storage, and cleanup services.
- `Web`: MVC controllers, Razor views, view models, AdminLTE shell, dependency injection, middleware, and HTTP security.

Prefer clear application services over unnecessary generic repositories, mediator pipelines, event buses, or microservices. Controllers must stay thin. Use async database and file APIs and pass cancellation tokens.

## 6. Database and configuration

Use a separate database and least-privilege application account:

- development database: `portfolio_document_lifecycle`;
- optional manual integration-test database: `portfolio_document_lifecycle_test`;
- table and column naming: lowercase `snake_case`;
- character set/collation: `utf8mb4` / `utf8mb4_0900_ai_ci`;
- all stored timestamps: UTC;
- decimal and date types must be explicitly configured;
- migrations belong to `DocumentLifecycle.Infrastructure`.

Never commit a database password. Keep a safe placeholder in tracked settings and put the actual development connection string in .NET user secrets or environment variables:

```powershell
dotnet user-secrets set --project src/DocumentLifecycle.Web `
  "ConnectionStrings:DefaultConnection" `
  "Server=127.0.0.1;Port=3306;Database=portfolio_document_lifecycle;User=<app_user>;Password=<password>;CharSet=utf8mb4"
```

The README must include example SQL for an administrator to create the database and a dedicated local user, but it must contain placeholders rather than a real password. Do not automatically run destructive migrations or seed demo data in a production environment. A documented development-only migration/seed command is acceptable.

The normal automated test suite must not require a developer's MySQL password. Use SQLite in-memory for relational web/integration tests and unit-test the domain rules. Provide an optional MySQL smoke-test procedure for the configured local test database.

## 7. Authentication and demo accounts

Use ASP.NET Core Identity. Disable public registration, password reset email, external login, and user administration for this mini version.

Seed these public demo users only in demo/development mode:

| Role | Email | Password | Main capability |
|---|---|---|---|
| Administrator | `admin@documents.demo` | `PortfolioDemo123!` | configuration and all records |
| Document Manager | `manager@documents.demo` | `PortfolioDemo123!` | create, revise, activate, and archive documents |
| Viewer | `viewer@documents.demo` | `PortfolioDemo123!` | read, search, download, and export |

Display the credentials on the login screen and in the README. Seed idempotently. Demo users cannot change their password/email, create roles, delete accounts, or gain additional privileges. Add a convenient logout-and-switch-role path without bypassing authentication.

Authorization must be enforced server-side with policies; hiding a menu item is not authorization.

## 8. Per-browser demo workspace and six-hour reset

Shared credentials must not mean shared mutable data. Each browser receives a cryptographically protected persistent `DemoWorkspaceId` cookie. Every domain record and upload belongs to that workspace. Logging out and logging in as another demo role in the same browser keeps the workspace, allowing the reviewer to test a multi-role workflow. Another browser or private window gets a different workspace.

Required behavior:

- create and seed a workspace on first real application request;
- update `LastActivityAtUtc` only for meaningful authenticated navigation or commands, throttled to avoid a write on every request;
- ignore static files, health checks, and background polling as activity;
- expire a workspace after six continuous hours without meaningful activity;
- on the next request, delete only that expired workspace's domain rows and uploaded files, create a new workspace, and seed the original sample data;
- run a modest periodic cleanup service while the app is alive, in addition to request-time expiry;
- never delete Identity users or data outside the expired workspace;
- enable cleanup only when `DemoMode:Enabled` is true;
- use transactions/idempotency so concurrent requests cannot create duplicate seed data.

Show this exact behavior prominently in the UI and README:

> Demo data is isolated per browser and automatically resets after 6 hours of inactivity.

## 9. Domain model

The exact implementation may evolve, but the following concepts are required:

- `DemoWorkspace`: public-safe ID, created time, last meaningful activity time, expiry time, seed version.
- `DocumentCategory`: name, description, active flag.
- `DocumentOwner`: display name, email or team label, active flag.
- `ManagedDocument`: generated code, title, description, category, owner, effective date, optional expiry date, lifecycle state, archive metadata, created/updated metadata.
- `DocumentRevision`: document, sequential revision number, change note, original filename, stored filename, media type, size, SHA-256 hash, uploader, upload time.
- `Notification`: recipient role/user where appropriate, message, link, read time, creation time.
- `AuditEvent`: actor, action, entity type/public ID, UTC time, and a safe JSON/details summary that never stores file content or secrets.

All domain records above include `WorkspaceId`. Use opaque public IDs in URLs instead of sequential database IDs where practical. Add indexes for workspace plus common filters and unique constraints such as document code within a workspace.

### Lifecycle rules

Persist only the workflow state `Draft`, `Active`, or `Archived`. Present `Expiring Soon` and `Expired` as derived statuses:

- `Draft`: metadata may be edited; cannot be downloaded by a Viewer until active.
- `Active`: requires title, category, owner, effective date, and at least one valid revision.
- `Expiring Soon`: active and expiry is from today through 30 days ahead, inclusive.
- `Expired`: active and expiry is before today.
- `Archived`: intentionally removed from active circulation; retains revisions and history.

Allowed commands:

```text
Draft --Activate--> Active
Active --Add revision--> Active
Active --Archive--> Archived
Archived --Restore--> Active
```

Activation, archive, restore, and revision upload must produce audit events. The application must reject invalid transitions server-side. Date calculations use an injected UTC clock and a clearly documented display timezone.

## 10. Functional scope and pages

### Public and account pages

- landing/login page with app explanation, demo credentials, reset notice, and clean-room statement;
- access denied, not found, and safe error pages;
- logout and role-switch guidance.

### Dashboard

- cards for Total, Draft, Active, Expiring Soon, Expired, and Archived;
- recent activity;
- documents expiring in the next 30 days;
- one compact chart created with an MIT-compatible dependency already included by AdminLTE, or a simple CSS/HTML visualization if a new dependency is unnecessary.

### Documents

- server-side paginated list with search and lifecycle/category/owner/expiry filters;
- create and edit draft metadata;
- detail page with metadata, current file, revision history, status badge, and audit timeline;
- activate, add revision with a required change note, archive, and restore commands;
- protected download action;
- XLSX list export that honors the current filters;
- compact PDF document summary containing metadata and history, not the uploaded document itself.

### Configuration

- category and owner CRUD with safe delete/deactivate rules;
- only Administrator may change configuration;
- prevent deletion when referenced; prefer deactivation.

### Notifications and audit

- in-app expiry notifications generated idempotently;
- mark one/all as read;
- searchable audit-event list for Administrator and Viewer;
- no real SMTP, SMS, push service, or background external integration.

## 11. Authorization matrix

| Capability | Administrator | Document Manager | Viewer |
|---|:---:|:---:|:---:|
| View dashboard and active documents | Yes | Yes | Yes |
| View drafts | Yes | Yes | No |
| Create/edit draft | Yes | Yes | No |
| Upload revision | Yes | Yes | No |
| Activate/archive/restore | Yes | Yes | No |
| Download permitted revision | Yes | Yes | Active/archived only |
| Export filtered results | Yes | Yes | Yes |
| Manage categories/owners | Yes | No | No |
| View full audit log | Yes | Own workflow context | Yes |

Test important allowed and denied cases. Every query and command must also enforce workspace isolation.

## 12. File handling and security

- Store uploaded content outside `wwwroot` and outside Git.
- Allow only PDF, PNG, and JPEG with a small documented limit, recommended 10 MB.
- Validate extension, declared media type, and file signature; never trust `IFormFile.FileName`.
- Generate a random physical filename and retain only a sanitized original name for display.
- Calculate SHA-256, prevent path traversal, and stream through an authorized controller action.
- Set safe `Content-Disposition`, `X-Content-Type-Options: nosniff`, and a restrictive Content Security Policy compatible with the chosen local assets.
- Use antiforgery protection on state-changing MVC actions, secure production cookies, SameSite, HTTPS redirection outside test, rate limiting on login, validation limits, and centralized exception handling.
- Do not expose stack traces, database errors, absolute paths, secrets, or numeric internal IDs.
- Never render uploaded HTML/SVG or user input as raw HTML.

Add structured logs, but do not log passwords, connection strings, cookies, file contents, or sensitive form values. Add `/health` with a lightweight liveness response; keep detailed diagnostics private.

## 13. UI and accessibility

All UI, validation messages, seeded content, README text, and screenshots must be English.

Create an original layout composition using AdminLTE 4.1.0 components, not a copy of a company layout. Include responsive navigation, breadcrumbs, active menu state, consistent badges, empty states, loading/disabled submit states, confirmation modals where appropriate, and keyboard-visible focus. Forms need labels and validation summaries; color cannot be the only status signal.

Copy only the required production assets from the official npm packages during the frontend build. Do not deploy AdminLTE demo pages, sample images, or unused plugins.

## 14. Seed data

Seed a small fictional dataset per new workspace, for example:

- categories: Policy, Procedure, Certificate, Contract;
- owners: Operations Team, People Team, Finance Team;
- 10-15 clearly fictional documents distributed across draft, active, expiring, expired, and archived states;
- revisions and audit events sufficient to make the dashboard useful.

Use fictional file contents generated by the project or small text-based test fixtures. Do not seed any real company document or realistic confidential data.

## 15. Testing strategy

Minimum automated coverage:

- unit tests for activation requirements, valid/invalid transitions, expiry boundaries, sequential revisions, and safe filename behavior;
- application tests for role policies and filtered queries;
- integration tests for login, access denied, antiforgery-aware commands, workspace isolation, workspace expiry/reset, CRUD happy paths, protected download, and upload rejection;
- persistence tests using SQLite in-memory for the normal suite;
- optional MySQL smoke test covering migration, seed, and one document workflow when test credentials are configured.

Use a deterministic fake clock and temporary upload directory in tests. Tests must be independent and parallel-safe. Collect coverage as an artifact, but do not chase an arbitrary percentage at the expense of meaningful cases.

## 16. Milestones and commit checkpoints

Every milestone ends with a focused Git commit. Before each commit, inspect the diff, run the milestone checks, and stage only relevant files. Do not squash these commits later.

| # | Deliverable | Required validation | Commit message |
|---:|---|---|---|
| 0 | Commit Guide/Prompt, MIT license, notices, clean-room declaration | docs reviewed; secret/company-name scan | `docs: define document lifecycle portfolio scope` |
| 1 | .NET solution, layers, central packages, SDK/tool pin, npm/AdminLTE pipeline | restore and build | `chore: scaffold document lifecycle solution` |
| 2 | Identity, policies, seeded demo users, login/logout, AdminLTE shell | auth tests and build | `feat(auth): add role-based demo sign in` |
| 3 | Workspace cookie, scoping, activity tracking, reset service | isolation/reset tests | `feat(demo): isolate and reset browser workspaces` |
| 4 | Domain entities, DbContext mappings, migration, seed service | domain/persistence tests | `feat(data): add document domain and demo seed` |
| 5 | Dashboard and responsive navigation | controller/view tests and build | `feat(dashboard): show lifecycle portfolio metrics` |
| 6 | Category and owner configuration | authorization and CRUD tests | `feat(settings): manage document reference data` |
| 7 | Document list, filters, create/edit/detail, activation | workflow tests | `feat(documents): implement core document workflow` |
| 8 | Secure revisions, upload validation, protected download | upload/security tests | `feat(files): add protected document revisions` |
| 9 | expiry derivation, notifications, read actions | boundary/idempotency tests | `feat(lifecycle): add expiry alerts and notifications` |
| 10 | archive/restore and audit views | transition/audit tests | `feat(audit): add archive workflow and activity history` |
| 11 | filtered XLSX export and PDF summary | export tests and file inspection | `feat(reports): export document lifecycle results` |
| 12 | hardening, error pages, health, logging, accessibility pass | full build/test and dependency audit | `test: harden document lifecycle application` |
| 13 | final README, setup, screenshots, manual publish instructions | clean clone rehearsal and Release publish | `docs: complete document lifecycle project handoff` |

If a milestone is too large, split it into two coherent, passing commits. Never combine later milestones into one large final commit. Never commit failing code merely to create more history.

## 17. Quality gates

Run from the repository root as applicable:

```powershell
dotnet tool restore
dotnet restore --locked-mode
npm ci
npm audit --omit=dev
dotnet format --verify-no-changes
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
dotnet list package --vulnerable --include-transitive
dotnet list package --deprecated
```

Commit `packages.lock.json` files and use locked restore after the first resolved restore. A vulnerability or deprecation result must be assessed and recorded; do not blindly upgrade outside the compatible .NET 8 line. Ensure the browser console is free of application errors and verify the core workflow at desktop and mobile widths.

## 18. Manual publish preparation

No Docker configuration is allowed. Add a manual hosting document that covers:

- framework-dependent and self-contained Release publish commands;
- required environment variables/user secrets and Data Protection key persistence;
- writable upload directory outside the public web root;
- MySQL database/user creation and migration bundle or reviewed migration command;
- HTTPS/reverse-proxy headers, process restart, logs, health check, backup, and rollback;
- demo-mode warning and six-hour cleanup behavior.

Do not deploy, buy hosting, create cloud resources, or push Git changes as part of this build. Hosting will be selected after the application is complete.

## 19. Definition of done

The project is complete only when:

1. A clean clone restores, builds, and passes all normal tests with documented prerequisites.
2. The application can use the configured MySQL database and its committed migrations.
3. All three demo roles can sign in using README credentials and authorization is enforced server-side.
4. A recruiter can complete: create draft, upload revision, activate, find expiry status, add revision, archive/restore, inspect audit history, and export results.
5. Separate browsers cannot see or modify one another's data, and six-hour inactivity reset is tested.
6. Upload and download controls pass the required security tests.
7. UI and documentation are English, responsive, accessible at a practical baseline, and contain the demo/reset notice.
8. MIT, third-party notices, clean-room declaration, dependency locks, setup, test, and manual publish documentation are present.
9. No Docker files, secrets, company references, copied assets, generated uploads, build output, or confidential data exist anywhere in Git history.
10. Git history contains the focused milestone commits above, the working tree is clean, and nothing has been pushed automatically.
