# School Directory live-contract fixes — implementation plan

> **For implementer:** Execute this plan with the `superpowers:executing-plans` skill, one task at a time, keeping existing user changes outside these files untouched.

## Goal

Make the two failed `sep_smoke` checks conform to `docs/test-plan-school-directory-api.md`:

1. `GET /api/classes` without `status` returns only `ACTIVE` classes, while an explicit allowed status remains an override.
2. A multipart upload whose file exceeds 2 MB gets a complete HTTP `413 Payload Too Large` response with `IMPORT_FILE_INVALID`, rather than a transport reset. The import service must not be called and the file must not be copied into the controller buffer.

## Architecture

- The default class-list policy belongs in `SchoolDirectoryService.ListClassesAsync`, which is the shared path for both school-scoped and admin-scoped controllers. The repository keeps its existing generic, nullable status filter so explicit repository callers retain their current semantics.
- The 2 MB limit is a business/upload contract enforced in `StudentImportControllerBase.UploadAsync`. The existing global multipart limit (8 MB) remains the framework safety ceiling. Remove the lower per-action request limit from both upload routes so requests between 2 MB and 8 MB reach the shared controller guard and can receive a response.

## Tech

.NET 8 / ASP.NET Core MVC, xUnit, existing `WebApplicationFactory` integration tests. No new packages, middleware, exception handlers, configuration keys, routes, DTOs, or database migrations.

## Spec references

- `docs/test-plan-school-directory-api.md`: G2 requires default `ACTIVE`; H8 accepts 400 or 413 for a file above 2 MB.
- Live smoke evidence on `sep_smoke` (2026-09-21): default class list contained `INACTIVE`; a 3 MB multipart upload reset the connection before HTTP response.

## Global constraints

- Preserve all unrelated dirty-worktree changes; do not commit, push, or modify migrations.
- Keep `status=INACTIVE` valid and visible when explicitly requested.
- Keep the existing 8 MB global `FormOptions.MultipartBodyLengthLimit`; requests above it may still be rejected by framework binding, but must not call the import service.
- Return the established `ApiResponse` shape and `IMPORT_FILE_INVALID` code for the controller-level >2 MB rejection.

## Review focus

- No default-status condition in individual controllers or repository queries; both public routes must receive the same service policy.
- Both school and admin upload actions must lose the low `[RequestSizeLimit]` attribute.
- The >2 MB guard remains before `MemoryStream` / `CopyToAsync` and before `PreviewAsync`.
- Tests must assert the status, error code, and zero service calls; a TestServer-only assertion is not sufficient to prove the Kestrel transport outcome, so complete the focused live H8 smoke after automated tests.

---

### Task 1 — Default class lists to active in the shared service

**Files**

- Modify: `Application/Services/Implementations/SchoolDirectoryService.cs`
- Modify: `Application.Tests/SchoolDirectory/SchoolDirectoryServiceTests.cs`

**Implementation**

1. In `ListClassesAsync`, preserve `NormalizeStatus` exactly as validation/normalization. After it succeeds, use `SchoolClassStatusCodes.Active` when the normalized value is null. Pass that resolved value into `ClassDirectoryFilter`.
2. Do not change `ClassListQuery`, routes, or `SchoolDirectoryRepository.ListClassesAsync`. A non-null filter is intentional only for the application service’s omitted-query default.
3. Add a focused service test, near the existing class-list validation test, that calls `ListClassesAsync` with `status: null` and asserts the fake repository received a `ClassDirectoryFilter` whose `Status` is `SchoolClassStatusCodes.Active`.
4. Add or extend a companion focused test for explicit lowercase `inactive`; assert it is normalized to `INACTIVE` and passed through. This prevents the new default from masking the supported admin/read filter.

**Acceptance checks**

- `GET /api/classes` and `GET /api/admin/schools/{schoolId}/classes` both use the same default because they share `ListClassesAsync`.
- `?status=INACTIVE` still returns inactive classes; `?status=BOGUS` still fails validation before repository access.

### Task 2 — Return 413 from the shared file-size guard

**Files**

- Modify: `WebAPI/Controllers/StudentImportsController.cs`
- Modify: `WebAPI/Controllers/Admin/StudentImportsAdminController.cs`
- Modify: `WebAPI/Controllers/StudentImportControllerBase.cs`
- Modify: `WebAPI/Program.cs`
- Modify: `WebAPI.IntegrationTests/StudentImportApiTests.cs`

**Implementation**

1. Remove `[RequestSizeLimit(UploadRequestLimit)]` from both `POST` upload actions. They then inherit the existing 8 MB global multipart ceiling.
2. Delete `UploadRequestLimit` from `StudentImportControllerBase` because no route uses it.
3. In `UploadAsync`, retain the pre-buffer `file.Length > StudentImportService.MaxFileBytes` check, but construct the existing `ApiResponse<StudentImportBatchDetailDto>.Fail` for `IMPORT_FILE_INVALID` and return it with `StatusCodes.Status413PayloadTooLarge`. Do not route this case through `ToActionResult`, since that intentionally maps `IMPORT_FILE_INVALID` to 422 for invalid file content.
4. Rewrite the `Program.cs` comment so it accurately documents the one 8 MB framework ceiling and the controller’s 2 MB file contract. Do not alter the 8 MB value.
5. Update `Upload_ALargeFileIsRefusedBeforeItIsReadIntoMemory` to expect 413, `IMPORT_FILE_INVALID`, and zero service calls.
6. Tighten `Upload_AGrosslyOversizedBodyNeverReachesTheService` to the same 413 behavior for its 4 MB TestServer request, including response-code and no-service-call assertions. Remove the obsolete comment that treats 400/422 as acceptable under TestServer.
7. If the integration-test fixture exposes the admin upload client route, add a second minimal assertion for that route; otherwise document in the test that both routes only differ before their shared `UploadAsync` call and verify the attribute removal by source review. Do not duplicate a large suite just to exercise inherited plumbing.

**Acceptance checks**

- A 2 MB + 1 KB `.xlsx` request receives HTTP 413 and an `ApiResponse` containing `IMPORT_FILE_INVALID`.
- The service call counter stays zero; no bytes are copied to `MemoryStream`.
- 0–2 MB valid files still flow to `PreviewAsync`; malformed/empty and invalid-workbook cases retain their current 400/422 behavior.
- The 8 MB multipart ceiling remains a hard framework backstop for any upload route.

### Task 3 — Run focused regressions and live smoke

**Files**

- Verify only: `docs/test-plan-school-directory-api.md`

**Automated commands**

```powershell
dotnet test Application.Tests/Application.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~SchoolDirectoryServiceTests"
dotnet test WebAPI.IntegrationTests/WebAPI.IntegrationTests.csproj -c Release --no-restore --filter "FullyQualifiedName~StudentImportApiTests"
dotnet build TeLoSchoolManagement.sln -c Release --no-restore --no-incremental
```

If the solution’s other `Tests/Application.Tests/Application.Tests.csproj` is the selected test project in the current checkout, use that project path instead; do not infer a green test result from a project that does not include `SchoolDirectoryServiceTests.cs`.

**Live smoke on `sep_smoke`**

1. Start the Release API against `sep_smoke` using the known working Development launch configuration. Do not run a second instance on the same port.
2. Seed only the minimum disposable school/branch/year/classes/users needed by the existing plan, including one `ACTIVE` and one `INACTIVE` class in the same school/year. Use a unique ID range and remove only that range afterward.
3. With a GV or HT token, call `GET /api/classes?academicYearId={yearId}`. Assert all returned classes are `ACTIVE` and the active fixture class is present.
4. Call the same endpoint with `status=INACTIVE`. Assert the inactive fixture class is present; call `status=BOGUS` and assert 422 validation.
5. Upload a generated 3 MB multipart `.xlsx` payload as HT/PHT to `POST /api/student-imports`. Assert the client receives a complete HTTP 413 (not a socket reset), response body contains `IMPORT_FILE_INVALID`, and no draft batch was created. Repeat once through the admin school-scoped upload route if the fixture has the admin token.
6. Run one normal small valid upload to confirm the path still reaches preview and produces a `DRAFT` batch, then cancel/delete it.
7. Clean only the fixture IDs/batches/files created in this smoke and verify the cleanup counts are zero.

### Task 4 — Final review and reporting

1. Inspect `git diff --` limited to the implementation/test files above. Confirm no accidental generated files, migration changes, or unrelated dirty files were staged or edited.
2. Re-read G2 and H8 in `docs/test-plan-school-directory-api.md`; record the observed HTTP status/code and evidence in the active task handoff/state note, not by weakening the expected behavior in the test plan.
3. Report the three automated command results, live G2/H8 results, the normal-upload regression result, and cleanup confirmation. Do not claim the full plan is passing if another scenario remains unrun or failed.
