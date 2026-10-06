namespace Application.DTOs;

public sealed record CreateExamRoomRequest(
    string Code,
    ulong RoomId,
    uint CandidateLimit);

public sealed record UpdateExamRoomRequest(
    string Code,
    ulong RoomId,
    uint CandidateLimit);

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
    int SessionCount);
