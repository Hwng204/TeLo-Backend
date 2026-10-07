using Application.DTOs;
using Infrastructure.Repositories.Interface;

namespace Application.Mappings;

public static class ExamRoomMappingExtensions
{
    public static ExamRoomDto ToDto(this ExamRoomData item) =>
        new(
            item.Id,
            item.ExamId,
            item.Code,
            new ExamRoomPhysicalRoomSummary(
                item.RoomId,
                item.RoomCode,
                item.RoomName,
                item.RoomType,
                item.RoomStatus),
            item.CandidateLimit,
            item.SessionCount);
}
