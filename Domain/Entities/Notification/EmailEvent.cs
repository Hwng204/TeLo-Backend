using Domain.Entities.Organization;

namespace Domain.Entities.Notification;

public sealed class EmailEvent
{
    public ulong Id { get; set; }
    public ulong? SchoolId { get; set; }
    public ulong ScopeKey { get; private set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string TriggerKind { get; set; } = "MANUAL";
    public string Status { get; set; } = "ACTIVE";
    public string VariablesJson { get; set; } = "[]";
    public uint Version { get; set; } = 1;
    public DateTime? UsedAt { get; set; }
    public School? School { get; set; }
}
