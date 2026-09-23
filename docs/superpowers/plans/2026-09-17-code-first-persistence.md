# Code-First Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. The user explicitly prohibited commits and pushes; every checkpoint remains local.

**Goal:** Replace the backend placeholders with a tested EF Core code-first model whose two migrations create the approved 49-table MySQL 8.0 schema from an empty database.

**Architecture:** Keep the existing Domain, Application, Infrastructure, and WebAPI projects. Domain owns EF-independent entities grouped by business area; Infrastructure owns Pomelo mappings, DbContext, dependency injection, and migrations; WebAPI is the composition root. Build the model in business-area slices, generate `InitialCreate`, then add persistence-only generated columns and all approved triggers in `AddDatabaseIntegrityObjects`.

**Tech Stack:** .NET 8, EF Core 8.0.31, Pomelo.EntityFrameworkCore.MySql 8.0.3, local dotnet-ef 8.0.31, MySQL 8.0, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-17-code-first-persistence-design.md`

## Global Constraints

- Treat `D:\downloads\sep_schema.sql` as the approved ERD reference; never execute it against the target database.
- `InitialCreate` targets an empty MySQL 8.0 database and creates 49 tables.
- C# identifiers use PascalCase; database tables, columns, keys, indexes, and checks retain the reference `snake_case` names.
- Domain must not reference EF Core.
- Do not create generic repositories, a custom Unit of Work, bulk DTOs/services/controllers, seed data, or either reference view.
- Keep all 11 database triggers from `sep_schema.sql`; their cross-row and trigger-level rules are not replaced by row-local `CHECK` constraints.
- Keep the two generated notification columns as EF shadow properties.
- Never commit a credential. `ConnectionStrings:DefaultConnection` must fail fast when absent or blank.
- Do not run `git commit`, `git push`, or otherwise write to a remote. Use `git status --short` as the local review checkpoint.
- Follow red-green-refactor for handwritten production code. Generated migrations are accepted only after a failing migration-contract test exists.

## File Map

### Remove

- `Domain/Entities/Student.cs`
- `Application/DTOs/StudentDto.cs`
- `Application/Interfaces/IApplicationDbContext.cs`
- `Application/Mappings/MappingProfile.cs`
- `Application/Services/Interface/IStudentService.cs`
- `Application/Services/Implement/StudentService.cs`
- `Infrastructure/Repositories/Interfaces/StudentRepository.cs`
- `Infrastructure/Repositories/Interfaces/IStudentRepository.cs`
- `Infrastructure/Repositories/Implementations/StudentRepository.cs`
- `Infrastructure/UnitOfWork/IUnitOfWork.cs`
- `Infrastructure/UnitOfWork/UnitOfWork.cs`
- `Infrastructure/Data/ApplicationDbContext.cs`
- `WebAPI/Controllers/StudentsController.cs`

### Create or replace

- `.config/dotnet-tools.json`: pins local `dotnet-ef` 8.0.31.
- `Domain/Entities/<Area>/*.cs`: 49 POCO entity files.
- `Infrastructure/Context/ApplicationDbContext.cs`: 49 DbSets and assembly configuration discovery.
- `Infrastructure/Configurations/<Area>/*Configuration.cs`: one configuration per entity.
- `Infrastructure/DependencyInjection.cs`: MySQL 8.0 registration and fail-fast connection validation.
- `Infrastructure/Migrations/*_InitialCreate.cs` and snapshot.
- `Infrastructure/Migrations/*_AddDatabaseIntegrityObjects.cs` and updated snapshot.
- `Infrastructure.Tests/Infrastructure.Tests.csproj`: xUnit test project.
- `Infrastructure.Tests/Persistence/ModelFactory.cs`: creates the model without opening a database connection.
- `Infrastructure.Tests/Persistence/*ModelTests.cs`: area and high-risk relational contract tests.
- `Infrastructure.Tests/Persistence/MigrationContractTests.cs`: ordered migration and SQL script tests.
- `WebAPI/Program.cs`: clean composition root with no sample endpoint.
- `WebAPI/appsettings.json`: blank, non-secret connection-string slot.

---

### Task 1: Pin the EF toolchain, create the test harness, and remove placeholders

**Files:**
- Create: `.config/dotnet-tools.json`
- Create: `Infrastructure.Tests/Infrastructure.Tests.csproj`
- Create: `Infrastructure.Tests/Persistence/ModelFactory.cs`
- Modify: `TeLoSchoolManagement.sln`
- Modify: `Infrastructure/Infrastructure.csproj`
- Remove: every file listed under **Remove** above

**Interfaces:**
- Consumes: the existing four-project solution.
- Produces: a test project referencing Infrastructure; Pomelo/EF packages available to later tasks; no fake Student persistence flow.

- [ ] **Step 1: Create the local tool manifest and pin package versions**

Run:

```powershell
dotnet new tool-manifest
dotnet tool install dotnet-ef --version 8.0.31
dotnet add Infrastructure/Infrastructure.csproj package Pomelo.EntityFrameworkCore.MySql --version 8.0.3
dotnet add Infrastructure/Infrastructure.csproj package Microsoft.EntityFrameworkCore.Design --version 8.0.31
```

Ensure `Microsoft.EntityFrameworkCore.Design` has `PrivateAssets="all"` and
`IncludeAssets="runtime; build; native; contentfiles; analyzers; buildtransitive"`.

- [ ] **Step 2: Scaffold the xUnit project before adding production behavior**

Run:

```powershell
dotnet new xunit -n Infrastructure.Tests -f net8.0
dotnet add Infrastructure.Tests/Infrastructure.Tests.csproj reference Infrastructure/Infrastructure.csproj
dotnet sln TeLoSchoolManagement.sln add Infrastructure.Tests/Infrastructure.Tests.csproj
```

Delete the generated `UnitTest1.cs`. Keep package versions produced by the
template unless restore reports an incompatibility with `net8.0`.

- [ ] **Step 3: Add the shared model factory shell**

Create `ModelFactory.cs` with this wished-for API; it will not compile until
Task 2 creates `ApplicationDbContext`:

```csharp
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Infrastructure.Tests.Persistence;

internal static class ModelFactory
{
    internal static IModel CreateModel()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(
                "Server=localhost;Database=sep_contract;User=root;",
                new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;

        using var context = new ApplicationDbContext(options);
        return context.Model;
    }
}
```

- [ ] **Step 4: Verify the harness fails for the intended missing context**

Run: `dotnet test Infrastructure.Tests/Infrastructure.Tests.csproj`

Expected: compile failure because `Infrastructure.Context.ApplicationDbContext`
does not exist.

- [ ] **Step 5: Remove all approved placeholders and add package/project references**

Delete the files in the plan's **Remove** list. Keep
`Application/Common/ApiResponse.cs`. Add an explicit `Domain` project reference
to Infrastructure alongside its Application reference.

- [ ] **Step 6: Local checkpoint**

Run: `git status --short`

Expected: local additions/removals only; no commit and no push.

---

### Task 2: Establish DbContext and fail-fast dependency injection

**Files:**
- Create: `Infrastructure/Context/ApplicationDbContext.cs`
- Create: `Infrastructure/DependencyInjection.cs`
- Create: `Infrastructure.Tests/Persistence/DependencyInjectionTests.cs`
- Modify: `WebAPI/Program.cs`
- Modify: `WebAPI/appsettings.json`

**Interfaces:**
- Produces: `ApplicationDbContext(DbContextOptions<ApplicationDbContext>)` and `IServiceCollection AddInfrastructure(this IServiceCollection, IConfiguration)`.
- Consumes: Pomelo 8.0.3 and MySQL server version 8.0.0 from Task 1.

- [ ] **Step 1: Write the failing DI tests**

```csharp
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Tests.Persistence;

public sealed class DependencyInjectionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddInfrastructure_RejectsMissingConnectionString(string? value)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = value
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var action = () => new ServiceCollection()
            .AddInfrastructure(configuration);

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Contains("ConnectionStrings:DefaultConnection", exception.Message);
    }
}
```

- [ ] **Step 2: Verify RED**

Run: `dotnet test Infrastructure.Tests/Infrastructure.Tests.csproj --filter DependencyInjectionTests`

Expected: compile failure because `AddInfrastructure` is absent.

- [ ] **Step 3: Implement the minimal DbContext and DI extension**

```csharp
namespace Infrastructure.Context;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
```

`AddInfrastructure` must reject null/blank configuration, then call:

```csharp
services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 0, 0)),
        mysql => mysql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));
```

- [ ] **Step 4: Verify GREEN**

Run: `dotnet test Infrastructure.Tests/Infrastructure.Tests.csproj --filter DependencyInjectionTests`

Expected: all DI tests pass.

- [ ] **Step 5: Make WebAPI a composition root**

Replace sample weather code with controller/Swagger registration and
`builder.Services.AddInfrastructure(builder.Configuration)`. Set
`ConnectionStrings:DefaultConnection` to an empty string in committed settings;
do not insert a password.

- [ ] **Step 6: Local checkpoint**

Run: `dotnet build TeLoSchoolManagement.sln`

Expected: the solution builds successfully with an empty EF model. Subsequent
tasks add the approved entity/configuration slices without changing this public
DbContext constructor.

---

### Task 3: Implement Organization and Academic entities and mappings

**Files:**
- Create entities under `Domain/Entities/Organization`: `School.cs`, `SchoolBranch.cs`, `SchoolClass.cs`, `Room.cs`
- Create entities under `Domain/Entities/Academic`: `AcademicYear.cs`, `Semester.cs`, `GradeLevel.cs`, `Subject.cs`, `Textbook.cs`, `TextbookChapter.cs`, `TextbookLesson.cs`, `AcademicContext.cs`
- Create matching configuration files under `Infrastructure/Configurations/Organization` and `Academic`
- Modify: `Infrastructure/Context/ApplicationDbContext.cs`
- Create: `Infrastructure.Tests/Persistence/OrganizationAcademicModelTests.cs`

**Interfaces:**
- Produces 12 DbSets: `Schools`, `SchoolBranches`, `SchoolClasses`, `Rooms`, `AcademicYears`, `Semesters`, `GradeLevels`, `Subjects`, `Textbooks`, `TextbookChapters`, `TextbookLessons`, `AcademicContexts`.
- Maps tables: `schools`, `school_branches`, `classes`, `rooms`, `academic_years`, `semesters`, `grade_levels`, `subjects`, `textbooks`, `textbook_chapters`, `textbook_lessons`, `academic_contexts`.

- [ ] **Step 1: Write the failing area/table test**

```csharp
[Fact]
public void Model_MapsOrganizationAndAcademicTables()
{
    var expected = new[]
    {
        "academic_contexts", "academic_years", "classes", "grade_levels",
        "rooms", "school_branches", "schools", "semesters", "subjects",
        "textbook_chapters", "textbook_lessons", "textbooks"
    };
    var actual = ModelFactory.CreateModel().GetEntityTypes()
        .Select(x => x.GetTableName())
        .Where(x => expected.Contains(x))
        .OrderBy(x => x)
        .ToArray();

    Assert.Equal(expected.OrderBy(x => x), actual);
}
```

Add focused assertions that `AcademicContext` has its six-column unique index
and composite `(school_branch_id, school_id)` FK, dates use `date`, and every
relationship uses the delete behavior from `sep_schema.sql`.

- [ ] **Step 2: Verify RED**

Run: `dotnet test Infrastructure.Tests/Infrastructure.Tests.csproj --filter OrganizationAcademicModelTests`

Expected: compile failure because the entity types and DbSets do not exist.

- [ ] **Step 3: Implement the 12 POCOs**

Use `ulong`/`ulong?` for unsigned bigint keys, `uint` for `sort_order`,
`DateOnly` for dates, initialized navigation collections, and no EF attributes.
Implement every scalar and navigation shown in the corresponding 12
`CREATE TABLE` blocks in `sep_schema.sql`.

- [ ] **Step 4: Implement all 12 Fluent Configurations**

For each table configure exact table/column names, lengths, defaults, named
keys/indexes/checks, composite/alternate keys, and `DeleteBehavior` matching
`ON DELETE`. In particular preserve:

- `uq_school_branches_id_school` as an alternate key for composite references.
- `ck_academic_years_dates` and `ck_semesters_dates`.
- `uq_classes_branch_year_name` and `idx_classes_grade`.
- `uq_academic_contexts_scope`, `idx_academic_contexts_subject_grade`, and the
  composite branch-school FK.

- [ ] **Step 5: Verify GREEN**

Run: `dotnet test Infrastructure.Tests/Infrastructure.Tests.csproj --filter OrganizationAcademicModelTests`

Expected: all area tests pass.

- [ ] **Step 6: Local checkpoint**

Run: `git status --short`

---

### Task 4: Implement Identity and navigation entities and mappings

**Files:**
- Create entities under `Domain/Entities/Identity`: `User.cs`, `Student.cs`, `Teacher.cs`, `Role.cs`, `Module.cs`, `Navbar.cs`, `Permission.cs`, `UserRole.cs`
- Create eight matching configurations under `Infrastructure/Configurations/Identity`
- Modify: `Infrastructure/Context/ApplicationDbContext.cs`
- Create: `Infrastructure.Tests/Persistence/IdentityModelTests.cs`

**Interfaces:**
- Produces DbSets `Users`, `Students`, `Teachers`, `Roles`, `Modules`, `Navbars`, `Permissions`, `UserRoles`.
- `Permission` uses composite key `(RoleId, NavbarId)` and has no surrogate `Id`.

- [ ] **Step 1: Write failing model tests**

Assert the exact table set:

```csharp
var expected = new[]
{
    "modules", "navbars", "permissions", "roles",
    "students", "teachers", "user_roles", "users"
};
```

Also assert `users.email` is `varchar(254)`, `users.created_at` is
`datetime(6)` with `CURRENT_TIMESTAMP(6)`, Navbar has its self-reference with
cascade delete, Permission has the composite PK, and Student/Teacher user links
have the approved uniqueness and delete behaviors.

- [ ] **Step 2: Verify RED**

Run: `dotnet test Infrastructure.Tests/Infrastructure.Tests.csproj --filter IdentityModelTests`

- [ ] **Step 3: Implement the eight POCOs and DbSets**

Use `DateOnly?` for birth dates, `bool?` for teacher gender, and string status
fields. Model all role, navbar, school branch, class, student, and teacher
navigations from the SQL foreign keys.

- [ ] **Step 4: Implement the eight exact configurations**

Preserve all unique/index names, including `uq_users_moet_identifier`,
`uq_teachers_homeroom_class`, `idx_navbars_module_parent_order`,
`idx_permissions_navbar`, and both `user_roles` indexes. Keep optional FKs and
`SET NULL` semantics exactly as defined.

- [ ] **Step 5: Verify GREEN and the full test project**

Run: `dotnet test Infrastructure.Tests/Infrastructure.Tests.csproj`

Expected: Identity plus prior tests pass.

- [ ] **Step 6: Local checkpoint**

Run: `git status --short`

---

### Task 5: Implement Question Bank and Matrix entities and mappings

**Files:**
- Create entities under `Domain/Entities/QuestionBank`: `WorkTask.cs`, `ExamMatrix.cs`, `MatrixDetail.cs`, `QuestionTask.cs`, `QuestionTaskDetail.cs`, `QuestionBank.cs`, `Question.cs`, `QuestionOption.cs`, `ExamSet.cs`, `ExamSetQuestion.cs`, `ExamVariant.cs`, `ExamVariantQuestion.cs`
- Create 12 matching configurations under `Infrastructure/Configurations/QuestionBank`
- Modify: `Infrastructure/Context/ApplicationDbContext.cs`
- Create: `Infrastructure.Tests/Persistence/QuestionBankModelTests.cs`

**Interfaces:**
- Produces 12 corresponding DbSets, including `WorkTasks` mapped to `tasks`.
- Establishes the persistence graph used by the future Exam Matrix use case.

- [ ] **Step 1: Write failing area and high-risk tests**

Assert these tables:

```csharp
var expected = new[]
{
    "exam_matrices", "exam_set_questions", "exam_sets", "exam_variant_questions",
    "exam_variants", "matrix_details", "question_banks", "question_options",
    "question_task_details", "question_tasks", "questions", "tasks"
};
```

Add assertions for `decimal(5,2)`, positive count checks, `bank_type` check,
`option_order_json` as `json`, and all unique matrix/question cells. Do not add
model checks for the ExamSet trigger rules.

- [ ] **Step 2: Verify RED**

Run: `dotnet test Infrastructure.Tests/Infrastructure.Tests.csproj --filter QuestionBankModelTests`

- [ ] **Step 3: Implement the 12 POCOs and DbSets**

Map every scalar from the corresponding SQL blocks. Use string-backed JSON for
`ExamVariantQuestion.OptionOrderJson`, `decimal` for allocated scores, and
`uint` for counts/positions.

- [ ] **Step 4: Implement the 12 configurations**

Preserve every named key/index/FK/check from the table definitions. Do not add
checks for approval pairing or source self-reference: the three approved ExamSet
triggers are created in Task 9 because their approved rules are represented as
trigger-level integrity objects rather than row-local check expressions.

- [ ] **Step 5: Verify GREEN**

Run: `dotnet test Infrastructure.Tests/Infrastructure.Tests.csproj --filter QuestionBankModelTests`

- [ ] **Step 6: Local checkpoint**

Run: `git status --short`

---

### Task 6: Implement Examination entities and mappings

**Files:**
- Create entities under `Domain/Entities/Examination`: `Exam.cs`, `ExamSubject.cs`, `ExamSubjectGradeLevel.cs`, `ExamSession.cs`, `ExamRoom.cs`, `SessionRoom.cs`, `ExamRegistration.cs`, `ExamProctor.cs`, `ProctorAssignment.cs`, `ExamAttempt.cs`, `ExamAttemptAnswer.cs`, `TechnicalIncident.cs`, `Violation.cs`
- Create 13 matching configurations under `Infrastructure/Configurations/Examination`
- Modify: `Infrastructure/Context/ApplicationDbContext.cs`
- Create: `Infrastructure.Tests/Persistence/ExaminationModelTests.cs`

**Interfaces:**
- Produces 13 DbSets and their relationships to Academic, Identity, and QuestionBank entities.

- [ ] **Step 1: Write failing model tests**

Assert these tables:

```csharp
var expected = new[]
{
    "exam_attempt_answers", "exam_attempts", "exam_proctors", "exam_registrations",
    "exam_rooms", "exam_sessions", "exam_subject_grade_levels", "exam_subjects",
    "exams", "proctor_assignments", "session_rooms", "technical_incidents", "violations"
};
```

Assert `datetime(6)`, score precision, exam/session date checks, candidate and
duration checks, and unique session/registration/proctor constraints. Do not
add model checks for result-publication or primary/backup trigger rules.

- [ ] **Step 2: Verify RED**

Run: `dotnet test Infrastructure.Tests/Infrastructure.Tests.csproj --filter ExaminationModelTests`

- [ ] **Step 3: Implement all 13 POCOs and DbSets**

Use `DateOnly` for exam dates, `DateTime` for session/attempt/incident times,
`decimal?` for scores, `bool?` for answer correctness, and exact nullable FKs.

- [ ] **Step 4: Implement all 13 configurations**

Preserve all named constraints from `sep_schema.sql`. Do not add checks for
result-publication pairing or primary/backup separation; their four approved
triggers are created in Task 9.

- [ ] **Step 5: Verify GREEN**

Run: `dotnet test Infrastructure.Tests/Infrastructure.Tests.csproj --filter ExaminationModelTests`

- [ ] **Step 6: Local checkpoint**

Run: `git status --short`

---

### Task 7: Implement Notification tables without generated columns

**Files:**
- Create entities under `Domain/Entities/Notifications`: `NotificationConfig.cs`, `NotificationTarget.cs`, `Notification.cs`, `NotificationRecipient.cs`
- Create four matching configurations under `Infrastructure/Configurations/Notification`
- Modify: `Infrastructure/Context/ApplicationDbContext.cs`
- Create: `Infrastructure.Tests/Persistence/NotificationModelTests.cs`
- Create: `Infrastructure.Tests/Persistence/SchemaContractTests.cs`

**Interfaces:**
- Produces `NotificationConfigs`, `NotificationTargets`, `Notifications`, and `NotificationRecipients` DbSets.
- Completes the 49-table pre-special-object model.

- [ ] **Step 1: Write failing notification and full-count tests**

```csharp
[Fact]
public void Model_MapsExactlyFortyNineTables()
{
    var tables = ModelFactory.CreateModel().GetEntityTypes()
        .Select(x => x.GetTableName())
        .Where(x => x is not null)
        .Distinct()
        .ToArray();

    Assert.Equal(49, tables.Length);
}
```

Assert the four notification table names, composite branch-school FKs, all
notification checks, and that `base_scope_school_key`/`base_scope_code_key` are
absent at this stage. Because EF Core cannot put a nullable property in an
alternate key, the exact three-column notification parent FK is emitted as a
manual `AddForeignKey` operation in `InitialCreate`.

- [ ] **Step 2: Verify RED**

Run: `dotnet test Infrastructure.Tests/Infrastructure.Tests.csproj --filter "FullyQualifiedName~NotificationModelTests|FullyQualifiedName~SchemaContractTests"`

- [ ] **Step 3: Implement four POCOs and DbSets**

Do not expose generated key columns on `NotificationConfig`. Implement the
normal scalar/navigation properties only.

- [ ] **Step 4: Implement four configurations excluding generated columns**

Preserve only the ordinary unique/index/FK/check definitions present in the
four `CREATE TABLE` blocks. Do not add checks for NotificationTarget shape or
NotificationConfig inheritance; their four approved triggers are created in
Task 9. Do not configure the generated columns or
`uq_notification_configs_base_scope` yet.

- [ ] **Step 5: Verify GREEN and build**

Run:

```powershell
dotnet test Infrastructure.Tests/Infrastructure.Tests.csproj
dotnet build TeLoSchoolManagement.sln
```

Expected: 49-table model tests and the solution build pass.

- [ ] **Step 6: Local checkpoint**

Run: `git status --short`

---

### Task 8: Generate and contract-test InitialCreate

**Files:**
- Create: `Infrastructure.Tests/Persistence/MigrationContractTests.cs`
- Create: `Infrastructure/Migrations/<timestamp>_InitialCreate.cs`
- Create: `Infrastructure/Migrations/<timestamp>_InitialCreate.Designer.cs`
- Create: `Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs`

**Interfaces:**
- Consumes: the 49-table model without generated columns.
- Produces: the first migration, named `InitialCreate`.

- [ ] **Step 1: Write the failing migration contract test**

Give `MigrationContractTests` this connection-free helper:

```csharp
private static ApplicationDbContext CreateContext()
{
    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseMySql(
            "Server=localhost;Database=sep_contract;User=root;",
            new MySqlServerVersion(new Version(8, 0, 0)))
        .Options;

    return new ApplicationDbContext(options);
}
```

```csharp
[Fact]
public void Migrations_StartWithInitialCreate()
{
    using var context = CreateContext();
    var names = context.Database.GetMigrations()
        .Select(id => id[(id.IndexOf('_') + 1)..])
        .ToArray();

    Assert.Equal(new[] { "InitialCreate" }, names);
}
```

Add a script assertion using `context.GetService<IMigrator>().GenerateScript()`:

```csharp
Assert.Equal(49, Regex.Matches(script, @"CREATE TABLE").Count);
Assert.DoesNotContain("CREATE TRIGGER", script, StringComparison.OrdinalIgnoreCase);
Assert.DoesNotContain("CREATE VIEW", script, StringComparison.OrdinalIgnoreCase);
Assert.DoesNotContain("base_scope_school_key", script, StringComparison.OrdinalIgnoreCase);
```

- [ ] **Step 2: Verify RED**

Run: `dotnet test Infrastructure.Tests/Infrastructure.Tests.csproj --filter MigrationContractTests`

Expected: failure because there are no migrations.

- [ ] **Step 3: Generate InitialCreate with the local tool**

Run:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations add InitialCreate --project Infrastructure --startup-project WebAPI --output-dir Migrations
```

Supply a syntactically valid temporary `ConnectionStrings__DefaultConnection`
environment value for design-time host construction; do not store it in a file.

- [ ] **Step 4: Review generated migration against the reference**

Confirm 49 `CreateTable` operations and inspect every named PK/FK/index/check,
column type, nullable/default/comment, and delete behavior. The only deliberate
operation-level adjustment is the nullable notification parent composite FK
described above; all other structure comes directly from the Fluent model.

- [ ] **Step 5: Verify GREEN**

Run: `dotnet test Infrastructure.Tests/Infrastructure.Tests.csproj --filter MigrationContractTests`

- [ ] **Step 6: Local checkpoint**

Run: `git status --short`

---

### Task 9: Add generated columns, index, and all 11 integrity triggers

**Files:**
- Modify: `Infrastructure/Configurations/Notification/NotificationConfigConfiguration.cs`
- Modify: `Infrastructure.Tests/Persistence/NotificationModelTests.cs`
- Modify: `Infrastructure.Tests/Persistence/MigrationContractTests.cs`
- Create: `Infrastructure/Migrations/<timestamp>_AddDatabaseIntegrityObjects.cs`
- Create: `Infrastructure/Migrations/<timestamp>_AddDatabaseIntegrityObjects.Designer.cs`
- Modify: `Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs`

**Interfaces:**
- Produces shadow properties `base_scope_school_key` (`ulong?`) and `base_scope_code_key` (`string?`), their unique index, and exactly 11 triggers.

- [ ] **Step 1: Write failing shadow-property and migration-order tests**

Assert the Domain type has no public properties with generated-column names,
while EF metadata does:

```csharp
Assert.Null(typeof(NotificationConfig).GetProperty("BaseScopeSchoolKey"));
Assert.Null(typeof(NotificationConfig).GetProperty("BaseScopeCodeKey"));
Assert.NotNull(entityType.FindProperty("base_scope_school_key"));
Assert.NotNull(entityType.FindProperty("base_scope_code_key"));
```

Update ordered migration expectation to:

```csharp
Assert.Equal(
    new[] { "InitialCreate", "AddDatabaseIntegrityObjects" },
    names);
```

Assert the final generated script has exactly two `GENERATED ALWAYS AS`
occurrences, contains `uq_notification_configs_base_scope`, contains exactly
11 `CREATE TRIGGER` statements with all approved trigger names, and contains
neither deferred view name.

Use this exact name set in the assertion:

```csharp
var expectedTriggers = new[]
{
    "trg_exam_sets_bi",
    "trg_exam_sets_ai",
    "trg_exam_sets_bu",
    "trg_exam_subjects_bi",
    "trg_exam_subjects_bu",
    "trg_exam_subject_grade_levels_bi",
    "trg_exam_subject_grade_levels_bu",
    "trg_notification_targets_bi",
    "trg_notification_targets_bu",
    "trg_notification_configs_bi",
    "trg_notification_configs_bu"
};

Assert.Equal(11, Regex.Matches(script, @"CREATE\s+TRIGGER", RegexOptions.IgnoreCase).Count);
foreach (var trigger in expectedTriggers)
{
    Assert.Contains(trigger, script, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Verify RED**

Run: `dotnet test Infrastructure.Tests/Infrastructure.Tests.csproj --filter "FullyQualifiedName~NotificationModelTests|FullyQualifiedName~MigrationContractTests"`

- [ ] **Step 3: Add shadow properties and unique index to Fluent configuration**

Configure stored generated expressions exactly as the SQL reference:

```csharp
builder.Property<ulong?>("base_scope_school_key")
    .HasColumnType("bigint unsigned")
    .HasComputedColumnSql(
        "CASE WHEN school_branch_id IS NULL AND base_config_id IS NULL " +
        "THEN COALESCE(school_id, 0) ELSE NULL END",
        stored: true);

builder.Property<string?>("base_scope_code_key")
    .HasColumnType("varchar(100)")
    .HasComputedColumnSql(
        "CASE WHEN school_branch_id IS NULL AND base_config_id IS NULL " +
        "THEN code ELSE NULL END",
        stored: true);

builder.HasIndex("base_scope_school_key", "base_scope_code_key")
    .IsUnique()
    .HasDatabaseName("uq_notification_configs_base_scope");
```

- [ ] **Step 4: Generate the second migration**

Run:

```powershell
dotnet tool run dotnet-ef migrations add AddDatabaseIntegrityObjects --project Infrastructure --startup-project WebAPI --output-dir Migrations
```

- [ ] **Step 5: Add exact trigger SQL to Up and Down**

Add all 11 approved trigger definitions from `sep_schema.sql` lines 982-1192 as
individual `migrationBuilder.Sql(...)` commands. Remove `DELIMITER` directives
because EF sends each trigger definition as one command. The migration must
create exactly these names:

```text
trg_exam_sets_bi
trg_exam_sets_ai
trg_exam_sets_bu
trg_exam_subjects_bi
trg_exam_subjects_bu
trg_exam_subject_grade_levels_bi
trg_exam_subject_grade_levels_bu
trg_notification_targets_bi
trg_notification_targets_bu
trg_notification_configs_bi
trg_notification_configs_bu
```

Use the following approved notification-config definitions with
`migrationBuilder.Sql(...)`. Do not use `DELIMITER` inside EF migration SQL:

```csharp
migrationBuilder.Sql("""
    CREATE TRIGGER trg_notification_configs_bi
    BEFORE INSERT ON notification_configs
    FOR EACH ROW
    BEGIN
      DECLARE v_parent_branch_id BIGINT UNSIGNED;
      DECLARE v_parent_base_id BIGINT UNSIGNED;
      DECLARE v_parent_event_code VARCHAR(100);

      IF NOT (
        (NEW.school_id IS NULL AND NEW.school_branch_id IS NULL AND NEW.base_config_id IS NULL)
        OR (NEW.school_id IS NOT NULL AND NEW.school_branch_id IS NULL AND NEW.base_config_id IS NULL)
        OR (NEW.school_id IS NOT NULL AND NEW.school_branch_id IS NOT NULL AND NEW.base_config_id IS NOT NULL)
      ) THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'invalid system/school/branch notification config shape';
      END IF;

      IF NEW.base_config_id IS NOT NULL
         AND NEW.id IS NOT NULL
         AND NEW.id <> 0
         AND NEW.base_config_id = NEW.id THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'a notification config cannot inherit from itself';
      END IF;

      IF NEW.base_config_id IS NOT NULL THEN
        SET v_parent_branch_id = NULL;
        SET v_parent_base_id = NULL;
        SET v_parent_event_code = NULL;

        SELECT school_branch_id, base_config_id, event_code
          INTO v_parent_branch_id, v_parent_base_id, v_parent_event_code
        FROM notification_configs
        WHERE id = NEW.base_config_id
        LIMIT 1;

        IF v_parent_branch_id IS NOT NULL OR v_parent_base_id IS NOT NULL THEN
          SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'base_config_id must reference a school-level base config';
        END IF;

        IF NOT (v_parent_event_code <=> NEW.event_code) THEN
          SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'branch override must keep the base event_code';
        END IF;
      END IF;
    END
    """);

migrationBuilder.Sql("""
    CREATE TRIGGER trg_notification_configs_bu
    BEFORE UPDATE ON notification_configs
    FOR EACH ROW
    BEGIN
      DECLARE v_parent_branch_id BIGINT UNSIGNED;
      DECLARE v_parent_base_id BIGINT UNSIGNED;
      DECLARE v_parent_event_code VARCHAR(100);

      IF NOT (
        (NEW.school_id IS NULL AND NEW.school_branch_id IS NULL AND NEW.base_config_id IS NULL)
        OR (NEW.school_id IS NOT NULL AND NEW.school_branch_id IS NULL AND NEW.base_config_id IS NULL)
        OR (NEW.school_id IS NOT NULL AND NEW.school_branch_id IS NOT NULL AND NEW.base_config_id IS NOT NULL)
      ) THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'invalid system/school/branch notification config shape';
      END IF;

      IF NEW.base_config_id IS NOT NULL AND NEW.base_config_id = NEW.id THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'a notification config cannot inherit from itself';
      END IF;

      IF NEW.base_config_id IS NOT NULL THEN
        SET v_parent_branch_id = NULL;
        SET v_parent_base_id = NULL;
        SET v_parent_event_code = NULL;

        SELECT school_branch_id, base_config_id, event_code
          INTO v_parent_branch_id, v_parent_base_id, v_parent_event_code
        FROM notification_configs
        WHERE id = NEW.base_config_id
        LIMIT 1;

        IF v_parent_branch_id IS NOT NULL OR v_parent_base_id IS NOT NULL THEN
          SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'base_config_id must reference a school-level base config';
        END IF;

        IF NOT (v_parent_event_code <=> NEW.event_code) THEN
          SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'branch override must keep the base event_code';
        END IF;
      END IF;
    END
    """);
```

In `Down`, execute drops for all 11 names before EF's generated index/column
drops. Use `DROP TRIGGER IF EXISTS` in reverse creation order, ending with the
three ExamSet triggers:

```csharp
migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_notification_configs_bu;");
migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_notification_configs_bi;");
migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_notification_targets_bu;");
migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_notification_targets_bi;");
migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_exam_subject_grade_levels_bu;");
migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_exam_subject_grade_levels_bi;");
migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_exam_subjects_bu;");
migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_exam_subjects_bi;");
migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_exam_sets_bu;");
migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_exam_sets_ai;");
migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_exam_sets_bi;");
```

Then drop `uq_notification_configs_base_scope`, `base_scope_code_key`, and
`base_scope_school_key` in that order. If EF generated those operations, reorder
them rather than duplicating them.

- [ ] **Step 6: Verify GREEN**

Run:

```powershell
dotnet test Infrastructure.Tests/Infrastructure.Tests.csproj --filter "FullyQualifiedName~NotificationModelTests|FullyQualifiedName~MigrationContractTests"
dotnet test Infrastructure.Tests/Infrastructure.Tests.csproj
```

- [ ] **Step 7: Local checkpoint**

Run: `git status --short`

---

### Task 10: Verify build, executable MySQL migration, rollback, and local diff

**Files:**
- Modify only files required to correct failures discovered by the verification commands.

**Interfaces:**
- Consumes: both migrations and the WebAPI composition root.
- Produces: evidence that upgrade and rollback work against MySQL 8.0.

- [ ] **Step 1: Restore, build, and run the full test suite**

Run:

```powershell
dotnet tool restore
dotnet restore TeLoSchoolManagement.sln
dotnet build TeLoSchoolManagement.sln --no-restore
dotnet test TeLoSchoolManagement.sln --no-build
```

Expected: zero build errors and all tests pass without warnings caused by the
new code.

- [ ] **Step 2: Check a local MySQL 8.0 server**

Run `mysql --version` and verify that a MySQL 8.0 service is running. The target
database must be empty; do not import `sep_schema.sql` before applying EF
migrations.

- [ ] **Step 3: Create the empty verification database**

Create database `sep` with `utf8mb4` and the `utf8mb4_0900_ai_ci` collation,
then set the process-local connection string to the local server before running
the migration command.

- [ ] **Step 4: Apply both migrations to the empty database**

Set the process-local connection string and run:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Server=127.0.0.1;Port=3307;Database=sep;User=root;'
dotnet tool run dotnet-ef database update --project Infrastructure --startup-project WebAPI
```

Query `information_schema` to confirm 49 base tables plus EF migration history,
two generated columns, one `uq_notification_configs_base_scope` index, exactly
11 triggers, and zero views owned by this schema.

- [ ] **Step 5: Roll back to migration 0**

Run:

```powershell
dotnet tool run dotnet-ef database update 0 --project Infrastructure --startup-project WebAPI
```

Confirm the 49 application tables and special objects are gone.

- [ ] **Step 6: Remove only the disposable test container**

Resolve and verify that the exact target is `leto-mysql-migration-test`, then
stop and remove that container. Do not touch other containers or volumes.

- [ ] **Step 7: Final local review**

Run:

```powershell
git status --short
git diff --check
git diff --stat
```

Inspect the complete local diff. Do not commit and do not push. Report build,
test, migration-up, schema inspection, and rollback evidence separately.
