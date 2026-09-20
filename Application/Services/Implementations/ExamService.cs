using Application.Common;
using Application.DTOs;
using Application.Mappings;
using Application.Services.Interface;
using Domain.Entities.Examination;
using Infrastructure.UnitOfWork;

namespace Application.Services.Implement;

public sealed class ExamService(IUnitOfWork unitOfWork) : IExamService
{
    public async Task<ServiceResult<ExamDetailDto>> CreateAsync(
        CreateExamRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ExamValidator.Validate(request);
        if (!validation.IsValid)
        {
            return ValidationFailure<ExamDetailDto>(validation);
        }

        var referenceFailure = await ValidateReferencesAsync(
            request.SemesterId,
            request.SchoolBranchId,
            cancellationToken);
        if (referenceFailure is not null)
        {
            return referenceFailure;
        }

        var exam = new Exam
        {
            SemesterId = request.SemesterId,
            SchoolBranchId = request.SchoolBranchId,
            Name = request.Name.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = ExamStatusCodes.Draft
        };

        await unitOfWork.Exams.AddAsync(exam, cancellationToken);
        await unitOfWork.CompleteAsync(cancellationToken);

        return await LoadDetailAsync(exam.Id, cancellationToken);
    }

    public async Task<ServiceResult<ExamPage>> ListAsync(
        ExamListQuery query,
        CancellationToken cancellationToken)
    {
        var validation = ExamValidator.Validate(query);
        if (!validation.IsValid)
        {
            return ValidationFailure<ExamPage>(validation);
        }

        var filter = query.ToFilter();
        var (items, totalCount) = await unitOfWork.Exams.ListAsync(filter, cancellationToken);
        return ServiceResult<ExamPage>.Success(new ExamPage(
            items.Select(item => item.ToDto()).ToArray(),
            filter.PageNumber,
            filter.PageSize,
            totalCount));
    }

    public async Task<ServiceResult<ExamDetailDto>> GetByIdAsync(
        ulong id,
        CancellationToken cancellationToken) =>
        await LoadDetailAsync(id, cancellationToken);

    public async Task<ServiceResult<ExamDetailDto>> UpdateAsync(
        ulong id,
        UpdateExamRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ExamValidator.Validate(request);
        if (!validation.IsValid)
        {
            return ValidationFailure<ExamDetailDto>(validation);
        }

        var exam = await unitOfWork.Exams.GetByIdAsync(id, cancellationToken);
        if (exam is null)
        {
            return NotFound<ExamDetailDto>();
        }

        var referenceFailure = await ValidateReferencesAsync(
            request.SemesterId,
            request.SchoolBranchId,
            cancellationToken);
        if (referenceFailure is not null)
        {
            return referenceFailure;
        }

        exam.SemesterId = request.SemesterId;
        exam.SchoolBranchId = request.SchoolBranchId;
        exam.Name = request.Name.Trim();
        exam.StartDate = request.StartDate;
        exam.EndDate = request.EndDate;
        exam.Status = request.Status.Trim().ToUpperInvariant();

        await unitOfWork.CompleteAsync(cancellationToken);
        return await LoadDetailAsync(id, cancellationToken);
    }

    public async Task<ServiceResult<bool>> DeleteAsync(
        ulong id,
        CancellationToken cancellationToken)
    {
        return await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            var exam = await unitOfWork.Exams.GetForDeleteAsync(id, transactionCancellationToken);
            if (exam is null)
            {
                return NotFound<bool>();
            }

            if (await unitOfWork.Exams.HasDependentDataAsync(id, transactionCancellationToken))
            {
                return ServiceResult<bool>.Failure(
                    "EXAM_HAS_DEPENDENCIES",
                    "Không thể xóa kỳ thi đã có môn thi, phòng thi hoặc giám thị.");
            }

            unitOfWork.Exams.Delete(exam);
            await unitOfWork.CompleteAsync(transactionCancellationToken);
            return ServiceResult<bool>.Success(true);
        }, cancellationToken);
    }

    private async Task<ServiceResult<ExamDetailDto>?> ValidateReferencesAsync(
        ulong semesterId,
        ulong schoolBranchId,
        CancellationToken cancellationToken)
    {
        var references = await unitOfWork.Exams.GetReferenceDataAsync(
            semesterId,
            schoolBranchId,
            cancellationToken);

        if (!references.SemesterExists)
        {
            return ServiceResult<ExamDetailDto>.Failure(
                "SEMESTER_NOT_FOUND",
                "Không tìm thấy học kỳ.");
        }

        return !references.SchoolBranchExists
            ? ServiceResult<ExamDetailDto>.Failure(
                "SCHOOL_BRANCH_NOT_FOUND",
                "Không tìm thấy cơ sở trường.")
            : null;
    }

    private async Task<ServiceResult<ExamDetailDto>> LoadDetailAsync(
        ulong id,
        CancellationToken cancellationToken)
    {
        var detail = await unitOfWork.Exams.GetDetailAsync(id, cancellationToken);
        return detail is null
            ? NotFound<ExamDetailDto>()
            : ServiceResult<ExamDetailDto>.Success(detail.ToDto());
    }

    private static ServiceResult<T> ValidationFailure<T>(ExamValidationResult validation) =>
        ServiceResult<T>.Failure(
            "VALIDATION_ERROR",
            "Dữ liệu kỳ thi không hợp lệ.",
            validation.Errors);

    private static ServiceResult<T> NotFound<T>() =>
        ServiceResult<T>.Failure(
            "EXAM_NOT_FOUND",
            "Không tìm thấy kỳ thi.");
}
