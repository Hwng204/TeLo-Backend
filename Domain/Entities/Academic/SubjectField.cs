namespace Domain.Entities.Academic;

// Lĩnh vực trong một môn (ví dụ Toán: "Số và phép tính", "Hình học và đo lường").
public sealed class SubjectField
{
    public ulong Id { get; set; }
    public ulong SubjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "ACTIVE";

    public Subject Subject { get; set; } = null!;
}
