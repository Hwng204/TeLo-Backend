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

    public async Task<ServiceResult<ExamRoomPlanDto>> GetPlanAsync(
        ulong examId,
        uint candidateLimit,
        CancellationToken cancellationToken)
    {
        if (candidateLimit == 0 || candidateLimit > int.MaxValue / 2)
        {
            return ServiceResult<ExamRoomPlanDto>.Failure(
                "VALIDATION_ERROR",
                "Số học sinh mỗi phòng phải là số nguyên lớn hơn 0.",
                new Dictionary<string, string[]>
                {
                    ["candidateLimit"] = ["Số học sinh mỗi phòng không hợp lệ."]
                });
        }

        var roster = await unitOfWork.ExamRooms.GetCandidateRosterAsync(examId, cancellationToken);
        if (roster is null)
        {
            return ExamNotFound<ExamRoomPlanDto>();
        }

        if (!roster.HasGradeLevel)
        {
            return ServiceResult<ExamRoomPlanDto>.Failure(
                "EXAM_GRADE_NOT_CONFIGURED",
                "Kỳ thi chưa được cấu hình khối thi.");
        }

        const int roomCount = 2;
        var limit = (int)candidateLimit;
        var capacityPerSession = limit * roomCount;
        var requiredSessionCount = roster.Students.Count == 0
            ? 0
            : (int)Math.Ceiling((double)roster.Students.Count / capacityPerSession);
        var sessions = new List<ExamRoomPlanSessionDto>(requiredSessionCount);

        for (var sessionIndex = 0; sessionIndex < requiredSessionCount; sessionIndex++)
        {
            var plannedRooms = new List<ExamRoomPlanRoomDto>(roomCount);
            for (var roomIndex = 0; roomIndex < roomCount; roomIndex++)
            {
                var offset = sessionIndex * capacityPerSession + roomIndex * limit;
                var students = roster.Students
                    .Skip(offset)
                    .Take(limit)
                    .Select(student => new ExamRoomPlanStudentDto(
                        student.Id,
                        student.Code,
                        student.FullName))
                    .ToArray();
                plannedRooms.Add(new ExamRoomPlanRoomDto(roomIndex + 1, students));
            }

            sessions.Add(new ExamRoomPlanSessionDto(sessionIndex + 1, plannedRooms));
        }

        return ServiceResult<ExamRoomPlanDto>.Success(new ExamRoomPlanDto(
            roster.Students.Count,
            roomCount,
            candidateLimit,
            capacityPerSession,
            requiredSessionCount,
            sessions));
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

    public async Task<ServiceResult<IReadOnlyList<ExamRoomDto>>> CreateBatchAsync(
        ulong examId,
        CreateExamRoomsRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ExamRoomValidator.Validate(request);
        if (!validation.IsValid)
        {
            return ValidationFailure<IReadOnlyList<ExamRoomDto>>(validation);
        }

        if (!await unitOfWork.ExamRooms.ExamExistsAsync(examId, cancellationToken))
        {
            return ExamNotFound<IReadOnlyList<ExamRoomDto>>();
        }

        return await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            foreach (var roomId in request.RoomIds)
            {
                if (!await unitOfWork.ExamRooms.RoomBelongsToExamBranchAsync(
                    examId,
                    roomId,
                    transactionCancellationToken))
                {
                    return PhysicalRoomNotFound<IReadOnlyList<ExamRoomDto>>();
                }

                if (await unitOfWork.ExamRooms.RoomExistsAsync(
                    examId,
                    roomId,
                    null,
                    transactionCancellationToken))
                {
                    return ServiceResult<IReadOnlyList<ExamRoomDto>>.Failure(
                        "EXAM_ROOM_ALREADY_EXISTS",
                        "Phòng máy đã được thêm vào kỳ thi.");
                }
            }

            var prefix = NormalizePrefix(request.CodePrefix);
            var nextNumber = 1;
            var newRooms = new List<ExamRoom>(request.RoomIds.Count);
            foreach (var roomId in request.RoomIds)
            {
                string code;
                do
                {
                    code = $"{prefix}{nextNumber:00}";
                    nextNumber++;
                }
                while (await unitOfWork.ExamRooms.CodeExistsAsync(
                    examId,
                    code,
                    null,
                    transactionCancellationToken));

                var examRoom = new ExamRoom
                {
                    ExamId = examId,
                    RoomId = roomId,
                    Code = code,
                    CandidateLimit = request.CandidateLimit
                };
                await unitOfWork.ExamRooms.AddAsync(examRoom, transactionCancellationToken);
                newRooms.Add(examRoom);
            }

            await unitOfWork.CompleteAsync(transactionCancellationToken);
            var result = new List<ExamRoomDto>(newRooms.Count);
            foreach (var room in newRooms)
            {
                var detail = await unitOfWork.ExamRooms.GetDetailAsync(
                    examId,
                    room.Id,
                    transactionCancellationToken);
                if (detail is null)
                {
                    return ServiceResult<IReadOnlyList<ExamRoomDto>>.Failure(
                        "EXAM_ROOM_NOT_FOUND",
                        "Không thể tải phòng thi vừa tạo.");
                }
                result.Add(detail.ToDto());
            }

            return ServiceResult<IReadOnlyList<ExamRoomDto>>.Success(result);
        }, cancellationToken);
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

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private static string NormalizePrefix(string prefix) => prefix.Trim().ToUpperInvariant();

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
