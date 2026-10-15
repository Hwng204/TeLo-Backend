namespace Domain.Entities.Organization;

// Table `request`; named UserRequest because `Request` clashes with ControllerBase.Request.
public sealed class UserRequest
{
    public ulong Id { get; set; }
    public ulong RequestProcessId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public ulong CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public ulong BranchId { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
