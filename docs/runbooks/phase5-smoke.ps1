param(
    [string] $DatabaseName = 'sep_matrix_tests',
    [int] $Port = 5098,
    [string] $SigningKey = 'phase5-verification-signing-key-at-least-32chars'
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$config = Get-Content -Raw -Encoding UTF8 (Join-Path $repoRoot 'WebAPI\appsettings.Development.json') | ConvertFrom-Json
$connectionString = $config.ConnectionStrings.DefaultConnection -replace 'Database=[^;]+', "Database=$DatabaseName"
$password = (($connectionString -split ';' | Where-Object { $_ -like 'Password=*' }) -split '=', 2)[1]
$mysql = (Get-Command mysql -ErrorAction Stop).Source
$baseUrl = "http://127.0.0.1:$Port"
$stdoutPath = [System.IO.Path]::GetTempFileName()
$stderrPath = [System.IO.Path]::GetTempFileName()
$server = $null

$env:MYSQL_PWD = $password
$env:ConnectionStrings__DefaultConnection = $connectionString
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
$env:Jwt__SigningKey = $SigningKey
$env:Jwt__Issuer = 'LeTo-Backend'
$env:Jwt__Audience = 'LeTo-Frontend'
$env:Logging__EventLog__LogLevel__Default = 'None'

function Invoke-JsonRequest(
    [string] $Name,
    [string] $Method,
    [string] $Uri,
    [string] $Token,
    [int] $ExpectedStatus,
    [object] $Body
) {
    $parameters = @{
        Method = $Method
        Uri = $Uri
        Headers = @{ Authorization = "Bearer $Token" }
        SkipHttpErrorCheck = $true
    }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = $Body | ConvertTo-Json -Depth 10 -Compress
    }
    $response = Invoke-WebRequest @parameters
    if ($response.StatusCode -ne $ExpectedStatus) {
        throw "$Name expected HTTP $ExpectedStatus but received $($response.StatusCode): $($response.Content)"
    }
    Write-Host "PASS $Name ($($response.StatusCode))"
    if ([string]::IsNullOrWhiteSpace($response.Content)) {
        return $null
    }
    return $response.Content | ConvertFrom-Json
}

function Invoke-EmptyRequest(
    [string] $Name,
    [string] $Method,
    [string] $Uri,
    [string] $Token,
    [int] $ExpectedStatus
) {
    return Invoke-JsonRequest $Name $Method $Uri $Token $ExpectedStatus $null
}

function Get-LoginToken([string] $Username) {
    $response = Invoke-WebRequest -Method POST -Uri "$baseUrl/api/auth/login" -ContentType 'application/json' -Body (@{
        username = $Username
        password = 'Phase5Password!123'
    } | ConvertTo-Json -Compress) -SkipHttpErrorCheck
    if ($response.StatusCode -ne 200) {
        throw "Login for $Username expected HTTP 200 but received $($response.StatusCode): $($response.Content)"
    }
    Write-Host "PASS Login $Username (200)"
    return ($response.Content | ConvertFrom-Json).accessToken
}

function Invoke-Sql([string] $Sql) {
    $Sql | & $mysql --protocol=TCP --host=127.0.0.1 --port=3306 --user=root $DatabaseName
    if ($LASTEXITCODE -ne 0) {
        throw "MySQL command failed with exit code $LASTEXITCODE."
    }
}

function Reset-Fixtures {
    Invoke-Sql @'
DELETE FROM matrix_details WHERE exam_matrix_id IN (SELECT id FROM exam_matrices WHERE academic_context_id = 9506);
DELETE FROM exam_matrices WHERE academic_context_id = 9506;
DELETE FROM tasks WHERE academic_context_id = 9506;
DELETE FROM user_roles WHERE id IN (9515, 9516, 9519);
DELETE FROM users WHERE id IN (9511, 9512, 9518);
DELETE FROM roles WHERE id IN (9513, 9514, 9517);
DELETE FROM semesters WHERE id = 9510;
DELETE FROM academic_contexts WHERE id = 9506;
DELETE FROM textbook_lessons WHERE id = 9507;
DELETE FROM textbook_chapters WHERE id = 9509;
DELETE FROM textbooks WHERE id = 9508;
DELETE FROM school_branches WHERE id = 9503;
DELETE FROM schools WHERE id = 9502;
DELETE FROM subjects WHERE id = 9505;
DELETE FROM grade_levels WHERE id = 9504;
DELETE FROM academic_years WHERE id = 9501;
DELETE FROM provinces WHERE code = '79';
'@
}

function Seed-Fixtures {
    Reset-Fixtures
    Invoke-Sql @'
INSERT INTO provinces (code, name, division_type, source, is_active, last_synced_at)
VALUES ('79', 'Phase 5 Province', 'province', 'SMOKE', 1, UTC_TIMESTAMP(6));
INSERT INTO academic_years (id, code, name, start_date, end_date, status, province_code)
VALUES (9501, '79-PHASE-5', 'Phase 5 Year', '2026-01-01', '2026-12-31', 'ACTIVE', '79');
INSERT INTO schools (id, code, name, status, province_code)
VALUES (9502, 'P5-SCHOOL', 'Phase 5 School', 'ACTIVE', '79');
INSERT INTO school_branches (id, school_id, code, name, status)
VALUES (9503, 9502, 'P5-BRANCH', 'Phase 5 Branch', 'ACTIVE');
INSERT INTO grade_levels (id, name, status)
VALUES (9504, 'Phase 5 Grade', 'ACTIVE');
INSERT INTO subjects (id, name, status)
VALUES (9505, 'Phase 5 Subject', 'ACTIVE');
INSERT INTO textbooks (id, title, book_set)
VALUES (9508, 'Phase 5 Textbook', NULL);
INSERT INTO textbook_chapters (id, textbook_id, title, sort_order)
VALUES (9509, 9508, 'Phase 5 Chapter', 1);
INSERT INTO textbook_lessons (id, chapter_id, content, title, sort_order)
VALUES (9507, 9509, 'Phase 5 Content', 'Phase 5 Lesson', 1);
INSERT INTO academic_contexts
    (id, academic_year_id, school_id, textbook_id, subject_id, grade_level_id, school_branch_id)
VALUES (9506, 9501, 9502, 9508, 9505, 9504, 9503);
INSERT INTO semesters (id, academic_year_id, semester_order, name, start_date, end_date, status)
VALUES (9510, 9501, 1, 'Phase 5 Semester', '2026-01-01', '2026-06-30', 'ACTIVE');
INSERT INTO roles (id, code, name)
VALUES (9513, 'PHT', 'Phase 5 PHT');
INSERT INTO roles (id, code, name)
VALUES (9514, 'TEAM_LEAD', 'Phase 5 Team Lead');
INSERT INTO roles (id, code, name)
VALUES (9517, 'OperationalAdmin', 'Phase 5 Operational Admin');
INSERT INTO users
    (id, username, email, school_branch_id, password_hash, full_name, status)
VALUES
    (9511, 'phase5-pht', 'phase5-pht@example.test', 9503, 'AQAAAAIAAYagAAAAEEnNcG9hZr8pKCWz/WencpsHKGWYKS0C8MRefmymKrnL/NPppiFIiOSg2ulbGs61fA==', 'Phase 5 PHT', 'ACTIVE'),
    (9512, 'phase5-lead', 'phase5-lead@example.test', 9503, 'AQAAAAIAAYagAAAAEEnNcG9hZr8pKCWz/WencpsHKGWYKS0C8MRefmymKrnL/NPppiFIiOSg2ulbGs61fA==', 'Phase 5 Team Lead', 'ACTIVE'),
    (9518, 'phase5-admin', 'phase5-admin@example.test', 9503, 'AQAAAAIAAYagAAAAEEnNcG9hZr8pKCWz/WencpsHKGWYKS0C8MRefmymKrnL/NPppiFIiOSg2ulbGs61fA==', 'Phase 5 Operational Admin', 'ACTIVE');
INSERT INTO user_roles (id, user_id, role_id)
VALUES (9515, 9511, 9513), (9516, 9512, 9514), (9519, 9518, 9517);
'@
}

try {
    Seed-Fixtures
    $server = Start-Process -FilePath 'dotnet' -ArgumentList @('WebAPI.dll', '--urls', $baseUrl) -WorkingDirectory (Join-Path $repoRoot 'WebAPI\bin\Debug\net8.0') -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -WindowStyle Hidden -PassThru

    $ready = $false
    for ($attempt = 1; $attempt -le 30; $attempt++) {
        Start-Sleep -Milliseconds 500
        if ($server.HasExited) {
            break
        }
        try {
            $probe = Invoke-WebRequest -Uri "$baseUrl/swagger/v1/swagger.json" -SkipHttpErrorCheck
            if ($probe.StatusCode -eq 200) {
                $ready = $true
                break
            }
        }
        catch {
        }
    }
    if (-not $ready) {
        $apiError = if (Test-Path -LiteralPath $stderrPath) { Get-Content -Raw -LiteralPath $stderrPath } else { '' }
        $apiOutput = if (Test-Path -LiteralPath $stdoutPath) { Get-Content -Raw -LiteralPath $stdoutPath } else { '' }
        throw "WebAPI did not become ready. STDERR=$apiError STDOUT=$apiOutput"
    }
    Write-Host 'PASS Swagger contract endpoint (200)'

    $phtToken = Get-LoginToken 'phase5-pht'
    $leadToken = Get-LoginToken 'phase5-lead'
    $adminToken = Get-LoginToken 'phase5-admin'

    $unauthenticatedAcademic = Invoke-WebRequest -Method GET -Uri "$baseUrl/api/academic-years?provinceCode=79" -SkipHttpErrorCheck
    if ($unauthenticatedAcademic.StatusCode -ne 401) { throw "Academic years without token expected HTTP 401 but received $($unauthenticatedAcademic.StatusCode)." }
    Write-Host 'PASS Academic years without token (401)'
    Invoke-JsonRequest 'Academic years as OperationalAdmin' 'GET' "$baseUrl/api/academic-years?provinceCode=79" $adminToken 200 $null | Out-Null
    Invoke-JsonRequest 'Province list as OperationalAdmin' 'GET' "$baseUrl/api/provinces" $adminToken 200 $null | Out-Null
    Invoke-JsonRequest 'Academic years denied for PHT' 'GET' "$baseUrl/api/academic-years?provinceCode=79" $phtToken 403 $null | Out-Null

    $reference = Invoke-JsonRequest 'Reference data' 'GET' "$baseUrl/api/matrix-reference-data?academicContextId=9506" $phtToken 200 $null
    if ($reference.academicContexts.Count -lt 1 -or $reference.lessons.Count -lt 1 -or $reference.teamLeads.Count -lt 1) {
        throw 'Reference data did not contain the seeded context, lesson, and Team Lead.'
    }
    Invoke-JsonRequest 'List matrix' 'GET' "$baseUrl/api/matrices" $phtToken 200 $null | Out-Null

    $directRequest = [ordered]@{
        name = 'Phase 5 Direct'
        academicContextId = 9506
        semesterId = 9510
        taskId = $null
        details = @([ordered]@{ lessonId = 9507; cognitiveLevel = 'NHAN_BIET'; questionCount = 2; allocatedScore = 10 })
    }
    $direct = Invoke-JsonRequest 'Create direct Draft' 'POST' "$baseUrl/api/matrices" $phtToken 201 $directRequest
    $directId = [string]$direct.id
    if ($direct.status -ne 'DRAFT' -or $direct.totalQuestions -ne 2 -or [decimal]$direct.totalScore -ne 10) { throw 'Direct matrix status or totals are incorrect.' }

    $updatedDirectRequest = [ordered]@{
        name = 'Phase 5 Direct Updated'
        academicContextId = 9506
        semesterId = 9510
        taskId = $null
        details = @([ordered]@{ lessonId = 9507; cognitiveLevel = 'NHAN_BIET'; questionCount = 3; allocatedScore = 10 })
    }
    $updatedDirect = Invoke-JsonRequest 'Update detail' 'PUT' "$baseUrl/api/matrices/$directId" $phtToken 200 $updatedDirectRequest
    if ($updatedDirect.totalQuestions -ne 3 -or [decimal]$updatedDirect.totalScore -ne 10) { throw 'Updated matrix totals are incorrect.' }

    $duplicateRequest = [ordered]@{
        name = 'Phase 5 Duplicate'
        academicContextId = 9506
        semesterId = 9510
        taskId = $null
        details = @(
            [ordered]@{ lessonId = 9507; cognitiveLevel = 'NHAN_BIET'; questionCount = 1; allocatedScore = 1 },
            [ordered]@{ lessonId = 9507; cognitiveLevel = 'NHAN_BIET'; questionCount = 1; allocatedScore = 1 }
        )
    }
    $duplicate = Invoke-JsonRequest 'Duplicate cell rejection' 'POST' "$baseUrl/api/matrices" $phtToken 422 $duplicateRequest
    if ($duplicate.code -ne 'DuplicateDetail') { throw "Duplicate cell returned code '$($duplicate.code)'." }

    $confirmed = Invoke-EmptyRequest 'Confirm direct' 'POST' "$baseUrl/api/matrices/$directId/confirm" $phtToken 200
    if ($confirmed.status -ne 'APPROVED') { throw 'Direct confirmation did not finish in APPROVED.' }

    $export = Invoke-WebRequest -Method GET -Uri "$baseUrl/api/matrices/$directId/export.xlsx" -Headers @{ Authorization = "Bearer $phtToken" } -SkipHttpErrorCheck
    if ($export.StatusCode -ne 200 -or $export.Content.Length -le 0 -or $export.Headers['Content-Type'] -notlike '*spreadsheetml.sheet*' -or $export.Headers['Content-Disposition'] -notlike 'attachment*') {
        throw "Excel export contract failed: status=$($export.StatusCode), bytes=$($export.Content.Length)."
    }
    Write-Host "PASS Export Excel (200, $($export.Content.Length) bytes)"

    $archived = Invoke-EmptyRequest 'Archive approved matrix' 'POST' "$baseUrl/api/matrices/$directId/archive" $phtToken 200
    if ($archived.status -ne 'ARCHIVED') { throw 'Archive did not return ARCHIVED.' }
    $clone = Invoke-EmptyRequest 'Clone archived matrix' 'POST' "$baseUrl/api/matrices/$directId/clone" $phtToken 201
    $cloneId = [string]$clone.id
    if ($clone.status -ne 'DRAFT' -or $null -ne $clone.taskId) { throw 'Clone did not create an unlinked Draft.' }
    $invalidState = Invoke-EmptyRequest 'Invalid state conflict' 'POST' "$baseUrl/api/matrices/$cloneId/approve" $phtToken 409
    if ($invalidState.code -ne 'InvalidTransition') { throw "Invalid state returned code '$($invalidState.code)'." }
    Invoke-EmptyRequest 'Delete Draft' 'DELETE' "$baseUrl/api/matrices/$cloneId" $phtToken 204 | Out-Null

    $forbidden = Invoke-JsonRequest 'Wrong-role direct create' 'POST' "$baseUrl/api/matrices" $leadToken 403 $directRequest
    if ($null -eq $forbidden) { throw 'Wrong-role response did not contain Problem Details.' }

    $taskRequest = [ordered]@{ assignedToUserId = 9512; academicContextId = 9506; semesterId = 9510; dueAt = $null; description = 'Phase 5 matrix task' }
    $task = Invoke-JsonRequest 'Assign matrix task' 'POST' "$baseUrl/api/matrix-tasks" $phtToken 201 $taskRequest
    $taskId = [string]$task.id
    Invoke-JsonRequest 'List matrix tasks' 'GET' "$baseUrl/api/matrix-tasks" $phtToken 200 $null | Out-Null

    $delegatedRequest = [ordered]@{
        name = 'Phase 5 Delegated'
        academicContextId = 9506
        semesterId = 9510
        taskId = [ulong]$taskId
        details = @([ordered]@{ lessonId = 9507; cognitiveLevel = 'VAN_DUNG'; questionCount = 4; allocatedScore = 10 })
    }
    $delegated = Invoke-JsonRequest 'Create delegated Draft' 'POST' "$baseUrl/api/matrices" $leadToken 201 $delegatedRequest
    $delegatedId = [string]$delegated.id
    Invoke-JsonRequest 'List my Team Lead tasks' 'GET' "$baseUrl/api/my/matrix-tasks" $leadToken 200 $null | Out-Null
    Invoke-JsonRequest 'Team Lead matrix detail' 'GET' "$baseUrl/api/matrices/$delegatedId" $leadToken 200 $null | Out-Null

    $submitted = Invoke-EmptyRequest 'Submit delegated matrix' 'POST' "$baseUrl/api/matrices/$delegatedId/submit" $leadToken 200
    if ($submitted.status -ne 'SUBMITTED') { throw 'Delegated submit did not return SUBMITTED.' }
    $rejected = Invoke-JsonRequest 'PHT reject delegated matrix' 'POST' "$baseUrl/api/matrices/$delegatedId/reject" $phtToken 200 @{ comment = 'Phase 5 revise' }
    if ($rejected.status -ne 'DRAFT' -or $rejected.rejectComment -ne 'Phase 5 revise') { throw 'Delegated rejection did not return Draft with the review comment.' }

    $updatedDelegatedRequest = [ordered]@{
        name = 'Phase 5 Delegated Reviewed'
        academicContextId = 9506
        semesterId = 9510
        taskId = [ulong]$taskId
        details = @([ordered]@{ lessonId = 9507; cognitiveLevel = 'VAN_DUNG'; questionCount = 5; allocatedScore = 10 })
    }
    Invoke-JsonRequest 'Team Lead revises rejected matrix' 'PUT' "$baseUrl/api/matrices/$delegatedId" $leadToken 200 $updatedDelegatedRequest | Out-Null
    $resubmitted = Invoke-EmptyRequest 'Resubmit delegated matrix' 'POST' "$baseUrl/api/matrices/$delegatedId/submit" $leadToken 200
    if ($resubmitted.status -ne 'SUBMITTED') { throw 'Delegated resubmit did not return SUBMITTED.' }
    $approved = Invoke-EmptyRequest 'PHT approve delegated matrix' 'POST' "$baseUrl/api/matrices/$delegatedId/approve" $phtToken 200
    if ($approved.status -ne 'APPROVED') { throw 'Delegated approve did not return APPROVED.' }

    $duplicateTaskMatrix = Invoke-JsonRequest 'Duplicate task conflict' 'POST' "$baseUrl/api/matrices" $leadToken 409 $delegatedRequest
    if ($duplicateTaskMatrix.code -ne 'TaskAlreadyHasMatrix') { throw "Duplicate task returned code '$($duplicateTaskMatrix.code)'." }
    $delegatedClone = Invoke-EmptyRequest 'Clone approved delegated matrix' 'POST' "$baseUrl/api/matrices/$delegatedId/clone" $phtToken 201
    if ($delegatedClone.status -ne 'DRAFT' -or $null -ne $delegatedClone.taskId) { throw 'Delegated clone did not become an unlinked Draft.' }
    Invoke-EmptyRequest 'Delete delegated clone Draft' 'DELETE' "$baseUrl/api/matrices/$($delegatedClone.id)" $phtToken 204 | Out-Null

    Write-Host 'PHASE5_SMOKE_PASS'
}
catch {
    Write-Host 'PHASE5_SMOKE_FAIL'
    if (Test-Path -LiteralPath $stderrPath) {
        Write-Host '--- WebAPI stderr ---'
        Get-Content -Raw -LiteralPath $stderrPath
    }
    if (Test-Path -LiteralPath $stdoutPath) {
        Write-Host '--- WebAPI stdout ---'
        Get-Content -Raw -LiteralPath $stdoutPath
    }
    throw
}
finally {
    if ($null -ne $server -and -not $server.HasExited) {
        Stop-Process -Id $server.Id -Force
        Wait-Process -Id $server.Id -Timeout 10 -ErrorAction SilentlyContinue
    }
    Reset-Fixtures
    Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
}
