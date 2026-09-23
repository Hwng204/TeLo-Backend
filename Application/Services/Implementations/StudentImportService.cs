using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Domain.Entities.Identity;
using Infrastructure.Exports;
using Infrastructure.Repositories.Interface;

namespace Application.Services.Implement;

public sealed class StudentImportService(
    IStudentImportRepository repository,
    TimeProvider clock) : IStudentImportService
{
    public const int MaxFileBytes = 2 * 1024 * 1024;
    private const int MaxPageSize = 100;
    private const int MaxRowsPageSize = StudentImportWorkbook.MaxRows;
    private const int MaxCommentLength = 1000;
    private const int MaxReportedConflicts = 20;
    private const int RawColumnLength = 255;
    private const string XlsxExtension = ".xlsx";

    public async Task<ServiceResult<StudentImportTemplateInfo>> GetTemplateInfoAsync(
        ImportCaller caller,
        ulong? academicYearId,
        CancellationToken cancellationToken)
    {
        var lookup = await LookupAsync(caller, academicYearId, [], cancellationToken);
        if (lookup.Error is not null)
        {
            return ServiceResult<StudentImportTemplateInfo>.Failure(
                lookup.Error.Code, lookup.Error.Message, lookup.Error.Details);
        }

        var value = lookup.Value!;
        return ServiceResult<StudentImportTemplateInfo>.Success(new StudentImportTemplateInfo(
            value.AcademicYearId,
            value.AcademicYearName,
            StudentImportWorkbook.Columns
                .Select(c => new StudentImportColumnDto(
                    c.Key, c.Header, c.Required, c.Format, c.Example))
                .ToList(),
            value.Classes.Select(c => new DirectoryOption(c.Id, c.Code, c.Name)).ToList(),
            StudentImportWorkbook.Genders,
            StudentImportWorkbook.DateFormat,
            StudentImportWorkbook.MaxRows,
            MaxFileBytes));
    }

    public async Task<ServiceResult<StudentImportFile>> BuildTemplateAsync(
        ImportCaller caller,
        ulong? academicYearId,
        CancellationToken cancellationToken)
    {
        var lookup = await LookupAsync(caller, academicYearId, [], cancellationToken);
        if (lookup.Error is not null)
        {
            return ServiceResult<StudentImportFile>.Failure(
                lookup.Error.Code, lookup.Error.Message, lookup.Error.Details);
        }

        var value = lookup.Value!;
        var bytes = StudentImportWorkbook.CreateTemplate(
            value.AcademicYearName,
            value.Classes.Select(c => new StudentImportClassOption(c.Code, c.Name)).ToList());
        return ServiceResult<StudentImportFile>.Success(new StudentImportFile(
            $"{FileNames.Safe($"MauDanhSachHocSinh_{value.AcademicYearName}", "MauDanhSachHocSinh")}{XlsxExtension}",
            bytes));
    }

    public async Task<ServiceResult<StudentImportBatchDetailDto>> PreviewAsync(
        ImportCaller caller,
        ulong? academicYearId,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var fileError = CheckFile(fileName, content);
        if (fileError is not null)
        {
            return Failure<StudentImportBatchDetailDto>(
                SchoolDirectoryErrorCodes.ImportFileInvalid, fileError);
        }

        IReadOnlyList<StudentImportRawRow> rawRows;
        try
        {
            rawRows = StudentImportWorkbook.Read(new MemoryStream(content));
        }
        catch (StudentImportFormatException exception)
        {
            return Failure<StudentImportBatchDetailDto>(
                SchoolDirectoryErrorCodes.ImportFileInvalid, exception.Message);
        }

        var lookup = await LookupAsync(
            caller, academicYearId,
            rawRows.Select(r => r.Code).Where(c => c.Length > 0).Distinct().ToList(),
            cancellationToken);
        if (lookup.Error is not null)
        {
            return ServiceResult<StudentImportBatchDetailDto>.Failure(
                lookup.Error.Code, lookup.Error.Message, lookup.Error.Details);
        }

        var value = lookup.Value!;
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var classesByCode = value.Classes.ToLookup(c => c.Code, StringComparer.OrdinalIgnoreCase);
        var firstRowByCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var rows = rawRows.Select(raw =>
        {
            var verdict = StudentImportRules.Validate(
                raw, classesByCode, value.ExistingCodes, firstRowByCode, today);
            return new NewImportRow(
                raw.RowNumber, Fit(raw.Code), Fit(raw.FullName), Fit(raw.DateOfBirth),
                Fit(raw.Gender), Fit(raw.AdmissionDate), Fit(raw.ClassCode),
                verdict.ClassId, verdict.Errors.Count == 0,
                StudentImportRules.SerializeErrors(verdict.Errors));
        }).ToList();

        var batchId = await repository.SaveDraftAsync(
            new NewImportBatch(
                value.SchoolId, value.AcademicYearId,
                caller.IsAdmin ? StudentImportSourceCodes.Admin : StudentImportSourceCodes.School,
                caller.ActorUserId, Fit(Path.GetFileName(fileName.Trim())), content,
                clock.GetUtcNow().UtcDateTime, rows),
            cancellationToken);

        var batch = await repository.GetBatchAsync(batchId, cancellationToken);
        return ServiceResult<StudentImportBatchDetailDto>.Success(
            await DetailAsync(batch!, new StudentImportRowsQuery(), cancellationToken));
    }

    public async Task<ServiceResult<DirectoryPage<StudentImportBatchDto>>> ListAsync(
        ImportCaller caller,
        StudentImportListQuery query,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var status = query.Status?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(status) && !StudentImportBatchStatusCodes.All.Contains(status))
        {
            errors["status"] = [$"Trạng thái phải là một trong: {string.Join(", ", StudentImportBatchStatusCodes.All)}."];
        }

        ValidatePaging(query.Page, query.PageSize, MaxPageSize, errors);
        if (errors.Count > 0)
        {
            return ServiceResult<DirectoryPage<StudentImportBatchDto>>.Failure(
                SchoolDirectoryErrorCodes.Validation, "Tham số truy vấn không hợp lệ.", errors);
        }

        // An admin without a school in the route sees every school's batches (the review inbox).
        ulong? schoolFilter = caller.SchoolId;
        if (!caller.IsAdmin)
        {
            var school = await ResolveSchoolAsync(caller, cancellationToken);
            if (school.Error is not null)
            {
                return ServiceResult<DirectoryPage<StudentImportBatchDto>>.Failure(
                    school.Error.Code, school.Error.Message);
            }

            schoolFilter = school.Value;
        }

        var page = await repository.ListBatchesAsync(
            schoolFilter, string.IsNullOrEmpty(status) ? null : status,
            query.Page, query.PageSize, cancellationToken);
        return ServiceResult<DirectoryPage<StudentImportBatchDto>>.Success(
            new DirectoryPage<StudentImportBatchDto>(
                page.Items.Select(ToDto).ToList(), query.Page, query.PageSize, page.TotalCount));
    }

    public async Task<ServiceResult<StudentImportBatchDetailDto>> GetAsync(
        ImportCaller caller,
        ulong batchId,
        StudentImportRowsQuery rows,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        ValidatePaging(rows.Page, rows.PageSize, MaxRowsPageSize, errors);
        if (errors.Count > 0)
        {
            return ServiceResult<StudentImportBatchDetailDto>.Failure(
                SchoolDirectoryErrorCodes.Validation, "Tham số truy vấn không hợp lệ.", errors);
        }

        var batch = await LoadBatchAsync(caller, batchId, cancellationToken);
        if (batch.Error is not null)
        {
            return ServiceResult<StudentImportBatchDetailDto>.Failure(
                batch.Error.Code, batch.Error.Message);
        }

        return ServiceResult<StudentImportBatchDetailDto>.Success(
            await DetailAsync(batch.Value!, rows, cancellationToken));
    }

    public async Task<ServiceResult<StudentImportFile>> GetFileAsync(
        ImportCaller caller,
        ulong batchId,
        CancellationToken cancellationToken)
    {
        var batch = await LoadBatchAsync(caller, batchId, cancellationToken);
        if (batch.Error is not null)
        {
            return ServiceResult<StudentImportFile>.Failure(batch.Error.Code, batch.Error.Message);
        }

        var file = await repository.GetFileAsync(batchId, cancellationToken);
        if (file is null)
        {
            return NotFound<StudentImportFile>();
        }

        var name = FileNames.Safe(Path.GetFileNameWithoutExtension(file.FileName), "DanhSachHocSinh");
        return ServiceResult<StudentImportFile>.Success(
            new StudentImportFile($"{name}{XlsxExtension}", file.Content));
    }

    public async Task<ServiceResult<StudentImportBatchDetailDto>> SubmitAsync(
        ImportCaller caller,
        ulong batchId,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadBatchAsync(caller, batchId, cancellationToken);
        if (loaded.Error is not null)
        {
            return ServiceResult<StudentImportBatchDetailDto>.Failure(
                loaded.Error.Code, loaded.Error.Message);
        }

        var batch = loaded.Value!;
        if (caller.IsAdmin ||
            batch.Source != StudentImportSourceCodes.School ||
            batch.Status != StudentImportBatchStatusCodes.Draft)
        {
            return StateInvalid<StudentImportBatchDetailDto>(
                "Chỉ gửi được file nháp do nhà trường tải lên.");
        }

        // The admin cannot apply a batch with invalid rows, so refuse to send one.
        if (batch.InvalidRows > 0)
        {
            return Failure<StudentImportBatchDetailDto>(
                SchoolDirectoryErrorCodes.ImportHasInvalidRows,
                $"Còn {batch.InvalidRows} dòng lỗi. Vui lòng sửa file và tải lên lại.");
        }

        return await TransitionAsync(
            batch, [StudentImportBatchStatusCodes.Draft], StudentImportBatchStatusCodes.Submitted,
            null, null, cancellationToken);
    }

    public async Task<ServiceResult<StudentImportBatchDetailDto>> CancelAsync(
        ImportCaller caller,
        ulong batchId,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadBatchAsync(caller, batchId, cancellationToken);
        if (loaded.Error is not null)
        {
            return ServiceResult<StudentImportBatchDetailDto>.Failure(
                loaded.Error.Code, loaded.Error.Message);
        }

        if (loaded.Value!.Status != StudentImportBatchStatusCodes.Draft)
        {
            return StateInvalid<StudentImportBatchDetailDto>("Chỉ hủy được file đang ở trạng thái nháp.");
        }

        return await TransitionAsync(
            loaded.Value, [StudentImportBatchStatusCodes.Draft],
            StudentImportBatchStatusCodes.Cancelled, null, null, cancellationToken);
    }

    public async Task<ServiceResult<StudentImportBatchDetailDto>> RejectAsync(
        ImportCaller caller,
        ulong batchId,
        RejectStudentImportRequest request,
        CancellationToken cancellationToken)
    {
        var comment = request.Comment?.Trim();
        if (string.IsNullOrEmpty(comment) || comment.Length > MaxCommentLength)
        {
            return ServiceResult<StudentImportBatchDetailDto>.Failure(
                SchoolDirectoryErrorCodes.Validation,
                "Lý do từ chối không hợp lệ.",
                new Dictionary<string, string[]>
                {
                    ["comment"] = [$"Lý do từ chối là bắt buộc và tối đa {MaxCommentLength} ký tự."]
                });
        }

        var loaded = await LoadBatchAsync(caller, batchId, cancellationToken);
        if (loaded.Error is not null)
        {
            return ServiceResult<StudentImportBatchDetailDto>.Failure(
                loaded.Error.Code, loaded.Error.Message);
        }

        if (!caller.IsAdmin || loaded.Value!.Status != StudentImportBatchStatusCodes.Submitted)
        {
            return StateInvalid<StudentImportBatchDetailDto>(
                "Chỉ từ chối được file nhà trường đã gửi lên.");
        }

        return await TransitionAsync(
            loaded.Value, [StudentImportBatchStatusCodes.Submitted],
            StudentImportBatchStatusCodes.Rejected, caller.ActorUserId, comment,
            cancellationToken);
    }

    public async Task<ServiceResult<StudentImportBatchDetailDto>> ApplyAsync(
        ImportCaller caller,
        ulong batchId,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadBatchAsync(caller, batchId, cancellationToken);
        if (loaded.Error is not null)
        {
            return ServiceResult<StudentImportBatchDetailDto>.Failure(
                loaded.Error.Code, loaded.Error.Message);
        }

        var batch = loaded.Value!;
        var applicable = caller.IsAdmin &&
            ((batch.Status == StudentImportBatchStatusCodes.Draft &&
                    batch.Source == StudentImportSourceCodes.Admin) ||
                batch.Status == StudentImportBatchStatusCodes.Submitted);
        if (!applicable)
        {
            return StateInvalid<StudentImportBatchDetailDto>(
                "File này không ở trạng thái có thể áp dụng.");
        }

        if (batch.InvalidRows > 0)
        {
            return Failure<StudentImportBatchDetailDto>(
                SchoolDirectoryErrorCodes.ImportHasInvalidRows,
                $"Còn {batch.InvalidRows} dòng lỗi nên chưa thể áp dụng.");
        }

        var pending = await repository.GetPendingValidRowsAsync(batchId, cancellationToken);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var students = new List<ImportStudentToCreate>(pending.Count);
        foreach (var row in pending)
        {
            var errors = new List<StudentImportRowErrorDto>();
            var fields = StudentImportRules.ParseFields(
                row.RawCode, row.RawFullName, row.RawDateOfBirth, row.RawGender,
                row.RawAdmissionDate, today, errors);
            if (errors.Count > 0 || fields.AdmissionDate is null || row.ResolvedSchoolClassId is null)
            {
                return Failure<StudentImportBatchDetailDto>(
                    SchoolDirectoryErrorCodes.ImportHasInvalidRows,
                    $"Dòng {row.RowNumber} không còn hợp lệ. Vui lòng tải file lên lại.");
            }

            students.Add(new ImportStudentToCreate(
                row.RowId, row.RowNumber, fields.Code, fields.FullName, fields.DateOfBirth,
                fields.Gender, fields.AdmissionDate.Value, row.ResolvedSchoolClassId.Value));
        }

        if (students.Count == 0)
        {
            return StateInvalid<StudentImportBatchDetailDto>("File không còn dòng nào để áp dụng.");
        }

        var result = await repository.ApplyAsync(
            batchId, caller.ActorUserId, students, clock.GetUtcNow().UtcDateTime,
            cancellationToken);
        switch (result.Status)
        {
            case ImportApplyStatus.StateInvalid:
                return StateInvalid<StudentImportBatchDetailDto>(
                    "File đã được xử lý bởi thao tác khác. Vui lòng tải lại.");
            case ImportApplyStatus.RowsConflict:
                return ServiceResult<StudentImportBatchDetailDto>.Failure(
                    SchoolDirectoryErrorCodes.ImportHasInvalidRows,
                    "Một số dòng không còn hợp lệ nên chưa có học sinh nào được thêm.",
                    new Dictionary<string, string[]>
                    {
                        ["rows"] = result.Conflicts.Take(MaxReportedConflicts)
                            .Select(ConflictMessage).ToArray()
                    });
        }

        var applied = await repository.GetBatchAsync(batchId, cancellationToken);
        return ServiceResult<StudentImportBatchDetailDto>.Success(
            await DetailAsync(applied!, new StudentImportRowsQuery(), cancellationToken));
    }

    private async Task<ServiceResult<StudentImportBatchDetailDto>> TransitionAsync(
        ImportBatchSummary batch,
        string[] from,
        string to,
        ulong? reviewerUserId,
        string? comment,
        CancellationToken cancellationToken)
    {
        var moved = await repository.TryTransitionAsync(
            batch.Id, from, to, reviewerUserId, comment, clock.GetUtcNow().UtcDateTime,
            cancellationToken);
        if (!moved)
        {
            return StateInvalid<StudentImportBatchDetailDto>(
                "File đã được xử lý bởi thao tác khác. Vui lòng tải lại.");
        }

        var updated = await repository.GetBatchAsync(batch.Id, cancellationToken);
        return ServiceResult<StudentImportBatchDetailDto>.Success(
            await DetailAsync(updated!, new StudentImportRowsQuery(), cancellationToken));
    }

    private async Task<ServiceResult<ImportLookup>> LookupAsync(
        ImportCaller caller,
        ulong? academicYearId,
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken)
    {
        if (academicYearId == 0)
        {
            return ServiceResult<ImportLookup>.Failure(
                SchoolDirectoryErrorCodes.Validation, "Tham số truy vấn không hợp lệ.",
                new Dictionary<string, string[]> { ["academicYearId"] = ["Năm học không hợp lệ."] });
        }

        var school = await ResolveSchoolAsync(caller, cancellationToken);
        if (school.Error is not null)
        {
            return ServiceResult<ImportLookup>.Failure(school.Error.Code, school.Error.Message);
        }

        var lookup = await repository.GetLookupAsync(
            school.Value, academicYearId, codes, cancellationToken);
        if (lookup.Value is { } value)
        {
            return ServiceResult<ImportLookup>.Success(value);
        }

        return lookup.Status switch
        {
            DirectoryReadStatus.SchoolNotFound => Failure<ImportLookup>(
                SchoolDirectoryErrorCodes.SchoolNotFound, "Không tìm thấy trường."),
            DirectoryReadStatus.ActiveAcademicYearNotFound => Failure<ImportLookup>(
                SchoolDirectoryErrorCodes.ActiveAcademicYearNotFound,
                "Trường chưa có năm học đang áp dụng. Vui lòng chọn năm học."),
            _ => ServiceResult<ImportLookup>.Failure(
                SchoolDirectoryErrorCodes.Validation, "Tham số truy vấn không hợp lệ.",
                new Dictionary<string, string[]>
                {
                    ["academicYearId"] = ["Năm học không hợp lệ với trường này."]
                })
        };
    }

    // Admin routes name the school; school users get theirs from their branch.
    private async Task<ServiceResult<ulong>> ResolveSchoolAsync(
        ImportCaller caller,
        CancellationToken cancellationToken)
    {
        if (caller.SchoolId is { } schoolId)
        {
            return ServiceResult<ulong>.Success(schoolId);
        }

        if (caller.IsAdmin)
        {
            return Failure<ulong>(SchoolDirectoryErrorCodes.SchoolNotFound, "Không tìm thấy trường.");
        }

        var actor = await repository.GetActorSchoolIdAsync(caller.ActorUserId, cancellationToken);
        return actor.Status switch
        {
            DirectoryReadStatus.Success => ServiceResult<ulong>.Success(actor.Value),
            DirectoryReadStatus.ActorNotFound => Failure<ulong>(
                SchoolDirectoryErrorCodes.Unauthorized, "Tài khoản không hợp lệ."),
            _ => Failure<ulong>(
                SchoolDirectoryErrorCodes.SchoolScopeRequired,
                "Tài khoản chưa được gán cơ sở/trường.")
        };
    }

    // A batch outside the caller's school looks exactly like a missing one.
    private async Task<ServiceResult<ImportBatchSummary>> LoadBatchAsync(
        ImportCaller caller,
        ulong batchId,
        CancellationToken cancellationToken)
    {
        var batch = await repository.GetBatchAsync(batchId, cancellationToken);
        if (batch is null)
        {
            return NotFound<ImportBatchSummary>();
        }

        ulong? expectedSchool = caller.SchoolId;
        if (expectedSchool is null && !caller.IsAdmin)
        {
            var school = await ResolveSchoolAsync(caller, cancellationToken);
            if (school.Error is not null)
            {
                return Failure<ImportBatchSummary>(school.Error.Code, school.Error.Message);
            }

            expectedSchool = school.Value;
        }

        return expectedSchool is { } expected && batch.SchoolId != expected
            ? NotFound<ImportBatchSummary>()
            : ServiceResult<ImportBatchSummary>.Success(batch);
    }

    private async Task<StudentImportBatchDetailDto> DetailAsync(
        ImportBatchSummary batch,
        StudentImportRowsQuery rows,
        CancellationToken cancellationToken)
    {
        var page = await repository.GetRowsAsync(
            batch.Id, rows.Page, rows.PageSize, rows.OnlyInvalid, cancellationToken);
        return new StudentImportBatchDetailDto(
            ToDto(batch),
            new DirectoryPage<StudentImportRowDto>(
                page.Items.Select(ToRowDto).ToList(), rows.Page, rows.PageSize, page.TotalCount));
    }

    private static string? CheckFile(string fileName, byte[] content)
    {
        if (string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(Path.GetExtension(fileName.Trim()), XlsxExtension, StringComparison.OrdinalIgnoreCase))
        {
            return "Chỉ nhận file Excel .xlsx.";
        }

        if (content.Length == 0)
        {
            return "File rỗng.";
        }

        if (content.Length > MaxFileBytes)
        {
            return $"File vượt quá {MaxFileBytes / (1024 * 1024)} MB.";
        }

        // An .xlsx is a zip archive, so it starts with "PK".
        return content.Length < 4 || content[0] != (byte)'P' || content[1] != (byte)'K'
            ? "File không phải Excel .xlsx hợp lệ."
            : null;
    }

    private static void ValidatePaging(
        int page,
        int pageSize,
        int maxPageSize,
        Dictionary<string, string[]> errors)
    {
        if (page < 1)
        {
            errors["page"] = ["Trang phải lớn hơn hoặc bằng 1."];
        }

        if (pageSize < 1 || pageSize > maxPageSize)
        {
            errors["pageSize"] = [$"Kích thước trang phải từ 1 đến {maxPageSize}."];
        }
    }

    private static string ConflictMessage(ImportRowConflict conflict)
    {
        var where = conflict.RowNumber > 0 ? $"Dòng {conflict.RowNumber}: " : string.Empty;
        return conflict.Reason == "CLASS_UNAVAILABLE"
            ? $"{where}lớp không còn hợp lệ (đã ngừng hoặc bị xóa)."
            : $"{where}mã học sinh {conflict.Code} đã được dùng.".Trim();
    }

    private static StudentImportBatchDto ToDto(ImportBatchSummary b) => new(
        b.Id, b.SchoolId, b.SchoolName, b.AcademicYearId, b.AcademicYearName, b.Source, b.Status,
        b.FileName, b.TotalRows, b.ValidRows, b.InvalidRows, b.CreatedByName, b.CreatedAt,
        b.ReviewedByName, b.ReviewedAt, b.ReviewComment, b.AppliedAt);

    private static StudentImportRowDto ToRowDto(ImportRowDetail r) => new(
        r.RowNumber, r.RawCode, r.RawFullName, r.RawDateOfBirth, r.RawGender, r.RawAdmissionDate,
        r.RawClassCode, r.ResolvedSchoolClassId, r.ResolvedClassName, r.IsValid,
        StudentImportRules.DeserializeErrors(r.ErrorJson), r.CreatedStudentId);

    // Raw columns are 255 wide; longer input is already reported as a row error.
    private static string Fit(string value) =>
        value.Length > RawColumnLength ? value[..RawColumnLength] : value;

    private static ServiceResult<T> Failure<T>(string code, string message) =>
        ServiceResult<T>.Failure(code, message);

    private static ServiceResult<T> NotFound<T>() =>
        Failure<T>(SchoolDirectoryErrorCodes.ImportBatchNotFound, "Không tìm thấy file import.");

    private static ServiceResult<T> StateInvalid<T>(string message) =>
        Failure<T>(SchoolDirectoryErrorCodes.ImportBatchStateInvalid, message);
}
