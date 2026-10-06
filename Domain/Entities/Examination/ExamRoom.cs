using Domain.Entities.Organization;

namespace Domain.Entities.Examination;

public sealed class ExamRoom
{
    public ulong Id { get; set; }
    public ulong ExamId { get; set; }
    public ulong RoomId { get; set; }
    public string Code { get; set; } = null!;
    public uint CandidateLimit { get; set; }

    public Exam Exam { get; set; } = null!;
    public Room Room { get; set; } = null!;
    public ICollection<SessionRoom> Sessions { get; set; } = new List<SessionRoom>();
}
