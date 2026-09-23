# Matrix API (Existing Database) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Each checkpoint is deliberately executed in a separate turn; stop after its verification report and wait for the user's approval. The user explicitly prohibits `git commit` and `git push`, so every checkpoint is local-only.

**Goal:** Expose the complete Exam Matrix HTTP API on top of the existing MySQL/EF Core database while preserving the approved lesson-based workflow, lifecycle, role permissions, and transaction guarantees.

**Architecture:** Keep the four existing Clean Architecture projects. The current EF entities remain the persistence model, with small EF-independent domain behavior added to `Domain.Entities.QuestionBank`. `Application` owns request/response contracts, use-case orchestration, validation, authorization ports, and transaction boundaries; `Infrastructure` owns EF repositories and reference-data queries; `WebAPI` owns authentication integration, RFC 7807 error mapping, controllers, Swagger, and composition. No frontend code is changed by this backend API plan.

**Tech Stack:** .NET 8, ASP.NET Core Web API, EF Core 8.0.31, Pomelo.EntityFrameworkCore.MySql 8.0.3, MySQL 8.0+, xUnit, Swagger/OpenAPI, ClosedXML for `.xlsx` export.

**Spec:** `D:\downloads\2026-09-16-matrix-management.md`; `D:\downloads\Project-Business-Context.md`; Figma sections `FINAL · LOGICAL ERD · SYSTEM ADMIN NOTIFICATION SCOPE Test`, `02 · MA TRẬN TRỰC QUAN`, `ACTIVE — NHÓM 2 · OVERLAY & TRẠNG THÁI`, and `WORKFLOW · TẠO MA TRẬN ĐỀ THI · 2 NHÁNH`; persistence baseline `docs/superpowers/specs/2026-09-17-code-first-persistence-design.md`.

## Global Constraints

- The existing migration chain is `20260917101842_InitialCreate` followed by `20260917102102_AddDatabaseIntegrityObjects`; never rewrite or delete either migration.
- The current repository model already maps 49 tables, including `exam_matrices`, `matrix_details`, and `tasks`. Add a new migration after the current latest migration for feature-required differences; do not rerun `InitialCreate` against the existing database.
- Database credentials are supplied only through `ConnectionStrings__DefaultConnection`, user secrets, or the deployment secret store. Do not put a usable credential in `appsettings.json`, tests, OpenAPI, or source control.
- Matrix lifecycle is exactly `DRAFT ⇄ SUBMITTED → APPROVED → ARCHIVED`; there is no `REJECTED` state and no duration field.
- `totalQuestions` and `totalScore` are calculated from `matrix_details` and are never accepted in a write request or persisted in `exam_matrices`.
- A direct PHT matrix has no task. A delegated Team Lead matrix has one `MATRIX` task and each task can create at most one matrix. Delegated scope is read from the task, never trusted from the request body.
- Detail cells are lesson-based and unique by `(exam_matrix_id, lesson_id, cognitive_level, question_type)`. `question_count > 0` and `allocated_score > 0` are required for every new or replaced cell.
- Permissions follow the agreed table: PHT can create/edit Draft and Submitted, approve Submitted, archive Approved, and clone Approved/Archived; the assigned Team Lead can edit/submit Draft and withdraw their own Submitted matrix before approval. No user can edit Approved/Archived content.
- Direct PHT “Confirm” is one database transaction that performs `DRAFT → SUBMITTED → APPROVED`; the client sees only the final Approved result.
- All writes that replace a header and details are atomic. Transition updates include the expected current status so a concurrent withdrawal/approval cannot both succeed.
- Use role claims supplied by the existing authentication system. A test authentication handler is allowed only inside integration tests; never ship a client-controlled role header as production authorization.
- Every checkpoint ends with targeted tests, `dotnet build` when relevant, `git diff --check`, and `git status --short`. Do not commit, push, reset, or discard unrelated user changes.

## Current baseline and the compatibility decision

The repository currently contains:

- `Infrastructure/Context/ApplicationDbContext.cs` with the 49 existing `DbSet<T>` mappings.
- `Infrastructure/Migrations/20260917101842_InitialCreate.*` and `Infrastructure/Migrations/20260917102102_AddDatabaseIntegrityObjects.*`.
- `Domain/Entities/QuestionBank/ExamMatrix.cs`, `MatrixDetail.cs`, and `WorkTask.cs` as persistence POCOs.
- `Application` containing only the existing `ApiResponse<T>` wrapper; no matrix service or HTTP controller exists.
- `WebAPI/Program.cs` already calls `AddControllers`, `AddInfrastructure`, and Swagger, while `WebAPI/appsettings.json` intentionally has a blank connection-string value.

The current schema differs from the agreed direct/delegated workflow in three material ways: `exam_matrices.task_id` is `NOT NULL`, `tasks` has no structured `academic_context_id` or `semester_id`, and the database check currently permits `allocated_score = 0`. The recommended compatibility decision is to add `AddMatrixFeatureConstraints` after the latest migration:

1. Alter `exam_matrices.task_id` to nullable while keeping `uq_exam_matrices_task` (MySQL permits multiple `NULL` values, so it still enforces one matrix per non-null task).
2. Add nullable `tasks.academic_context_id` and `tasks.semester_id`, their foreign keys, and the task-scope index. Application validation requires both for `task_type = MATRIX`.
3. Replace `ck_matrix_details_score` with the strict `allocated_score > 0` check after the preflight confirms that no existing row has a zero score.
4. Add the composite matrix scope/status index required by list filtering.

If the live database does not have the current EF migration history, or contains invalid rows, the work stops at Checkpoint 0 for an explicit baseline/data-cleanup decision. No API code is written against an unverified schema.

## File map

### Existing files to modify

- `Domain/Entities/QuestionBank/WorkTask.cs`: add nullable structured matrix-task scope fields and navigations.
- `Domain/Entities/QuestionBank/ExamMatrix.cs`: make `TaskId` nullable, add lifecycle/derived-total behavior without EF references.
- `Domain/Entities/QuestionBank/MatrixDetail.cs`: add normalized cell construction/validation helpers without persistence attributes.
- `Infrastructure/Configurations/QuestionBank/WorkTaskConfiguration.cs`: map task scope columns, foreign keys, and indexes.
- `Infrastructure/Configurations/QuestionBank/ExamMatrixConfiguration.cs`: map nullable task link and list indexes.
- `Infrastructure/Configurations/QuestionBank/MatrixDetailConfiguration.cs`: map the strict positive-score check.
- `Infrastructure.Tests/Persistence/QuestionBankModelTests.cs`, `SchemaContractTests.cs`, and `MigrationContractTests.cs`: extend the existing persistence contract tests for the feature migration.
- `Infrastructure/DependencyInjection.cs`: register feature-specific repositories, transaction runner, and reference readers.
- `WebAPI/Program.cs`: register application services, exception/problem-details handling, and authentication integration without storing credentials.

### New backend files

- `Domain/Entities/QuestionBank/MatrixStatus.cs`, `MatrixActor.cs`, `MatrixDetailValue.cs`, `MatrixDomainException.cs`.
- `.config/dotnet-tools.json` if the repository does not already pin `dotnet-ef` 8.x.
- `Infrastructure/Migrations/<timestamp>_AddMatrixFeatureConstraints.cs` and its generated designer/snapshot update.
- `Application/ExamMatrices/Contracts/MatrixContracts.cs`.
- `Application/ExamMatrices/IMatrixApplicationService.cs` and `MatrixApplicationService.cs`.
- `Application/MatrixTasks/IMatrixTaskApplicationService.cs` and `MatrixTaskApplicationService.cs`.
- `Application/Common/Security/IMatrixCurrentUser.cs` and `Application/Common/Errors/MatrixApplicationException.cs`.
- `Infrastructure/Repositories/Matrix/ExamMatrixRepository.cs`, `MatrixTaskRepository.cs`, `MatrixReferenceRepository.cs`, and `MatrixTransaction.cs`.
- `WebAPI/Controllers/MatricesController.cs`, `MatrixTasksController.cs`, and `MatrixReferenceDataController.cs`.
- `WebAPI/Errors/MatrixExceptionHandler.cs` and the test-only authentication handler under `WebAPI.IntegrationTests/Support`.
- `Infrastructure.Tests/Persistence/MatrixApiSchemaTests.cs`, `Domain.Tests/ExamMatrices/ExamMatrixTests.cs`, `Application.Tests/ExamMatrices/MatrixApplicationServiceTests.cs`, and `WebAPI.IntegrationTests/MatricesApiTests.cs`.
- `Application/ExamMatrices/Exports/MatrixWorkbookExporter.cs` and `Infrastructure/Exports/ClosedXmlMatrixWorkbookExporter.cs`.
- `docs/contracts/matrix-api.openapi.yaml` and `docs/runbooks/matrix-api.md`.

---

## Checkpoint 0: Verify the live database and role vocabulary (read-only)

**Files:** none in production code. Record the result in `docs/runbooks/matrix-api-db-audit.md` only after the audit commands succeed.

**Purpose:** Prove which database is running, which EF migrations are applied, whether the existing data can accept the compatibility migration, and which role codes represent PHT and Tổ trưởng. This checkpoint does not change schema or data.

**Commands:**

```powershell
cd "D:\FPT uni\Fall 2026\SEP490\sep-backend\LeTo-Backend"
$env:ConnectionStrings__DefaultConnection = Read-Host "Enter the dedicated matrix database connection string"
dotnet build TeLoSchoolManagement.sln
dotnet test Infrastructure.Tests\Infrastructure.Tests.csproj
dotnet ef migrations list --project Infrastructure --startup-project WebAPI
```

Run the following read-only MySQL queries using the same connection:

```sql
SELECT DATABASE() AS database_name, VERSION() AS mysql_version;

SELECT MigrationId
FROM __EFMigrationsHistory
ORDER BY MigrationId;

SELECT table_name, column_name, is_nullable, column_type
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND (
       (table_name = 'exam_matrices' AND column_name IN ('task_id', 'semester_id', 'academic_context_id'))
    OR (table_name = 'tasks' AND column_name IN ('task_type', 'academic_context_id', 'semester_id'))
    OR (table_name = 'matrix_details' AND column_name IN ('question_count', 'allocated_score'))
  )
ORDER BY table_name, ordinal_position;

SELECT table_name, index_name, non_unique, GROUP_CONCAT(column_name ORDER BY seq_in_index) AS columns_in_order
FROM information_schema.statistics
WHERE table_schema = DATABASE()
  AND table_name IN ('exam_matrices', 'matrix_details', 'tasks')
GROUP BY table_name, index_name, non_unique
ORDER BY table_name, index_name;

SELECT COUNT(*) AS zero_score_rows
FROM matrix_details
WHERE allocated_score <= 0;

SELECT task_id, COUNT(*) AS matrix_count
FROM exam_matrices
WHERE task_id IS NOT NULL
GROUP BY task_id
HAVING COUNT(*) > 1;

SELECT task_type, COUNT(*) AS task_count
FROM tasks
GROUP BY task_type
ORDER BY task_type;

SELECT id, code, name
FROM roles
ORDER BY id;
```

**Expected gate:** the migration history contains the two current baseline IDs in order; all three matrix tables exist; there are no zero/negative scores or duplicate non-null task links; and the role table yields the exact codes to configure in the application. If the database is unreachable, has no matching EF history, or has invalid rows, stop and report the exact query result. Do not apply `InitialCreate` or edit data to make the migration pass.

**Checkpoint evidence:** database/version, applied migration IDs, schema differences, invalid-row counts, role-code mapping, and the decision that Checkpoint 1 can proceed. Then pause.

---

## Checkpoint 1: Align EF model and live schema for the matrix workflow

**Files:**

- Modify: `Domain/Entities/QuestionBank/WorkTask.cs`, `ExamMatrix.cs`, `MatrixDetail.cs`.
- Modify: `Infrastructure/Configurations/QuestionBank/WorkTaskConfiguration.cs`, `ExamMatrixConfiguration.cs`, `MatrixDetailConfiguration.cs`.
- Modify: existing `Infrastructure.Tests/Persistence/QuestionBankModelTests.cs`, `SchemaContractTests.cs`, `MigrationContractTests.cs`.
- Create: `Infrastructure.Tests/Persistence/MatrixApiSchemaTests.cs`.
- Create: `.config/dotnet-tools.json` only when `dotnet ef --version` is not an EF Core 8 tool.
- Create: `Infrastructure/Migrations/<timestamp>_AddMatrixFeatureConstraints.cs` and generated designer; update `ApplicationDbContextModelSnapshot.cs`.

**Interfaces produced:** EF metadata exposes `WorkTask.AcademicContextId: ulong?`, `WorkTask.SemesterId: ulong?`, and `ExamMatrix.TaskId: ulong?`; the database migration adds the same columns/constraints without touching the two existing migrations.

- [ ] **Step 1: Add failing model/migration contract tests.** Assert `ExamMatrix.TaskId` is nullable; task scope columns map to `academic_context_id` and `semester_id`; both task FKs use restrict delete; the matrix scope/status index has columns `(academic_context_id, semester_id, status)`; the existing unique cell index remains; and the score check SQL is `allocated_score > 0`.

```csharp
var matrix = ModelFactory.CreateModel().FindEntityType(typeof(ExamMatrix))!;
Assert.True(matrix.FindProperty(nameof(ExamMatrix.TaskId))!.IsNullable);

var task = ModelFactory.CreateModel().FindEntityType(typeof(WorkTask))!;
Assert.Equal("academic_context_id", task.FindProperty(nameof(WorkTask.AcademicContextId))!.GetColumnName());
Assert.Equal("semester_id", task.FindProperty(nameof(WorkTask.SemesterId))!.GetColumnName());

var detail = ModelFactory.CreateDesignModel().FindEntityType(typeof(MatrixDetail))!;
Assert.Equal("allocated_score > 0", detail.GetCheckConstraints()
    .Single(x => x.Name == "ck_matrix_details_score").Sql);
```

- [ ] **Step 2: Run the focused tests and verify RED.**

```powershell
dotnet test Infrastructure.Tests\Infrastructure.Tests.csproj --filter "FullyQualifiedName~MatrixApiSchemaTests|FullyQualifiedName~QuestionBankModelTests"
```

Expected: failure because the current model still has a required task link, no structured task scope, and the non-strict score check.

- [ ] **Step 3: Implement the model changes.** Make `ExamMatrix.TaskId` and `Task` nullable; add task scope properties/navigations; configure the nullable FK and the composite list index; change only the score check expression; preserve the existing unique cell index and delete behavior.

- [ ] **Step 4: Generate the feature migration.** If needed, pin and restore the local EF 8 tool, then run:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations add AddMatrixFeatureConstraints --project Infrastructure --startup-project WebAPI --output-dir Migrations
```

Review that the migration alters `exam_matrices.task_id`, adds two task columns/FKs/indexes, replaces the score check, and creates the composite matrix index. It must not recreate any table, trigger, or view.

- [ ] **Step 5: Apply only this migration to the dedicated database from Checkpoint 0.**

```powershell
dotnet tool run dotnet-ef database update --project Infrastructure --startup-project WebAPI
```

- [ ] **Step 6: Verify GREEN and the live schema.**

```powershell
dotnet test Infrastructure.Tests\Infrastructure.Tests.csproj
dotnet build TeLoSchoolManagement.sln --no-restore
git diff --check
git status --short
```

Re-run the Checkpoint 0 `information_schema` queries and confirm the new migration ID is last. Pause with the migration SQL and test output.

---

## Checkpoint 2: Implement and unit-test domain lifecycle rules

**Files:**

- Modify: `Domain/Entities/QuestionBank/ExamMatrix.cs`, `MatrixDetail.cs`.
- Create: `Domain/Entities/QuestionBank/MatrixStatus.cs`, `MatrixActor.cs`, `MatrixDetailValue.cs`, `MatrixDomainException.cs`.
- Create: `Domain.Tests/Domain.Tests.csproj`, `Domain.Tests/ExamMatrices/ExamMatrixTests.cs`; add the project to `TeLoSchoolManagement.sln`.

**Interfaces produced:**

```csharp
public static class MatrixStatusCodes
{
    public const string Draft = "DRAFT";
    public const string Submitted = "SUBMITTED";
    public const string Approved = "APPROVED";
    public const string Archived = "ARCHIVED";
}

public enum MatrixActorRole { Pht, TeamLead }
public readonly record struct MatrixActor(ulong UserId, MatrixActorRole Role);
public sealed record MatrixDetailValue(
    ulong LessonId,
    string CognitiveLevel,
    string QuestionType,
    uint QuestionCount,
    decimal AllocatedScore);
```

`ExamMatrix` exposes `CanEdit(MatrixActor)`, `CanHardDelete(MatrixActor)`, `ReplaceDetails(IEnumerable<MatrixDetailValue>, MatrixActor)`, `Submit(MatrixActor)`, `Withdraw(MatrixActor)`, `Approve(MatrixActor)`, `ConfirmDirect(MatrixActor)`, `Archive(MatrixActor)`, `CloneAsDraft()`, and read-only `TotalQuestions`/`TotalScore` calculations. Methods mutate only the aggregate and never call EF or a database.

- [ ] **Step 1: Write failing tests for the exact state/role table.** Cover PHT direct Draft edit/delete/submit/confirm, PHT Submitted edit/approve, assigned Team Lead Draft edit/submit, assigned Team Lead Submitted withdraw, forbidden cross-task actions, Approved/Archived immutability, archive requiring Approved, clone preserving detail values but clearing task/status, and direct confirm ending in Approved.

- [ ] **Step 2: Add failing validation tests.** Reject empty names, unsupported cognitive levels, blank/overlong question types, duplicate composite cells (case-insensitive after normalization), `question_count == 0`, `allocated_score <= 0`, and an empty detail collection when the agreed UI requires at least one cell before submit/confirm.

- [ ] **Step 3: Run the domain tests and verify RED.**

```powershell
dotnet test Domain.Tests\Domain.Tests.csproj
```

- [ ] **Step 4: Implement the minimal domain behavior.** Keep status persisted as the existing bounded string; use the four constants for comparisons; normalize cognitive/question-type values once at the aggregate boundary; throw `MatrixDomainException` with stable error codes; keep totals derived.

- [ ] **Step 5: Verify GREEN.**

```powershell
dotnet test Domain.Tests\Domain.Tests.csproj
dotnet build TeLoSchoolManagement.sln --no-restore
git diff --check
git status --short
```

Pause with the state-transition test report before adding persistence or controllers.

---

## Checkpoint 3: Freeze application contracts, validation, authorization, and use cases

**Files:**

- Create: `Application/Common/Security/IMatrixCurrentUser.cs`, `Application/Common/Errors/MatrixApplicationException.cs`.
- Create: `Application/ExamMatrices/Contracts/MatrixContracts.cs`, `IMatrixApplicationService.cs`, `MatrixApplicationService.cs`.
- Create: `Application/MatrixTasks/IMatrixTaskApplicationService.cs`, `MatrixTaskApplicationService.cs`.
- Create: `Application.Tests/Application.Tests.csproj`, `Application.Tests/ExamMatrices/MatrixApplicationServiceTests.cs`; add the project to the solution.

**Interfaces produced:**

```csharp
public sealed record MatrixListFilter(
    ulong? AcademicContextId,
    ulong? SemesterId,
    string? Status,
    string? Keyword,
    int Page = 1,
    int PageSize = 20);

public sealed record MatrixDetailRequest(
    ulong LessonId,
    string CognitiveLevel,
    string QuestionType,
    uint QuestionCount,
    decimal AllocatedScore);

public sealed record SaveMatrixRequest(
    string Name,
    ulong? AcademicContextId,
    ulong? SemesterId,
    ulong? TaskId,
    IReadOnlyList<MatrixDetailRequest> Details);

public enum MatrixAction
{
    Edit, Delete, Submit, Withdraw, Approve, Confirm, Archive, Clone, Export
}

public interface IMatrixApplicationService
{
    Task<PagedResult<MatrixListItem>> ListAsync(MatrixListFilter filter, CancellationToken ct);
    Task<MatrixResponse> GetAsync(ulong matrixId, CancellationToken ct);
    Task<MatrixResponse> CreateAsync(SaveMatrixRequest request, CancellationToken ct);
    Task<MatrixResponse> UpdateAsync(ulong matrixId, SaveMatrixRequest request, CancellationToken ct);
    Task DeleteDraftAsync(ulong matrixId, CancellationToken ct);
    Task<MatrixResponse> SubmitAsync(ulong matrixId, CancellationToken ct);
    Task<MatrixResponse> WithdrawAsync(ulong matrixId, CancellationToken ct);
    Task<MatrixResponse> ApproveAsync(ulong matrixId, CancellationToken ct);
    Task<MatrixResponse> ConfirmDirectAsync(ulong matrixId, CancellationToken ct);
    Task<MatrixResponse> ArchiveAsync(ulong matrixId, CancellationToken ct);
    Task<MatrixResponse> CloneAsync(ulong matrixId, CancellationToken ct);
}
```

Define `MatrixResponse` with `id`, `name`, `status`, nullable `taskId`, `academicContext`, nullable `semester`, `details`, calculated `totalQuestions`, calculated `totalScore`, and server-generated `allowedActions`. Define `PagedResult<T>` with `items`, `page`, `pageSize`, and `totalCount`. No write contract contains either total.

- [ ] **Step 1: Write failing service tests using in-memory fake ports.** Cover direct PHT creation with `taskId = null`, delegated creation resolving context/semester from the task, rejection of client scope different from task scope, one-matrix-per-task conflict, lesson not belonging to the context textbook, invalid semester/year pairing, PHT/Team Lead authorization, and every transition.

- [ ] **Step 2: Define feature-specific ports without EF references.** Use `IExamMatrixStore`, `IMatrixTaskStore`, `IMatrixReferenceReader`, `IMatrixTransaction`, and `IMatrixCurrentUser`. Ports must expose only matrix use cases; do not introduce a generic repository or generic unit of work.

- [ ] **Step 3: Implement validation and orchestration.** Validate page bounds and request fields; resolve a delegated task first; query lesson/context/semester relationships through the reference reader; map requests to `MatrixDetailValue`; call domain methods; calculate `allowedActions` from the current actor and aggregate; convert domain errors to stable application error codes.

- [ ] **Step 4: Implement transaction/concurrency rules in the application boundary.** Create/update/clone/delete and direct confirm run within one transaction. Transition operations load the aggregate with a status expectation and return a conflict when the expected status no longer matches.

- [ ] **Step 5: Verify GREEN.**

```powershell
dotnet test Application.Tests\Application.Tests.csproj
dotnet test Domain.Tests\Domain.Tests.csproj
dotnet build TeLoSchoolManagement.sln --no-restore
git diff --check
git status --short
```

Pause with the frozen DTO/port signatures and service test output.

---

## Checkpoint 4: Implement EF repositories, projections, and transaction handling

**Files:**

- Create: `Infrastructure/Repositories/Matrix/ExamMatrixRepository.cs`, `MatrixTaskRepository.cs`, `MatrixReferenceRepository.cs`, `MatrixTransaction.cs`.
- Modify: `Infrastructure/DependencyInjection.cs`.
- Create: `Infrastructure.Tests/Persistence/MatrixRepositoryTests.cs` and a dedicated `MatrixDatabaseFixture` that reads `ConnectionStrings__MatrixTest` without logging it.

**Interfaces consumed:** the ports from Checkpoint 3 and the 49-table `ApplicationDbContext` from the existing persistence baseline.

- [ ] **Step 1: Write failing repository integration tests.** Against a dedicated schema, assert list filters/paging, detail eager loading, direct null task link, delegated task scope, lesson scope resolution, one-task uniqueness, cascade deletion of Draft details, and status transition conflict behavior. Tests must fail clearly when `ConnectionStrings__MatrixTest` is absent; never silently use a developer's production database.

- [ ] **Step 2: Implement read queries.** Use `AsNoTracking` projections for list/detail, include only the context/subject/grade/textbook/semester/task data needed by the response, and order details by lesson sort order then cognitive/question type. Default list excludes `ARCHIVED` unless the caller explicitly asks for it.

- [ ] **Step 3: Implement transactional writes.** Use `ApplicationDbContext.Database.BeginTransactionAsync`; replace all details only after validation; rely on the database unique cell/task indexes for the final race check; translate `DbUpdateException` for those indexes to application conflicts; commit only after `SaveChangesAsync` succeeds.

- [ ] **Step 4: Implement reference readers.** Verify semester belongs to the academic context's academic year and each lesson belongs to the context's textbook through `textbook_chapters`; query eligible Team Leads through `users`, `teachers`, `user_roles`, and `roles` in the same school branch.

- [ ] **Step 5: Register only feature-specific services.** `AddInfrastructure` keeps its fail-fast blank-connection behavior and registers the matrix stores/readers/transaction. Do not add a generic repository or expose `ApplicationDbContext` to `Application`.

- [ ] **Step 6: Verify against the dedicated database.**

```powershell
$env:ConnectionStrings__MatrixTest = Read-Host "Enter the dedicated matrix test database connection string"
dotnet test Infrastructure.Tests\Infrastructure.Tests.csproj --filter "FullyQualifiedName~MatrixRepositoryTests"
dotnet test Application.Tests\Application.Tests.csproj
dotnet build TeLoSchoolManagement.sln --no-restore
git diff --check
git status --short
```

Pause with query and transaction test evidence.

---

## Checkpoint 5: Expose matrix CRUD and lifecycle HTTP endpoints

**Files:**

- Create: `WebAPI/Controllers/MatricesController.cs`, `WebAPI/Errors/MatrixExceptionHandler.cs`.
- Modify: `WebAPI/Program.cs`.
- Create: `docs/contracts/matrix-api.openapi.yaml`.
- Create: `WebAPI.IntegrationTests/WebAPI.IntegrationTests.csproj`, `WebAPI.IntegrationTests/Support/TestAuthHandler.cs`, and `WebAPI.IntegrationTests/MatricesApiTests.cs`; add the project to the solution.

**Endpoint contract:**

| Method | Route | Success | Behavior |
|---|---|---:|---|
| GET | `/api/matrices` | 200 | Paged list; filters `academicContextId`, `semesterId`, `status`, `keyword`, `page`, `pageSize`; default hides Archived. |
| GET | `/api/matrices/{id}` | 200 | Header, details, derived totals, and `allowedActions`. |
| POST | `/api/matrices` | 201 | Direct PHT Draft or assigned-Team-Lead Draft. |
| PUT | `/api/matrices/{id}` | 200 | Atomic header/detail replacement when the actor may edit. |
| DELETE | `/api/matrices/{id}` | 204 | Hard-delete Draft only. |
| POST | `/api/matrices/{id}/submit` | 200 | `DRAFT → SUBMITTED`. |
| POST | `/api/matrices/{id}/withdraw` | 200 | Assigned Team Lead: `SUBMITTED → DRAFT`. |
| POST | `/api/matrices/{id}/approve` | 200 | PHT: `SUBMITTED → APPROVED`. |
| POST | `/api/matrices/{id}/confirm` | 200 | Direct PHT only; atomic `DRAFT → SUBMITTED → APPROVED`. |
| POST | `/api/matrices/{id}/archive` | 200 | PHT: `APPROVED → ARCHIVED`. |
| POST | `/api/matrices/{id}/clone` | 201 | PHT clones Approved/Archived header/details into an unlinked Draft. |

- [ ] **Step 1: Write failing integration tests.** Use `WebApplicationFactory<Program>` with a test authentication handler that injects a user id/role claim. Assert 401 without identity, 403 for wrong role/owner, 404 for unknown id, 409 for state/task races, 422 for detail validation, 201/200/204 success codes, and that response totals are derived.

- [ ] **Step 2: Implement consistent ProblemDetails mapping.** Map malformed JSON/model binding to 400, missing identity to 401, permission failures to 403, missing records to 404, domain/request-cell validation to 422, and unique/concurrency conflicts to 409. Include a stable `code` and field-level `errors`; never expose SQL or stack traces.

- [ ] **Step 3: Implement the controller as a thin adapter.** Bind query/body/route values, pass the authenticated actor through `IMatrixCurrentUser`, return `ApiResponse<T>` only where the existing API convention requires it, and keep OpenAPI response schemas identical to the actual JSON.

- [ ] **Step 4: Configure authentication integration.** Read the existing claims (`NameIdentifier` and role claims) in production; fail closed when claims are absent. Keep the test handler in the integration-test project only.

- [ ] **Step 5: Write the OpenAPI document.** Set `servers: [{ url: /api }]`, define the request/response/error schemas, state/status/action enums, nullable task/semester fields, and examples for direct and delegated creation. The document must explicitly say totals are response-only and delegated scope is server-resolved.

- [ ] **Step 6: Verify GREEN and manually inspect Swagger.**

```powershell
dotnet test WebAPI.IntegrationTests\WebAPI.IntegrationTests.csproj
dotnet test TeLoSchoolManagement.sln
dotnet build TeLoSchoolManagement.sln --no-restore
dotnet run --project WebAPI\WebAPI.csproj
```

Open `https://localhost:7033/swagger` and verify the matrix routes match `docs/contracts/matrix-api.openapi.yaml`. Pause before adding tasks/export.

---

## Checkpoint 6: Add matrix task assignment, reference data, and Excel export

**Files:**

- Create: `WebAPI/Controllers/MatrixTasksController.cs`, `MatrixReferenceDataController.cs`.
- Create: `Application/MatrixTasks/Contracts/MatrixTaskContracts.cs`, `Application/ExamMatrices/Exports/MatrixWorkbookExporter.cs`, `Infrastructure/Exports/ClosedXmlMatrixWorkbookExporter.cs`.
- Modify: `Application/MatrixTasks/MatrixTaskApplicationService.cs`, `WebAPI/Controllers/MatricesController.cs`, `Application/Application.csproj`, and `Infrastructure/Infrastructure.csproj` to add the pinned ClosedXML package where the implementation is placed.
- Modify: `docs/contracts/matrix-api.openapi.yaml` and create `docs/runbooks/matrix-api.md`.
- Extend: `WebAPI.IntegrationTests/MatricesApiTests.cs` with task/reference/export scenarios.

**Endpoint contract:**

| Method | Route | Success | Rules |
|---|---|---:|---|
| POST | `/api/matrix-tasks` | 201 | PHT creates a `MATRIX` task with assignee, due date, context, semester, and description. |
| GET | `/api/matrix-tasks` | 200 | PHT task list with status/assignee/due filters. |
| GET | `/api/my/matrix-tasks` | 200 | Current Team Lead's assigned task list. |
| GET | `/api/matrix-tasks/{id}` | 200 | Task scope and linked matrix, if one exists. |
| GET | `/api/matrix-reference-data` | 200 | Contexts, semesters, lessons, and eligible Team Leads for selectors. |
| GET | `/api/matrices/{id}/export.xlsx` | 200 | Attachment containing matrix scope, details, totals, and a safe filename. |

- [ ] **Step 1: Write failing task/reference/export tests.** Assert only PHT can assign; assignee is an eligible Team Lead in the same branch; task scope fields are required; a task cannot link a second matrix; Team Lead receives only their tasks; reference lessons are limited to the context textbook; and export contains every saved cell/totals.

- [ ] **Step 2: Implement task application flow.** Persist structured context/semester fields, initialize `task_type = MATRIX`, set the agreed task status, and expose the linked matrix id. Do not encode scope in `description`.

- [ ] **Step 3: Implement reference-data query.** Return stable selector records (`id`, `code/title`, display name, relationship ids) and exclude inactive or out-of-branch records according to the current user.

- [ ] **Step 4: Implement export.** Add the pinned ClosedXML version, create one workbook with a summary section and detail rows, return `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`, generate a filename from a sanitized matrix name, and prefix cells that begin with `=`, `+`, `-`, or `@` so user-entered names cannot become formulas.

- [ ] **Step 5: Verify GREEN and manual downloads.**

```powershell
dotnet test WebAPI.IntegrationTests\WebAPI.IntegrationTests.csproj
dotnet test TeLoSchoolManagement.sln
dotnet build TeLoSchoolManagement.sln --no-restore
git diff --check
git status --short
```

Use Swagger/Postman to create one direct Draft and one delegated task/matrix, download both exports, and record the response headers in the runbook. Pause.

---

## Checkpoint 7: End-to-end database verification and handoff

**Files:**

- Create/modify only: `docs/runbooks/matrix-api.md`, test fixtures, and files required to fix a verified failure. Do not modify unrelated modules.

- [ ] **Step 1: Run the complete local quality gate.**

```powershell
cd "D:\FPT uni\Fall 2026\SEP490\sep-backend\LeTo-Backend"
dotnet tool restore
dotnet restore TeLoSchoolManagement.sln
dotnet build TeLoSchoolManagement.sln --no-restore
dotnet test TeLoSchoolManagement.sln --no-build
git diff --check
git status --short
```

- [ ] **Step 2: Apply the latest migration to the dedicated existing database.** Confirm `__EFMigrationsHistory` contains the baseline two migrations plus `AddMatrixFeatureConstraints`; do not apply the migration to an unknown shared database without the owner's approval.

- [ ] **Step 3: Execute the business smoke matrix.** Verify direct PHT Draft → Confirm → Approved → Archive → Clone; delegated PHT task → Team Lead Draft → Submit → Withdraw → Resubmit → PHT edit → Approve; Draft delete; duplicate-cell rejection; wrong-role 403; invalid-state 409; and Excel 200/download.

- [ ] **Step 4: Verify the frontend contract handoff.** Point `VITE_API_URL` at the WebAPI base URL only after the OpenAPI document and Swagger responses match. Frontend mock tests are not evidence that the database API is connected.

- [ ] **Step 5: Final local report.** List changed files, migration id, test/build output, endpoint smoke results, known limitations (especially the authentication provider if it is still supplied by another module), and the exact next checkpoint. Leave all changes uncommitted and wait for user direction.

## Checkpoint protocol

Only one checkpoint is executed per user turn. After each checkpoint, report:

1. Files changed and why.
2. Commands run and their actual result.
3. Database/data impact and rollback status.
4. API contract or permission decisions exposed to the next checkpoint.
5. The next checkpoint, then stop.

The immediate next action is **Checkpoint 0**, which is read-only. No migration, controller, or application code should be written until its database-history and invalid-data gates pass.
