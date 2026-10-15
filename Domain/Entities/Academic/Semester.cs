namespace Domain.Entities.Academic;

public sealed class Semester
{
    public ulong Id { get; set; }
    public ulong AcademicYearId { get; set; }
    public byte Order { get; set; }
    public string Name { get; set; } = string.Empty;

    public AcademicYear AcademicYear { get; set; } = null!;

    public void Configure(string name) => Name = name;
}
