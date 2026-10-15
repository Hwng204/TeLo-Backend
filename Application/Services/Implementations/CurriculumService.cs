using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Domain.Entities.Academic;
using Infrastructure.Exports;
using Infrastructure.Repositories.Interface;

namespace Application.Services.Implement;

// Error codes are part of the API contract: CurriculumController maps them to status codes.
public static class CurriculumErrorCodes
{
    public const string Validation = "VALIDATION_ERROR";
    public const string BranchRequired = "BRANCH_REQUIRED";
    public const string ChapterNotFound = "CHAPTER_NOT_FOUND";
    public const string LessonNotFound = "LESSON_NOT_FOUND";
    public const string ChapterDuplicate = "CHAPTER_DUPLICATE";
    public const string LessonDuplicate = "LESSON_DUPLICATE";
    public const string ChapterScopeLocked = "CHAPTER_SCOPE_LOCKED";
    public const string ImportFileInvalid = "IMPORT_FILE_INVALID";
    public const string ImportHasInvalidRows = "IMPORT_HAS_INVALID_ROWS";
    public const string ImportConflict = "IMPORT_CONFLICT";
}

public sealed class CurriculumService(ICurriculumRepository repository) : ICurriculumService
{
    public const int MaxFileBytes = 5 * 1024 * 1024;
    private const string TemplateFileName = "mau-nhap-chuong-bai.xlsx";
    private const string ArchivedStatus = "INACTIVE";

    public async Task<ServiceResult<CurriculumDto>> GetAsync(
        CurriculumActor actor,
        CancellationToken cancellationToken)
    {
        if (await repository.GetActorBranchIdAsync(actor.UserId, cancellationToken) is not { } branchId)
        {
            return BranchRequired<CurriculumDto>();
        }

        return ServiceResult<CurriculumDto>.Success(new CurriculumDto(
            actor.CanManage,
            await repository.ListGradesAsync(cancellationToken),
            await repository.ListFieldsAsync(cancellationToken),
            await repository.ListChaptersAsync(branchId, cancellationToken)));
    }

    public async Task<ServiceResult<CurriculumChapterRow>> CreateChapterAsync(
        CurriculumActor actor,
        SaveChapterRequest request,
        CancellationToken cancellationToken)
    {
        if (await repository.GetActorBranchIdAsync(actor.UserId, cancellationToken) is not { } branchId)
        {
            return BranchRequired<CurriculumChapterRow>();
        }

        var (number, title, errors) = await ValidateChapterAsync(request, cancellationToken);
        if (errors.Count > 0)
        {
            return ValidationFailure<CurriculumChapterRow>(errors);
        }

        Task<(bool Number, bool Title)> FindConflict() => repository.FindChapterConflictAsync(
            branchId, request.GradeLevelId, request.FieldId, number, title, null, cancellationToken);
        var conflict = await FindConflict();
        if (conflict.Number || conflict.Title)
        {
            return ChapterDuplicate(number, conflict);
        }

        var chapter = new Chapter
        {
            SchoolBranchId = branchId,
            GradeLevelId = request.GradeLevelId,
            SubjectId = request.FieldId,
            Number = number,
            Title = title
        };
        repository.Add(chapter);
        if (await repository.SaveAsync(cancellationToken) == CurriculumSaveStatus.Duplicate)
        {
            // A concurrent request took the number or title; look again to say which.
            return ChapterDuplicate(number, await FindConflict());
        }

        return await LoadChapterAsync(branchId, chapter.Id, cancellationToken);
    }

    public async Task<ServiceResult<CurriculumChapterRow>> UpdateChapterAsync(
        CurriculumActor actor,
        ulong chapterId,
        SaveChapterRequest request,
        CancellationToken cancellationToken)
    {
        if (await repository.GetActorBranchIdAsync(actor.UserId, cancellationToken) is not { } branchId)
        {
            return BranchRequired<CurriculumChapterRow>();
        }

        var chapter = await repository.GetChapterAsync(branchId, chapterId, cancellationToken);
        if (chapter is null)
        {
            return ChapterNotFound<CurriculumChapterRow>();
        }

        var (number, title, errors) = await ValidateChapterAsync(request, cancellationToken);
        if (errors.Count > 0)
        {
            return ValidationFailure<CurriculumChapterRow>(errors);
        }

        // Matrices and question tasks pick lessons by grade and subject, so a chapter in use keeps both.
        var scopeChanged = chapter.GradeLevelId != request.GradeLevelId || chapter.SubjectId != request.FieldId;
        if (scopeChanged && await repository.IsChapterInUseAsync(chapterId, cancellationToken))
        {
            return ServiceResult<CurriculumChapterRow>.Failure(
                CurriculumErrorCodes.ChapterScopeLocked,
                "Không thể đổi khối lớp hoặc môn học vì chương học đã được sử dụng.");
        }

        Task<(bool Number, bool Title)> FindConflict() => repository.FindChapterConflictAsync(
            branchId, request.GradeLevelId, request.FieldId, number, title, chapterId, cancellationToken);
        var conflict = await FindConflict();
        if (conflict.Number || conflict.Title)
        {
            return ChapterDuplicate(number, conflict);
        }

        chapter.GradeLevelId = request.GradeLevelId;
        chapter.SubjectId = request.FieldId;
        chapter.Number = number;
        chapter.Title = title;
        if (await repository.SaveAsync(cancellationToken) == CurriculumSaveStatus.Duplicate)
        {
            return ChapterDuplicate(number, await FindConflict());
        }

        return await LoadChapterAsync(branchId, chapterId, cancellationToken);
    }

    public async Task<ServiceResult<CurriculumDeleteResultDto>> DeleteChapterAsync(
        CurriculumActor actor,
        ulong chapterId,
        CancellationToken cancellationToken)
    {
        if (await repository.GetActorBranchIdAsync(actor.UserId, cancellationToken) is not { } branchId)
        {
            return BranchRequired<CurriculumDeleteResultDto>();
        }

        var chapter = await repository.GetChapterAsync(branchId, chapterId, cancellationToken);
        if (chapter is null)
        {
            return ChapterNotFound<CurriculumDeleteResultDto>();
        }

        if (!await repository.IsChapterInUseAsync(chapterId, cancellationToken))
        {
            // Its lessons go with it (cascade).
            repository.Remove(chapter);
            if (await repository.SaveAsync(cancellationToken) != CurriculumSaveStatus.InUse)
            {
                return ServiceResult<CurriculumDeleteResultDto>.Success(new CurriculumDeleteResultDto(false));
            }

            // A foreign key stopped it: a lesson became used meanwhile, so archive instead.
            chapter = await repository.GetChapterAsync(branchId, chapterId, cancellationToken);
            if (chapter is null)
            {
                return ChapterNotFound<CurriculumDeleteResultDto>();
            }
        }

        // Only the chapter row: its lessons leave the lists with it and keep what references them.
        chapter.Status = ArchivedStatus;
        await repository.SaveAsync(cancellationToken);
        return ServiceResult<CurriculumDeleteResultDto>.Success(new CurriculumDeleteResultDto(true));
    }

    public async Task<ServiceResult<CurriculumLessonRow>> CreateLessonAsync(
        CurriculumActor actor,
        ulong chapterId,
        SaveLessonRequest request,
        CancellationToken cancellationToken)
    {
        if (await repository.GetActorBranchIdAsync(actor.UserId, cancellationToken) is not { } branchId)
        {
            return BranchRequired<CurriculumLessonRow>();
        }

        var chapter = await repository.GetChapterAsync(branchId, chapterId, cancellationToken);
        if (chapter is null)
        {
            return ChapterNotFound<CurriculumLessonRow>();
        }

        var (number, title, errors) = ValidateLesson(request);
        if (errors.Count > 0)
        {
            return ValidationFailure<CurriculumLessonRow>(errors);
        }

        Task<(bool Number, bool Title)> FindConflict() =>
            repository.FindLessonConflictAsync(chapterId, number, title, null, cancellationToken);
        var conflict = await FindConflict();
        if (conflict.Number || conflict.Title)
        {
            return LessonDuplicate(number, conflict);
        }

        var lesson = new Lesson { ChapterId = chapterId, Number = number, Title = title };
        repository.Add(lesson);
        if (await repository.SaveAsync(cancellationToken) == CurriculumSaveStatus.Duplicate)
        {
            return LessonDuplicate(number, await FindConflict());
        }

        return await LoadLessonAsync(branchId, lesson.Id, cancellationToken);
    }

    public async Task<ServiceResult<CurriculumLessonRow>> UpdateLessonAsync(
        CurriculumActor actor,
        ulong lessonId,
        SaveLessonRequest request,
        CancellationToken cancellationToken)
    {
        if (await repository.GetActorBranchIdAsync(actor.UserId, cancellationToken) is not { } branchId)
        {
            return BranchRequired<CurriculumLessonRow>();
        }

        var lesson = await repository.GetLessonAsync(branchId, lessonId, cancellationToken);
        if (lesson is null)
        {
            return LessonNotFound<CurriculumLessonRow>();
        }

        var (number, title, errors) = ValidateLesson(request);
        if (errors.Count > 0)
        {
            return ValidationFailure<CurriculumLessonRow>(errors);
        }

        Task<(bool Number, bool Title)> FindConflict() =>
            repository.FindLessonConflictAsync(lesson.ChapterId, number, title, lessonId, cancellationToken);
        var conflict = await FindConflict();
        if (conflict.Number || conflict.Title)
        {
            return LessonDuplicate(number, conflict);
        }

        lesson.Number = number;
        lesson.Title = title;
        if (await repository.SaveAsync(cancellationToken) == CurriculumSaveStatus.Duplicate)
        {
            return LessonDuplicate(number, await FindConflict());
        }

        return await LoadLessonAsync(branchId, lessonId, cancellationToken);
    }

    public async Task<ServiceResult<CurriculumDeleteResultDto>> DeleteLessonAsync(
        CurriculumActor actor,
        ulong lessonId,
        CancellationToken cancellationToken)
    {
        if (await repository.GetActorBranchIdAsync(actor.UserId, cancellationToken) is not { } branchId)
        {
            return BranchRequired<CurriculumDeleteResultDto>();
        }

        var lesson = await repository.GetLessonAsync(branchId, lessonId, cancellationToken);
        if (lesson is null)
        {
            return LessonNotFound<CurriculumDeleteResultDto>();
        }

        if (!await repository.IsLessonInUseAsync(lessonId, cancellationToken))
        {
            repository.Remove(lesson);
            if (await repository.SaveAsync(cancellationToken) != CurriculumSaveStatus.InUse)
            {
                return ServiceResult<CurriculumDeleteResultDto>.Success(new CurriculumDeleteResultDto(false));
            }

            // A foreign key stopped it: the lesson became used meanwhile, so archive instead.
            lesson = await repository.GetLessonAsync(branchId, lessonId, cancellationToken);
            if (lesson is null)
            {
                return LessonNotFound<CurriculumDeleteResultDto>();
            }
        }

        lesson.Status = ArchivedStatus;
        await repository.SaveAsync(cancellationToken);
        return ServiceResult<CurriculumDeleteResultDto>.Success(new CurriculumDeleteResultDto(true));
    }

    public async Task<ServiceResult<CurriculumFile>> BuildTemplateAsync(CancellationToken cancellationToken)
    {
        var grades = await repository.ListGradesAsync(cancellationToken);
        var fields = await repository.ListFieldsAsync(cancellationToken);
        var content = CurriculumWorkbook.CreateTemplate(
            grades.Select(grade => grade.Name).ToArray(),
            fields.Select(field => CurriculumImportRules.Clean(field.Name)).ToArray());
        return ServiceResult<CurriculumFile>.Success(new CurriculumFile(TemplateFileName, content));
    }

    public async Task<ServiceResult<CurriculumImportPreviewDto>> PreviewImportAsync(
        CurriculumActor actor,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var evaluation = await EvaluateAsync(actor, fileName, content, cancellationToken);
        return evaluation.Error is { } error
            ? ServiceResult<CurriculumImportPreviewDto>.Failure(error.Code, error.Message, error.Details)
            : ServiceResult<CurriculumImportPreviewDto>.Success(evaluation.Value!.Plan.Preview);
    }

    public async Task<ServiceResult<CurriculumImportResultDto>> ImportAsync(
        CurriculumActor actor,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var evaluation = await EvaluateAsync(actor, fileName, content, cancellationToken);
        if (evaluation.Error is { } error)
        {
            return ServiceResult<CurriculumImportResultDto>.Failure(error.Code, error.Message, error.Details);
        }

        var (branchId, plan) = evaluation.Value!;
        if (!plan.Preview.CanImport)
        {
            return ServiceResult<CurriculumImportResultDto>.Failure(
                CurriculumErrorCodes.ImportHasInvalidRows,
                $"Tệp có {plan.Preview.Errors.Count} lỗi. Vui lòng sửa tệp và tải lên lại, hệ thống chưa lưu dữ liệu nào.");
        }

        foreach (var planned in plan.Chapters)
        {
            if (planned.ExistingChapterId is { } existingId)
            {
                foreach (var lesson in planned.Lessons)
                {
                    repository.Add(new Lesson { ChapterId = existingId, Number = lesson.Number, Title = lesson.Title });
                }

                continue;
            }

            var chapter = new Chapter
            {
                SchoolBranchId = branchId,
                GradeLevelId = planned.GradeLevelId,
                SubjectId = planned.SubjectId,
                Number = planned.Number,
                Title = planned.Title
            };
            foreach (var lesson in planned.Lessons)
            {
                chapter.Lessons.Add(new Lesson { Number = lesson.Number, Title = lesson.Title });
            }

            repository.Add(chapter);
        }

        // One SaveChanges is one transaction: either the whole file is stored or nothing is.
        if (await repository.SaveAsync(cancellationToken) == CurriculumSaveStatus.Duplicate)
        {
            return ServiceResult<CurriculumImportResultDto>.Failure(
                CurriculumErrorCodes.ImportConflict,
                "Dữ liệu chương/bài vừa thay đổi nên tệp bị trùng. Vui lòng tải lên lại để kiểm tra.");
        }

        return ServiceResult<CurriculumImportResultDto>.Success(new CurriculumImportResultDto(
            plan.Preview.NewChapterCount, plan.Preview.LessonCount));
    }

    private async Task<ServiceResult<(ulong BranchId, CurriculumImportPlan Plan)>> EvaluateAsync(
        CurriculumActor actor,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken)
    {
        if (await repository.GetActorBranchIdAsync(actor.UserId, cancellationToken) is not { } branchId)
        {
            return BranchRequired<(ulong, CurriculumImportPlan)>();
        }

        if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
            content.Length == 0 ||
            content.Length > MaxFileBytes)
        {
            return ServiceResult<(ulong, CurriculumImportPlan)>.Failure(
                CurriculumErrorCodes.ImportFileInvalid,
                $"Tệp không hợp lệ. Vui lòng chọn tệp .xlsx không quá {MaxFileBytes / (1024 * 1024)} MB và không quá {CurriculumWorkbook.MaxRows} dòng dữ liệu.");
        }

        CurriculumImportSheet sheet;
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            sheet = CurriculumWorkbook.Read(stream);
        }
        catch (CurriculumImportFormatException exception)
        {
            return ServiceResult<(ulong, CurriculumImportPlan)>.Failure(
                CurriculumErrorCodes.ImportFileInvalid, exception.Message);
        }

        var plan = CurriculumImportRules.Evaluate(
            sheet,
            await repository.ListGradesAsync(cancellationToken),
            await repository.ListFieldsAsync(cancellationToken),
            await repository.ListChaptersAsync(branchId, cancellationToken));
        return ServiceResult<(ulong, CurriculumImportPlan)>.Success((branchId, plan));
    }

    private async Task<(uint Number, string Title, Dictionary<string, string[]> Errors)> ValidateChapterAsync(
        SaveChapterRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.GradeLevelId == 0)
        {
            errors["gradeLevelId"] = ["Trường Khối lớp là bắt buộc."];
        }
        else if ((await repository.ListGradesAsync(cancellationToken)).All(grade => grade.Id != request.GradeLevelId))
        {
            errors["gradeLevelId"] = ["Khối lớp không hợp lệ."];
        }

        if (request.FieldId == 0)
        {
            errors["fieldId"] = ["Trường Môn học là bắt buộc."];
        }
        else if ((await repository.ListFieldsAsync(cancellationToken)).All(field => field.Id != request.FieldId))
        {
            errors["fieldId"] = ["Môn học không hợp lệ."];
        }

        var number = CheckNumber(request.Number, "Số chương", errors);
        var title = CheckText(request.Title, "title", "Tên chương", CurriculumImportRules.MaxTitleLength, errors);
        return (number, title, errors);
    }

    private static (uint Number, string Title, Dictionary<string, string[]> Errors) ValidateLesson(
        SaveLessonRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var number = CheckNumber(request.Number, "Số bài", errors);
        var title = CheckText(request.Title, "title", "Tên bài", CurriculumImportRules.MaxTitleLength, errors);
        return (number, title, errors);
    }

    private static uint CheckNumber(int? value, string label, Dictionary<string, string[]> errors)
    {
        if (value is >= 1 and <= CurriculumImportRules.MaxNumber)
        {
            return (uint)value.Value;
        }

        errors["number"] = [$"{label} phải là số nguyên từ 1 đến {CurriculumImportRules.MaxNumber}."];
        return 0;
    }

    private static string CheckText(
        string? value,
        string key,
        string label,
        int maxLength,
        Dictionary<string, string[]> errors)
    {
        var cleaned = CurriculumImportRules.Clean(value);
        if (cleaned.Length == 0)
        {
            errors[key] = [$"Trường {label} là bắt buộc."];
        }
        else if (cleaned.Length > maxLength)
        {
            errors[key] = [$"{label} vượt quá độ dài tối đa {maxLength} ký tự."];
        }

        return cleaned;
    }

    private async Task<ServiceResult<CurriculumChapterRow>> LoadChapterAsync(
        ulong branchId,
        ulong chapterId,
        CancellationToken cancellationToken)
    {
        var row = (await repository.ListChaptersAsync(branchId, cancellationToken))
            .SingleOrDefault(chapter => chapter.Id == chapterId);
        return row is null ? ChapterNotFound<CurriculumChapterRow>() : ServiceResult<CurriculumChapterRow>.Success(row);
    }

    // Reads the row back like the list does, so the in-use flag matches it.
    private async Task<ServiceResult<CurriculumLessonRow>> LoadLessonAsync(
        ulong branchId,
        ulong lessonId,
        CancellationToken cancellationToken)
    {
        var row = (await repository.ListChaptersAsync(branchId, cancellationToken))
            .SelectMany(chapter => chapter.Lessons)
            .SingleOrDefault(lesson => lesson.Id == lessonId);
        return row is null ? LessonNotFound<CurriculumLessonRow>() : ServiceResult<CurriculumLessonRow>.Success(row);
    }

    private static ServiceResult<T> BranchRequired<T>() => ServiceResult<T>.Failure(
        CurriculumErrorCodes.BranchRequired,
        "Tài khoản chưa được gán phân hiệu nên không xem được chương và bài học.");

    private static ServiceResult<T> ChapterNotFound<T>() =>
        ServiceResult<T>.Failure(CurriculumErrorCodes.ChapterNotFound, "Không tìm thấy chương học.");

    private static ServiceResult<T> LessonNotFound<T>() =>
        ServiceResult<T>.Failure(CurriculumErrorCodes.LessonNotFound, "Không tìm thấy bài học.");

    private static ServiceResult<T> ValidationFailure<T>(Dictionary<string, string[]> errors) =>
        ServiceResult<T>.Failure(
            CurriculumErrorCodes.Validation,
            "Vui lòng nhập đầy đủ và đúng các trường bắt buộc.",
            errors);

    private static ServiceResult<CurriculumChapterRow> ChapterDuplicate(uint number, (bool Number, bool Title) conflict) =>
        Duplicate<CurriculumChapterRow>(
            CurriculumErrorCodes.ChapterDuplicate,
            $"Số chương {number} đã tồn tại trong khối lớp và môn học đã chọn.",
            "Tên chương đã tồn tại trong khối lớp và môn học đã chọn.",
            conflict);

    private static ServiceResult<CurriculumLessonRow> LessonDuplicate(uint number, (bool Number, bool Title) conflict) =>
        Duplicate<CurriculumLessonRow>(
            CurriculumErrorCodes.LessonDuplicate,
            $"Số bài {number} đã tồn tại trong chương này.",
            "Tên bài học đã tồn tại trong chương này.",
            conflict);

    // Details name every field that collides. A race the second look no longer sees is reported on the title.
    private static ServiceResult<T> Duplicate<T>(
        string code,
        string numberMessage,
        string titleMessage,
        (bool Number, bool Title) conflict)
    {
        var details = new Dictionary<string, string[]>();
        if (conflict.Number)
        {
            details["number"] = [numberMessage];
        }

        if (conflict.Title || !conflict.Number)
        {
            details["title"] = [titleMessage];
        }

        return ServiceResult<T>.Failure(code, conflict.Number ? numberMessage : titleMessage, details);
    }
}
