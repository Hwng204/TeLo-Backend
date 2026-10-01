namespace Domain.Entities.Identity;

// Scalar identifiers deliberately have no cascading FKs: deleting an unused catalog
// entry or an account must not delete the record of who changed its authorization.
public sealed class IdentityAudit
{
    public ulong Id { get; set; }
    public ulong ActorUserId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public ulong EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
