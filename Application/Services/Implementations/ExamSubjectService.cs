using Application.Common;
using Application.DTOs;
using Application.Mappings;
using Application.Services.Interface;
using Domain.Entities.Examination;
using Infrastructure.UnitOfWork;

namespace Application.Services.Implement;

public sealed class ExamSubjectService(IUnitOfWork unitOfWork) : IExamSubjectService
{
    public async Task<ServiceResult<IReadOnlyList<ExamSubjectDto>>> ListAsync(
        ulong examId,
        CancellationToken cancellationToken)
    {
        if (!await unitOfWork.ExamSubjects.ExamExistsAsync(examId, cancellationToken))
        {
            return ExamNotFound<IReadOnlyList<ExamSubjectDto>>();
        }

        var items = await unitOfWork.ExamSubjects.ListByExamAsync(examId, cancellationToken);
        return ServiceResult<IReadOnlyList<ExamSubjectDto>>.Success(
            items.Select(item => item.ToDto()).ToArray());
    }

    public async Task<ServiceResult<ExamSubjectDto>> CreateAsync(
        ulong examId,
        CreateExamSubjectRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ExamSubjectValidator.Validate(request);
        if (!validation.IsValid)
        {
            return ValidationFailure<ExamSubjectDto>(validation);
        }

        var referenceFailure = await ValidateReferencesAsync(examId, request.SubjectId, cancellationToken);
        if (referenceFailure is not null)
        {
            return referenceFailure;
        }

        if (await unitOfWork.ExamSubjects.ExistsAsync(examId, request.SubjectId, null, cancellationToken))
        {
            return Duplicate<ExamSubjectDto>();
        }

        var examSubject = new ExamSubject
        {
            ExamId = examId,
            SubjectId = request.SubjectId,
            DurationMinutes = request.DurationMinutes,
            Status = ExamSubjectStatusCodes.Active
        };

        await unitOfWork.ExamSubjects.AddAsync(examSubject, cancellationToken);
        await unitOfWork.CompleteAsync(cancellationToken);
        return await LoadDetailAsync(examId, examSubject.Id, cancellationToken);
    }

    public async Task<ServiceResult<ExamSubjectDto>> UpdateAsync(
        ulong examId,
        ulong id,
        UpdateExamSubjectRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ExamSubjectValidator.Validate(request);
        if (!validation.IsValid)
        {
            return ValidationFailure<ExamSubjectDto>(validation);
        }

        var examSubject = await unitOfWork.ExamSubjects.GetByIdAsync(examId, id, cancellationToken);
        if (examSubject is null)
        {
            return ExamSubjectNotFound<ExamSubjectDto>();
        }

        if (!await unitOfWork.ExamSubjects.SubjectExistsAsync(request.SubjectId, cancellationToken))
        {
            return SubjectNotFound<ExamSubjectDto>();
        }

        if (await unitOfWork.ExamSubjects.ExistsAsync(examId, request.SubjectId, id, cancellationToken))
        {
            return Duplicate<ExamSubjectDto>();
        }

        examSubject.SubjectId = request.SubjectId;
        examSubject.DurationMinutes = request.DurationMinutes;
        examSubject.Status = request.Status.Trim().ToUpperInvariant();

        await unitOfWork.CompleteAsync(cancellationToken);
        return await LoadDetailAsync(examId, id, cancellationToken);
    }

    public async Task<ServiceResult<bool>> DeleteAsync(
        ulong examId,
        ulong id,
        CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            var examSubject = await unitOfWork.ExamSubjects.GetForDeleteAsync(
                examId,
                id,
                transactionCancellationToken);
            if (examSubject is null)
            {
                return ExamSubjectNotFound<bool>();
            }

            if (await unitOfWork.ExamSubjects.HasDependentDataAsync(id, transactionCancellationToken))
            {
                return ServiceResult<bool>.Failure(
                    "EXAM_SUBJECT_HAS_DEPENDENCIES",
                    "Không thể xóa môn thi đã có cấu hình khối lớp.");
            }

            unitOfWork.ExamSubjects.Delete(examSubject);
            await unitOfWork.CompleteAsync(transactionCancellationToken);
            return ServiceResult<bool>.Success(true);
        }, cancellationToken);

    private async Task<ServiceResult<ExamSubjectDto>?> ValidateReferencesAsync(
        ulong examId,
        ulong subjectId,
        CancellationToken cancellationToken)
    {
        if (!await unitOfWork.ExamSubjects.ExamExistsAsync(examId, cancellationToken))
        {
            return ExamNotFound<ExamSubjectDto>();
        }

        return !await unitOfWork.ExamSubjects.SubjectExistsAsync(subjectId, cancellationToken)
            ? SubjectNotFound<ExamSubjectDto>()
            : null;
    }

    private async Task<ServiceResult<ExamSubjectDto>> LoadDetailAsync(
        ulong examId,
        ulong id,
        CancellationToken cancellationToken)
    {
        var detail = await unitOfWork.ExamSubjects.GetDetailAsync(examId, id, cancellationToken);
        return detail is null
            ? ExamSubjectNotFound<ExamSubjectDto>()
            : ServiceResult<ExamSubjectDto>.Success(detail.ToDto());
    }

    private static ServiceResult<T> ValidationFailure<T>(ExamValidationResult validation) =>
        ServiceResult<T>.Failure(
            "VALIDATION_ERROR",
            "Dữ liệu môn thi không hợp lệ.",
            validation.Errors);

    private static ServiceResult<T> ExamNotFound<T>() =>
        ServiceResult<T>.Failure("EXAM_NOT_FOUND", "Không tìm thấy kỳ thi.");

    private static ServiceResult<T> SubjectNotFound<T>() =>
        ServiceResult<T>.Failure("SUBJECT_NOT_FOUND", "Không tìm thấy môn học.");

    private static ServiceResult<T> ExamSubjectNotFound<T>() =>
        ServiceResult<T>.Failure("EXAM_SUBJECT_NOT_FOUND", "Không tìm thấy môn thi trong kỳ thi này.");

    private static ServiceResult<T> Duplicate<T>() =>
        ServiceResult<T>.Failure("EXAM_SUBJECT_ALREADY_EXISTS", "Môn học đã được thêm vào kỳ thi.");
}
