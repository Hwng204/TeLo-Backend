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
    public const string ChapterInUse = "CHAPTER_IN_USE";
    public const string LessonInUse = "LESSON_IN_USE";
    public const string ChapterScopeLocked = "CHAPTER_SCOPE_LOCKED";
    public const string ImportFileInvalid = "IMPORT_FILE_INVALID";
    public const string ImportHasInvalidRows = "IMPORT_HAS_INVALID_ROWS";
    public const string ImportConflict = "IMPORT_CONFLICT";
}

public sealed class CurriculumService(ICurriculumRepository repository) : ICurriculumService
{
    public const int MaxFileBytes = 5 * 1024 * 1024;
    private const string TemplateFileName = "mau-nhap-chuong-bai.xlsx";

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

        var (code, title, errors) = await ValidateChapterAsync(request, cancellationToken);
        if (errors.Count > 0)
        {
            return ValidationFailure<CurriculumChapterRow>(errors);
        }

        var conflict = await repository.FindChapterConflictAsync(
            branchId, request.GradeLevelId, request.FieldId, code, title, null, cancellationToken);
        if (conflict.CodeTaken || conflict.TitleTaken)
        {
            return ChapterDuplicate(conflict);
        }

        var chapter = new Chapter
        {
            SchoolBranchId = branchId,
            GradeLevelId = request.GradeLevelId,
            FieldId = request.FieldId,
            Code = code,
            Title = title,
            SortOrder = await repository.NextChapterSortOrderAsync(branchId, request.GradeLevelId, cancellationToken)
        };
        repository.Add(chapter);
        if (await repository.SaveAsync(cancellationToken) == CurriculumSaveStatus.Duplicate)
        {
            return ChapterDuplicate((true, false));
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

        var (code, title, errors) = await ValidateChapterAsync(request, cancellationToken);
        if (errors.Count > 0)
        {
            return ValidationFailure<CurriculumChapterRow>(errors);
        }

        // Matrices and question tasks pick lessons by grade and subject, so a chapter in use keeps both.
        var scopeChanged = chapter.GradeLevelId != request.GradeLevelId || chapter.FieldId != request.FieldId;
        if (scopeChanged && await repository.IsChapterInUseAsync(chapterId, cancellationToken))
        {
            return ServiceResult<CurriculumChapterRow>.Failure(
                CurriculumErrorCodes.ChapterScopeLocked,
                "Không thể đổi khối lớp hoặc lĩnh vực vì chương học đã được sử dụng.");
        }

        var conflict = await repository.FindChapterConflictAsync(
            branchId, request.GradeLevelId, request.FieldId, code, title, chapterId, cancellationToken);
        if (conflict.CodeTaken || conflict.TitleTaken)
        {
            return ChapterDuplicate(conflict);
        }

        chapter.GradeLevelId = request.GradeLevelId;
        chapter.FieldId = request.FieldId;
        chapter.Code = code;
        chapter.Title = title;
        if (await repository.SaveAsync(cancellationToken) == CurriculumSaveStatus.Duplicate)
        {
            return ChapterDuplicate((true, false));
        }

        return await LoadChapterAsync(branchId, chapterId, cancellationToken);
    }

    public async Task<ServiceResult<bool>> DeleteChapterAsync(
        CurriculumActor actor,
        ulong chapterId,
        CancellationToken cancellationToken)
    {
        if (await repository.GetActorBranchIdAsync(actor.UserId, cancellationToken) is not { } branchId)
        {
            return BranchRequired<bool>();
        }

        var chapter = await repository.GetChapterAsync(branchId, chapterId, cancellationToken);
        if (chapter is null)
        {
            return ChapterNotFound<bool>();
        }

        if (await repository.IsChapterInUseAsync(chapterId, cancellationToken))
        {
            return ChapterInUse();
        }

        // Its lessons go with it (cascade); a foreign key still stops the delete if one became used meanwhile.
        repository.Remove(chapter);
        return await repository.SaveAsync(cancellationToken) == CurriculumSaveStatus.InUse
            ? ChapterInUse()
            : ServiceResult<bool>.Success(true);
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

        var (code, title, errors) = ValidateLesson(request);
        if (errors.Count > 0)
        {
            return ValidationFailure<CurriculumLessonRow>(errors);
        }

        var conflict = await repository.FindLessonConflictAsync(chapterId, code, title, null, cancellationToken);
        if (conflict.CodeTaken || conflict.TitleTaken)
        {
            return LessonDuplicate(conflict);
        }

        var lesson = new Lesson
        {
            ChapterId = chapterId,
            Code = code,
            Title = title,
            SortOrder = await repository.NextLessonSortOrderAsync(chapterId, cancellationToken)
        };
        repository.Add(lesson);
        if (await repository.SaveAsync(cancellationToken) == CurriculumSaveStatus.Duplicate)
        {
            return LessonDuplicate((true, false));
        }

        return ServiceResult<CurriculumLessonRow>.Success(ToRow(lesson, false));
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

        var (code, title, errors) = ValidateLesson(request);
        if (errors.Count > 0)
        {
            return ValidationFailure<CurriculumLessonRow>(errors);
        }

        var conflict = await repository.FindLessonConflictAsync(
            lesson.ChapterId, code, title, lessonId, cancellationToken);
        if (conflict.CodeTaken || conflict.TitleTaken)
        {
            return LessonDuplicate(conflict);
        }

        lesson.Code = code;
        lesson.Title = title;
        if (await repository.SaveAsync(cancellationToken) == CurriculumSaveStatus.Duplicate)
        {
            return LessonDuplicate((true, false));
        }

        return ServiceResult<CurriculumLessonRow>.Success(
            ToRow(lesson, await repository.IsLessonInUseAsync(lessonId, cancellationToken)));
    }

    public async Task<ServiceResult<bool>> DeleteLessonAsync(
        CurriculumActor actor,
        ulong lessonId,
        CancellationToken cancellationToken)
    {
        if (await repository.GetActorBranchIdAsync(actor.UserId, cancellationToken) is not { } branchId)
        {
            return BranchRequired<bool>();
        }

        var lesson = await repository.GetLessonAsync(branchId, lessonId, cancellationToken);
        if (lesson is null)
        {
            return LessonNotFound<bool>();
        }

        if (await repository.IsLessonInUseAsync(lessonId, cancellationToken))
        {
            return LessonInUse();
        }

        repository.Remove(lesson);
        return await repository.SaveAsync(cancellationToken) == CurriculumSaveStatus.InUse
            ? LessonInUse()
            : ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<CurriculumFile>> BuildTemplateAsync(CancellationToken cancellationToken)
    {
        var grades = await repository.ListGradesAsync(cancellationToken);
        var fields = await repository.ListFieldsAsync(cancellationToken);
        var content = CurriculumWorkbook.CreateTemplate(
            grades.Select(grade => grade.Name).ToArray(),
            fields.Select(field => field.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
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

        var nextChapterOrder = new Dictionary<ulong, uint>();
        foreach (var planned in plan.Chapters)
        {
            if (planned.ExistingChapterId is { } existingId)
            {
                var order = await repository.NextLessonSortOrderAsync(existingId, cancellationToken);
                foreach (var lesson in planned.Lessons)
                {
                    repository.Add(new Lesson
                    {
                        ChapterId = existingId,
                        Code = lesson.Code,
                        Title = lesson.Title,
                        SortOrder = order++
                    });
                }

                continue;
            }

            if (!nextChapterOrder.TryGetValue(planned.GradeLevelId, out var chapterOrder))
            {
                chapterOrder = await repository.NextChapterSortOrderAsync(
                    branchId, planned.GradeLevelId, cancellationToken);
            }

            nextChapterOrder[planned.GradeLevelId] = chapterOrder + 1;
            var chapter = new Chapter
            {
                SchoolBranchId = branchId,
                GradeLevelId = planned.GradeLevelId,
                FieldId = planned.FieldId,
                Code = planned.Code,
                Title = planned.Title,
                SortOrder = chapterOrder
            };
            uint lessonOrder = 1;
            foreach (var lesson in planned.Lessons)
            {
                chapter.Lessons.Add(new Lesson { Code = lesson.Code, Title = lesson.Title, SortOrder = lessonOrder++ });
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

        IReadOnlyList<CurriculumImportRawRow> rows;
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            rows = CurriculumWorkbook.Read(stream);
        }
        catch (CurriculumImportFormatException exception)
        {
            return ServiceResult<(ulong, CurriculumImportPlan)>.Failure(
                CurriculumErrorCodes.ImportFileInvalid, exception.Message);
        }

        var plan = CurriculumImportRules.Evaluate(
            rows,
            await repository.ListGradesAsync(cancellationToken),
            await repository.ListFieldsAsync(cancellationToken),
            await repository.ListChaptersAsync(branchId, cancellationToken));
        return ServiceResult<(ulong, CurriculumImportPlan)>.Success((branchId, plan));
    }

    private async Task<(string Code, string Title, Dictionary<string, string[]> Errors)> ValidateChapterAsync(
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
            errors["fieldId"] = ["Trường Lĩnh vực là bắt buộc."];
        }
        else if ((await repository.ListFieldsAsync(cancellationToken)).All(field => field.Id != request.FieldId))
        {
            errors["fieldId"] = ["Lĩnh vực không hợp lệ."];
        }

        var code = CheckText(request.Code, "code", "Mã chương", CurriculumImportRules.MaxCodeLength, errors);
        var title = CheckText(request.Title, "title", "Tên chương", CurriculumImportRules.MaxTitleLength, errors);
        return (code, title, errors);
    }

    private static (string Code, string Title, Dictionary<string, string[]> Errors) ValidateLesson(
        SaveLessonRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var code = CheckText(request.Code, "code", "Mã bài", CurriculumImportRules.MaxCodeLength, errors);
        var title = CheckText(request.Title, "title", "Tên bài", CurriculumImportRules.MaxTitleLength, errors);
        return (code, title, errors);
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

    private static CurriculumLessonRow ToRow(Lesson lesson, bool inUse) =>
        new(lesson.Id, lesson.ChapterId, lesson.Code, lesson.Title, lesson.SortOrder, inUse);

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

    private static ServiceResult<CurriculumChapterRow> ChapterDuplicate((bool CodeTaken, bool TitleTaken) conflict)
    {
        var details = new Dictionary<string, string[]>();
        if (conflict.CodeTaken)
        {
            details["code"] = ["Mã chương đã tồn tại trong khối lớp và lĩnh vực đã chọn."];
        }

        if (conflict.TitleTaken)
        {
            details["title"] = ["Tên chương đã tồn tại trong khối lớp và lĩnh vực đã chọn."];
        }

        return ServiceResult<CurriculumChapterRow>.Failure(
            CurriculumErrorCodes.ChapterDuplicate,
            "Mã chương hoặc tên chương đã tồn tại trong khối lớp và lĩnh vực đã chọn.",
            details);
    }

    private static ServiceResult<CurriculumLessonRow> LessonDuplicate((bool CodeTaken, bool TitleTaken) conflict)
    {
        var details = new Dictionary<string, string[]>();
        if (conflict.CodeTaken)
        {
            details["code"] = ["Mã bài học đã tồn tại trong chương này."];
        }

        if (conflict.TitleTaken)
        {
            details["title"] = ["Tên bài học đã tồn tại trong chương này."];
        }

        return ServiceResult<CurriculumLessonRow>.Failure(
            CurriculumErrorCodes.LessonDuplicate,
            "Mã bài học hoặc tên bài học đã tồn tại trong chương này.",
            details);
    }

    private static ServiceResult<bool> ChapterInUse() => ServiceResult<bool>.Failure(
        CurriculumErrorCodes.ChapterInUse,
        "Chương học đã được sử dụng trong câu hỏi, đề thi, ma trận hoặc nhiệm vụ nên không thể xoá.");

    private static ServiceResult<bool> LessonInUse() => ServiceResult<bool>.Failure(
        CurriculumErrorCodes.LessonInUse,
        "Bài học đã được sử dụng trong câu hỏi, đề thi, ma trận hoặc nhiệm vụ nên không thể xoá.");
}
