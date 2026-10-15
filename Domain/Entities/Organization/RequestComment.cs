namespace Domain.Entities.Organization;

public sealed class RequestComment
{
    public ulong Id { get; set; }
    public ulong RequestId { get; set; }
    public ulong CreateBy { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
