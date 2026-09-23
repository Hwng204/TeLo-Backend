namespace Domain.Entities.Identity;

public sealed class TeacherManagementException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
