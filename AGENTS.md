# Repository guidance

## Scope and orientation

This file applies to the entire repository. Read the relevant source and project
README before changing behavior. The root `README.md` describes the intended user
experience; the worker and Service Bus READMEs describe the asynchronous workflow
and its known reliability limitations.

## Solution layout

The solution is `Demo.Archivero.slnx`. Projects target .NET 10 with nullable
reference types and implicit usings enabled.

- `src/Demo.Archivero.Domain`: entities, enums, and domain types.
- `src/Demo.Archivero.Application`: use cases, interfaces, DTOs, and shared settings.
- `src/Demo.Archivero.Backend.Api`: ASP.NET Core API, authentication, endpoints,
  result mapping, and dependency injection.
- `src/Demo.Archivero.Infrastructure.SqlRepository`: EF Core SQL Server context,
  repositories, authentication implementation, and migrations.
- `src/Demo.Archivero.Infrastructure.AzureBlob`: blob storage implementation.
- `src/Demo.Archivero.Infrastructure.OpenXml`: Word document generation.
- `src/Demo.Archivero.Infrastructure.ServiceBus`: independent create/delete queue
  senders and receivers, message contracts, and settlement logic.
- `src/Demo.Archivero.Converter.Worker`: hosted services that consume the queues
  and invoke document creation/deletion. This is not an HTTP service.
- `src/Demo.Archivero.Frontend.Spa`: ASP.NET Core host serving plain HTML, CSS,
  and JavaScript from `wwwroot`.
- `src/Demo.Archivero.UnitTests`: NUnit tests and manual HTTP requests.
- `src/Demo.Archivero.Converter.Client`: separate HTTP client project currently
  outside the solution; inspect its usage before treating it as active integration.

## Build and validation

Run commands from the repository root with the .NET 10 SDK installed:

```powershell
dotnet restore Demo.Archivero.slnx
dotnet build Demo.Archivero.slnx --no-restore
dotnet test src/Demo.Archivero.UnitTests/Demo.Archivero.UnitTests.csproj --no-build
```

The `--no-build` test command assumes the preceding solution build succeeded in
the same configuration. For focused work, use `dotnet test` with `--filter` and
the relevant test name. Add or update meaningful NUnit coverage when changing
application or messaging behavior; follow the existing local test doubles.
Documentation-only changes do not require a build or test run.

Start the required hosts in separate terminals after configuring their services:

```powershell
dotnet run --project src/Demo.Archivero.Backend.Api
dotnet run --project src/Demo.Archivero.Converter.Worker
dotnet run --project src/Demo.Archivero.Frontend.Spa
```

Report what was validated and any environmental blockers. Do not claim that a
successful build or unit test run verifies live SQL Server or Azure integrations.

## Implementation conventions

- Follow the surrounding C# style: four-space indentation, file-scoped namespaces,
  PascalCase types/members, and camelCase parameters/locals.
- Keep use cases in Application and infrastructure implementations behind its
  interfaces. Preserve the existing project dependency direction; keep endpoint
  and host wiring out of Domain.
- Use the existing dependency injection registrations, `ResultDto<T>` error
  conventions, and API result mapping when extending behavior.
- Keep asynchronous I/O asynchronous and propagate cancellation tokens. Use
  structured logging and the existing `IDateTimeProvider` abstraction where used.
- Preserve file ownership checks, authentication, and audit fields. Keep file
  limits aligned with `EntitiesSettings`, EF mappings, and frontend validation.
- Preserve the documented SQL collation `Latin1_General_100_CI_AS`. For schema
  changes, update EF mappings and add a migration with the matching model snapshot;
  do not rewrite historical migrations to implement a new schema change.
- Keep changes focused; avoid unrelated refactors, dependency upgrades, generated
  build outputs, and modifications to local IDE files.

## Frontend behavior

Maintain the existing single-page structure in `wwwroot/index.html`, `styles.css`,
and `app.js`. Preserve responsive layout, the loading overlay during API calls,
token expiration/logout handling, and the navigation/form-reset behavior described
in the root README. Keep API routes and request/response contracts synchronized
with the backend. For UI changes, verify the affected flow in a browser when
available and describe any unverified interactions.

## Messaging and configuration

- Read `src/Demo.Archivero.Infrastructure.ServiceBus/README.md` and
  `src/Demo.Archivero.Converter.Worker/README.md` before changing queue behavior.
- Preserve separate create/delete queues, stable operation message IDs, scoped
  handling per delivery, and explicit completion only after successful processing.
  Account for retries, duplicate deliveries, and partial blob/database failures;
  do not assume cross-queue ordering or exactly-once execution.
- Store credentials in user secrets or environment variables, never source files
  or logs. Environment configuration uses `__` in place of `:`.
- The worker requires `ConnectionStrings:ArchiveroDB`, the `ArchiveroBlobService`
  connection string/container, and the `ArchiveroFileBus` connection string and
  create/delete queue names. Inspect host configuration and settings validation
  for the complete requirements of each executable.
- Provision queues and apply database migrations in the intended development
  environment before running the worker. Do not add automatic provisioning or
  destructive database operations as an incidental part of another change.
