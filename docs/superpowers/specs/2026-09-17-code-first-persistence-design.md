# Code-First Persistence Architecture Design

## Objective

Convert `sep_schema.sql`, which was generated from the approved ERD, into the
authoritative EF Core code-first model for the backend. A new MySQL 8.0 database
must be creatable by applying the repository migrations in order. The SQL file
is a reference input only and is not applied to the database before EF Core.

This phase completes the persistence foundation. It does not generate CRUD
DTOs, repositories, services, or controllers for every table. Application
features will add those abstractions only when a concrete use case needs them,
starting with the Exam Matrix flow.

## Technology and Source of Truth

- Runtime: .NET 8.
- Database: MySQL 8.0 (8.0.16+ for enforced `CHECK` constraints), InnoDB,
  `utf8mb4`.
- ORM/provider: EF Core 8 with `Pomelo.EntityFrameworkCore.MySql` 8.x.
- CLI: repository-local `dotnet-ef` 8.x tool manifest. The installed global
  `dotnet-ef` 10.0.8 must not be used for this project.
- Source of truth after this work: Domain entities, Fluent Configurations, and
  EF Core migrations checked into the backend repository.
- Reference source: `D:\downloads\sep_schema.sql`.
- Migration target: an empty MySQL database. No baseline, existence checks, or
  reuse of pre-created tables is required.

## Clean Architecture Boundaries

The existing four projects remain in place.

```text
Domain <- Application <- Infrastructure
            ^                 ^
            +----- WebAPI ----+
```

### Domain

`Domain` contains plain C# entities, scalar properties, and navigation
properties. It has no EF Core reference or persistence attributes.

Entities are grouped by business area rather than placed in one flat folder:

- `Organization`: School, SchoolBranch, SchoolClass, Room.
- `Academic`: AcademicYear, Semester, GradeLevel, Subject, Textbook,
  TextbookChapter, TextbookLesson, AcademicContext.
- `Identity`: User, Student, Teacher, Role, Module, Navbar, Permission,
  UserRole.
- `QuestionBank`: WorkTask, ExamMatrix, MatrixDetail, QuestionTask,
  QuestionTaskDetail, QuestionBank, Question, QuestionOption, ExamSet,
  ExamSetQuestion, ExamVariant, ExamVariantQuestion.
- `Examination`: Exam, ExamSubject, ExamSubjectGradeLevel, ExamSession,
  ExamRoom, SessionRoom, ExamRegistration, ExamProctor, ProctorAssignment,
  ExamAttempt, ExamAttemptAnswer, TechnicalIncident, Violation.
- `Notifications`: NotificationConfig, NotificationTarget, Notification,
  NotificationRecipient.

These groups contain exactly 49 entity types. C# names use PascalCase. Names
that collide with common framework or language concepts use unambiguous domain
names, for example `SchoolClass` maps to `classes` and `WorkTask` maps to
`tasks`.

### Application

`Application` stays free of EF Core and persistence implementations. It keeps
the existing `ApiResponse<T>` response wrapper for future API use. No generic
repository, generic Unit of Work, placeholder DbContext interface, or bulk CRUD
abstractions are introduced.

DTOs, ports, services, and use-case-specific repository abstractions will be
added when implementing business flows, beginning with Exam Matrix.

### Infrastructure

`Infrastructure` owns:

```text
Infrastructure/
|-- DependencyInjection.cs
|-- Context/
|   +-- ApplicationDbContext.cs
|-- Configurations/
|   |-- Organization/
|   |-- Academic/
|   |-- Identity/
|   |-- QuestionBank/
|   |-- Examination/
|   +-- Notifications/
+-- Migrations/
```

`Context`, `Configurations`, and `Migrations` are direct Infrastructure
folders, matching the reference repository. No additional `Persistence`
folder or project is introduced. There is one focused
`IEntityTypeConfiguration<T>` per entity.
`ApplicationDbContext` exposes 49 `DbSet<T>` properties and calls
`ApplyConfigurationsFromAssembly`. Infrastructure registers the context with a
fixed `MySqlServerVersion(new Version(8, 0, 0))` and a connection string passed
from the host.

### WebAPI

`WebAPI` is the composition root. It reads
`ConnectionStrings:DefaultConnection`, fails fast with a clear error when the
value is absent, and calls the Infrastructure registration extension. Passwords
and other secrets are supplied through environment configuration or user
secrets and are never committed.

The sample Student controller and weather forecast endpoint are removed. No
replacement API endpoint is added in this persistence-only phase.

## Placeholder Removal

The following scaffolding does not represent the approved schema and will be
removed or replaced:

- The sample `Student` entity that uses `Guid` and unrelated fields.
- `StudentDto`, empty student service, empty student repository, and their
  interfaces.
- The fake `IApplicationDbContext` and fake Unit of Work.
- `StudentsController` and the weather forecast endpoint.
- Stale duplicate or incorrectly named repository interface files.

## Relational Mapping Rules

The EF model reproduces the approved relational contract for all 49 tables:

- Database tables, columns, indexes, keys, foreign keys, and constraints retain
  the `snake_case` names from the SQL reference.
- `BIGINT UNSIGNED` maps to `ulong`; nullable values map to `ulong?`.
- `INT UNSIGNED` maps to `uint`; nullable values map to `uint?`.
- `DATE` maps to `DateOnly`/`DateOnly?`.
- `DATETIME(6)` maps to `DateTime`/`DateTime?` with precision 6.
- `DECIMAL(5,2)` maps to `decimal`/`decimal?` with precision 5 and scale 2.
- MySQL `BOOLEAN` maps to `bool`/`bool?`.
- `JSON` remains a string-backed JSON column in this phase because no domain
  JSON shape has been approved.
- `TEXT` and `LONGTEXT`, `VARCHAR` lengths, nullability, defaults, and comments
  follow the SQL reference.
- Auto-increment is configured for every surrogate unsigned bigint primary key.
- Composite primary keys, alternate keys, composite foreign keys, unique
  indexes, ordinary indexes, and delete behavior reproduce the reference.
- `ON UPDATE RESTRICT` is not modeled as application behavior; MySQL's generated
  constraints retain non-cascading key updates.
- Status and type fields remain bounded strings. Domain enums are deferred until
  a use case defines their lifecycle and compatibility requirements.

Navigation collections are initialized, and relationship configuration remains
in Infrastructure. Domain entities do not contain EF attributes.

## Row-Level Integrity Rules

The 20 checks already present in the SQL reference are retained unchanged.
All 11 reference triggers are also retained. Their rules include cross-row
validation and invariants that are intentionally represented as trigger-level
objects rather than row-local `CHECK` constraints. The trigger set therefore
remains authoritative for approval pairing, self-reference prevention,
result-publication pairing, primary/backup set separation, notification target
shape, and notification-config inheritance on MySQL 8.0.

## Migration Design

### InitialCreate

`InitialCreate` is generated from the EF model and creates all 49 tables,
ordinary fields, primary keys, alternate keys, foreign keys, unique and ordinary
indexes, defaults, comments, delete behavior, and row-level checks.

It does not create views, triggers, or generated columns.

### AddDatabaseIntegrityObjects

The second migration uses explicit MySQL SQL for persistence-only objects that
cannot be represented adequately by ordinary entity properties:

1. Add stored generated column `base_scope_school_key`.
2. Add stored generated column `base_scope_code_key`.
3. Add unique index `uq_notification_configs_base_scope` over those columns.
4. Add all 11 approved triggers from the SQL reference.

The generated columns are EF shadow properties and do not appear in the public
Domain entity. The index enforces uniqueness for system- and school-level base
notification configs despite MySQL's handling of nullable values.

The two notification-config triggers inspect another row and cannot be
guaranteed by a same-row check. The other nine triggers remain as explicit
trigger-level integrity objects from the approved schema. Existing composite
foreign keys continue to guarantee matching school and config code.

EF Core does not allow a nullable property to participate in an alternate key,
so the public model keeps `NotificationConfig.SchoolId` nullable and tracks the
parent navigation through `BaseConfigId`. `InitialCreate` replaces the provider's
single-column navigation FK with the exact three-column database FK from the
reference schema.

`Down` removes objects in this order:

1. Drop all 11 triggers.
2. Drop `uq_notification_configs_base_scope`.
3. Drop `base_scope_code_key`.
4. Drop `base_scope_school_key`.

### Deferred Views

Neither reference view is created now:

- `v_effective_notification_configs`
- `v_user_notification_inbox`

There is no current application use case consuming them. When the Notification
module is implemented, an additional migration and keyless EF entities may be
added if the application design chooses to query these views.

## Dependency Injection and Configuration

`AddInfrastructure(IServiceCollection, IConfiguration)` validates
`ConnectionStrings:DefaultConnection`, registers `ApplicationDbContext` with
Pomelo and MySQL 8.0, and sets the migrations assembly to Infrastructure.

The committed settings file contains no usable credential. Local development
uses environment variables or .NET user secrets. Migration commands specify
Infrastructure as the migration project and WebAPI as the startup project.

## Verification Strategy

An xUnit `Infrastructure.Tests` project verifies the persistence contract.

### Model contract tests

- Assert exactly 49 mapped entity types/tables.
- Assert every mapped table and column uses the expected `snake_case` name.
- Check representative and high-risk mappings for unsigned numeric types,
  lengths, decimal precision, JSON, date/time precision, nullable columns,
  defaults, comments, composite keys, alternate keys, foreign keys, indexes,
  delete behavior, and checks.
- Explicitly verify composite relationships such as branch-to-school and
  notification base-config matching.
- Verify the generated columns are shadow properties rather than Domain
  properties.

### Migration contract tests

- Assert the ordered migration set is `InitialCreate`, followed by
  `AddDatabaseIntegrityObjects`.
- Generate the MySQL migration script and assert it contains 49 table creates.
- Assert the special migration contains exactly two generated columns, the one
  dependent unique index, and all 11 approved triggers.
- Assert both deferred view names are absent.

### Executable database verification

Run the complete migration chain against an empty MySQL 8.0 database, inspect
the resulting schema, and roll back to migration `0`. Migration execution is
only reported successful after both upgrade and rollback pass. Docker is
optional; a locally installed MySQL 8.0 server or a shared MySQL 8.0 instance
may be used for this verification.

The completion gate also requires a clean `dotnet build` and a passing full test
suite.

## Out of Scope

- DTOs, request/response models, business services, repositories, and API
  endpoints for the 49 tables.
- Generic repository and custom Unit of Work abstractions.
- Seed data.
- Authentication and authorization implementation.
- Notification view mappings and notification delivery logic.
- Exam Matrix business behavior beyond persistence mappings.
- Applying or importing `sep_schema.sql` directly.

## Acceptance Criteria

- The solution preserves the four approved Clean Architecture projects and
  dependency direction.
- Domain contains exactly 49 persistence-backed, EF-independent entity classes
  grouped by business area.
- Infrastructure contains one Fluent Configuration per entity and a DbContext
  with 49 DbSets.
- Applying all migrations to an empty MySQL 8.0 database produces the approved
  table schema plus the approved notification integrity objects.
- Rolling all migrations back to `0` succeeds.
- The final database has two generated columns, one dependent unique index, all
  11 approved triggers, and no views.
- No database credential is committed.
- Build and automated tests pass; any inability to execute against MySQL 8.0 is
  reported explicitly as a blocker.
