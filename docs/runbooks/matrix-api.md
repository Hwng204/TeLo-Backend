# Matrix API runbook

## Local configuration

Keep the database connection string in `WebAPI/appsettings.Development.json` or an environment variable. Do not commit it.

Required JWT settings:

```powershell
$env:Jwt__SigningKey = "a-local-development-key-with-at-least-32-characters"
$env:Jwt__Issuer = "LeTo-Backend"
$env:Jwt__Audience = "LeTo-Frontend"
```

Role-code configuration is read from `MatrixAuth:PhtRoleCodes` and `MatrixAuth:TeamLeadRoleCodes`. The defaults are `PHT`, `TEAM_LEAD`, and `TO_TRUONG`; align them with the actual `roles.code` values before using login in a shared environment.

## Authentication

Call `POST /api/auth/login` with a database user whose `password_hash` uses the ASP.NET Core `PasswordHasher<User>` format. Send the returned token as:

```text
Authorization: Bearer <accessToken>
```

Matrix requests require a `NameIdentifier` claim and a recognized role claim. Missing identity returns `401`; an authenticated user without a matrix role returns `403`.

## Matrix rules

- Direct PHT matrices use `taskId = null`.
- Delegated matrices resolve academic context and semester from the Matrix task.
- Totals are calculated from `details`; clients must not send or persist totals.
- Lifecycle is `DRAFT <-> SUBMITTED -> APPROVED -> ARCHIVED`.
- Direct PHT confirmation returns only the final `APPROVED` result.
- Clone creates a separate unlinked Draft; it does not create a version row.
- There is no `REJECTED` status and no duration field.

## Verification

```powershell
dotnet build TeLoSchoolManagement.sln --no-restore
dotnet test TeLoSchoolManagement.sln --no-build
dotnet test Infrastructure.Tests\Infrastructure.Tests.csproj --no-build
dotnet test WebAPI.IntegrationTests\WebAPI.IntegrationTests.csproj --no-build
dotnet tool run dotnet-ef migrations list --project Infrastructure --startup-project WebAPI
```

For the Phase 5 database smoke test, apply migrations to the dedicated test database first, then run:

```powershell
dotnet tool run dotnet-ef database update --project Infrastructure --startup-project WebAPI
& .\docs\runbooks\phase5-smoke.ps1
```

The smoke script uses `sep_matrix_tests`, creates only temporary fixtures with IDs `9501`–`9516`, logs in with temporary fixture users to obtain JWTs, and removes its fixtures in `finally`. It does not modify `sep_retry` or store a real secret in source.

Phase 5 smoke coverage:

- Swagger contract endpoint and authenticated matrix list.
- Direct Draft create/update/confirm/archive/clone/delete.
- Delegated task assignment and Team Lead Draft/submit/withdraw/resubmit/PHT approve flow.
- Reference data, role denial (`403`), invalid state (`409`), duplicate detail (`422`), duplicate task (`409`).
- Excel download response and clone-as-new-unlinked-Draft behavior.
