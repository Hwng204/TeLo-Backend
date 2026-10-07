using Domain.Entities.Organization;

namespace Domain.Entities.Notification;

public sealed class EmailTemplate
{
    public ulong Id { get; set; }
    public ulong SchoolId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string EventCode { get; set; } = "";
    public string Status { get; set; } = "ACTIVE";
    public uint Version { get; set; } = 1;
    public DateTime? UsedAt { get; set; }
    public School School { get; set; } = null!;
    public ICollection<EmailTemplateVersion> Revisions { get; set; } = new List<EmailTemplateVersion>();
}
