using Application.Common;
using Application.DTOs;
using Application.Mappings;
using Application.Services.Interface;
using Domain.Entities.Examination;
using Infrastructure.UnitOfWork;

namespace Application.Services.Implement;

public sealed class ExamRoomService(IUnitOfWork unitOfWork) : IExamRoomService
{
    public async Task<ServiceResult<IReadOnlyList<ExamRoomDto>>> ListAsync(
        ulong examId,
        CancellationToken cancellationToken)
    {
        if (!await unitOfWork.ExamRooms.ExamExistsAsync(examId, cancellationToken))
        {
            return ExamNotFound<IReadOnlyList<ExamRoomDto>>();
        }

        var items = await unitOfWork.ExamRooms.ListByExamAsync(examId, cancellationToken);
        return ServiceResult<IReadOnlyList<ExamRoomDto>>.Success(
            items.Select(item => item.ToDto()).ToArray());
    }

    public async Task<ServiceResult<IReadOnlyList<ExamRoomOptionDto>>> ListOptionsAsync(
        ulong examId,
        CancellationToken cancellationToken)
    {
        if (!await unitOfWork.ExamRooms.ExamExistsAsync(examId, cancellationToken))
        {
            return ExamNotFound<IReadOnlyList<ExamRoomOptionDto>>();
        }

        var items = await unitOfWork.ExamRooms.ListRoomOptionsAsync(examId, cancellationToken);
        return ServiceResult<IReadOnlyList<ExamRoomOptionDto>>.Success(items
            .Select(item => new ExamRoomOptionDto(
                item.Id,
                item.Code,
                item.Name,
                item.RoomType,
                item.Status))
            .ToArray());
    }

    public async Task<ServiceResult<ExamRoomDto>> GetByIdAsync(
        ulong examId,
        ulong id,
        CancellationToken cancellationToken)
    {
        var detail = await unitOfWork.ExamRooms.GetDetailAsync(examId, id, cancellationToken);
        return detail is null
            ? ExamRoomNotFound<ExamRoomDto>()
            : ServiceResult<ExamRoomDto>.Success(detail.ToDto());
    }

    public async Task<ServiceResult<ExamRoomDto>> CreateAsync(
        ulong examId,
        CreateExamRoomRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ExamRoomValidator.Validate(request);
        if (!validation.IsValid)
        {
            return ValidationFailure<ExamRoomDto>(validation);
        }

        var normalizedCode = NormalizeCode(request.Code);
        var referenceFailure = await ValidateReferencesAsync(examId, request.RoomId, cancellationToken);
        if (referenceFailure is not null)
        {
            return referenceFailure;
        }

        var duplicateFailure = await ValidateDuplicatesAsync(
            examId,
            normalizedCode,
            request.RoomId,
            null,
            cancellationToken);
        if (duplicateFailure is not null)
        {
            return duplicateFailure;
        }

        var examRoom = new ExamRoom
        {
            ExamId = examId,
            RoomId = request.RoomId,
            Code = normalizedCode,
            CandidateLimit = request.CandidateLimit
        };

        await unitOfWork.ExamRooms.AddAsync(examRoom, cancellationToken);
        await unitOfWork.CompleteAsync(cancellationToken);
        return await LoadDetailAsync(examId, examRoom.Id, cancellationToken);
    }

    public async Task<ServiceResult<ExamRoomDto>> UpdateAsync(
        ulong examId,
        ulong id,
        UpdateExamRoomRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ExamRoomValidator.Validate(request);
        if (!validation.IsValid)
        {
            return ValidationFailure<ExamRoomDto>(validation);
        }

        var examRoom = await unitOfWork.ExamRooms.GetByIdAsync(examId, id, cancellationToken);
        if (examRoom is null)
        {
            return ExamRoomNotFound<ExamRoomDto>();
        }

        if (!await unitOfWork.ExamRooms.RoomBelongsToExamBranchAsync(examId, request.RoomId, cancellationToken))
        {
            return PhysicalRoomNotFound<ExamRoomDto>();
        }

        var normalizedCode = NormalizeCode(request.Code);
        var duplicateFailure = await ValidateDuplicatesAsync(
            examId,
            normalizedCode,
            request.RoomId,
            id,
            cancellationToken);
        if (duplicateFailure is not null)
        {
            return duplicateFailure;
        }

        examRoom.RoomId = request.RoomId;
        examRoom.Code = normalizedCode;
        examRoom.CandidateLimit = request.CandidateLimit;

        await unitOfWork.CompleteAsync(cancellationToken);
        return await LoadDetailAsync(examId, id, cancellationToken);
    }

    public async Task<ServiceResult<bool>> DeleteAsync(
        ulong examId,
        ulong id,
        CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            var examRoom = await unitOfWork.ExamRooms.GetForDeleteAsync(
                examId,
                id,
                transactionCancellationToken);
            if (examRoom is null)
            {
                return ExamRoomNotFound<bool>();
            }

            if (await unitOfWork.ExamRooms.HasDependentDataAsync(id, transactionCancellationToken))
            {
                return ServiceResult<bool>.Failure(
                    "EXAM_ROOM_HAS_DEPENDENCIES",
                    "Không thể xóa phòng thi đã được gán vào ca thi.");
            }

            unitOfWork.ExamRooms.Delete(examRoom);
            await unitOfWork.CompleteAsync(transactionCancellationToken);
            return ServiceResult<bool>.Success(true);
        }, cancellationToken);

    private async Task<ServiceResult<ExamRoomDto>?> ValidateReferencesAsync(
        ulong examId,
        ulong roomId,
        CancellationToken cancellationToken)
    {
        if (!await unitOfWork.ExamRooms.ExamExistsAsync(examId, cancellationToken))
        {
            return ExamNotFound<ExamRoomDto>();
        }

        return !await unitOfWork.ExamRooms.RoomBelongsToExamBranchAsync(examId, roomId, cancellationToken)
            ? PhysicalRoomNotFound<ExamRoomDto>()
            : null;
    }

    private async Task<ServiceResult<ExamRoomDto>?> ValidateDuplicatesAsync(
        ulong examId,
        string code,
        ulong roomId,
        ulong? excludedId,
        CancellationToken cancellationToken)
    {
        if (await unitOfWork.ExamRooms.CodeExistsAsync(examId, code, excludedId, cancellationToken))
        {
            return ServiceResult<ExamRoomDto>.Failure(
                "EXAM_ROOM_CODE_ALREADY_EXISTS",
                "Mã phòng thi đã tồn tại trong kỳ thi.");
        }

        return await unitOfWork.ExamRooms.RoomExistsAsync(examId, roomId, excludedId, cancellationToken)
            ? ServiceResult<ExamRoomDto>.Failure(
                "EXAM_ROOM_ALREADY_EXISTS",
                "Phòng học đã được thêm vào kỳ thi.")
            : null;
    }

    private async Task<ServiceResult<ExamRoomDto>> LoadDetailAsync(
        ulong examId,
        ulong id,
        CancellationToken cancellationToken)
    {
        var detail = await unitOfWork.ExamRooms.GetDetailAsync(examId, id, cancellationToken);
        return detail is null
            ? ExamRoomNotFound<ExamRoomDto>()
            : ServiceResult<ExamRoomDto>.Success(detail.ToDto());
    }

    private static string NormalizeCode(string code) => code.Trim().ToLowerInvariant();

    private static ServiceResult<T> ValidationFailure<T>(ExamValidationResult validation) =>
        ServiceResult<T>.Failure(
            "VALIDATION_ERROR",
            "Dữ liệu phòng thi không hợp lệ.",
            validation.Errors);

    private static ServiceResult<T> ExamNotFound<T>() =>
        ServiceResult<T>.Failure("EXAM_NOT_FOUND", "Không tìm thấy kỳ thi.");

    private static ServiceResult<T> PhysicalRoomNotFound<T>() =>
        ServiceResult<T>.Failure(
            "ROOM_NOT_FOUND",
            "Không tìm thấy phòng học thuộc cùng cơ sở với kỳ thi.");

    private static ServiceResult<T> ExamRoomNotFound<T>() =>
        ServiceResult<T>.Failure(
            "EXAM_ROOM_NOT_FOUND",
            "Không tìm thấy phòng thi trong kỳ thi này.");
}
