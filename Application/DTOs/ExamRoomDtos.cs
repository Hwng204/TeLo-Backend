namespace Application.DTOs;

public sealed record CreateExamRoomRequest(
    string Code,
    ulong RoomId,
    uint CandidateLimit);

public sealed record UpdateExamRoomRequest(
    string Code,
    ulong RoomId,
    uint CandidateLimit);

public sealed record CreateExamRoomsRequest(
    uint RoomCount,
    uint CandidateLimit,
    string CodePrefix,
    IReadOnlyList<ulong> RoomIds);

public sealed record ExamRoomPhysicalRoomSummary(
    ulong Id,
    string Code,
    string Name,
    string RoomType,
    string Status);

public sealed record ExamRoomOptionDto(
    ulong Id,
    string Code,
    string Name,
    string RoomType,
    string Status);

public sealed record ExamRoomDto(
    ulong Id,
    ulong ExamId,
    string Code,
    ExamRoomPhysicalRoomSummary Room,
    uint CandidateLimit,
    string Status,
    int SessionCount);

public sealed record ExamRoomPlanStudentDto(
    ulong Id,
    string Code,
    string FullName);

public sealed record ExamRoomPlanRoomDto(
    int RoomNumber,
    IReadOnlyList<ExamRoomPlanStudentDto> Students);

public sealed record ExamRoomPlanSessionDto(
    int SessionNumber,
    IReadOnlyList<ExamRoomPlanRoomDto> Rooms);

public sealed record ExamRoomPlanDto(
    int TotalStudents,
    int RoomCount,
    uint CandidateLimit,
    int CapacityPerSession,
    int RequiredSessionCount,
    IReadOnlyList<ExamRoomPlanSessionDto> Sessions);
