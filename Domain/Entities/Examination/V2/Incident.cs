namespace Domain.Entities.Examination;

// Bảng `incident` của ERD v2: mới có cấu trúc, chủ module kỳ thi viết service sau.
public sealed class Incident
{
    public ulong Id { get; set; }
    public ulong TestResultId { get; set; }
    public ulong IncidentTypeId { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}
