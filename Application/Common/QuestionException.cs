namespace Application.Common;

public sealed class QuestionException(string code, string message, int status = 422, IReadOnlyDictionary<string, string[]>? details = null)
    : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
    public IReadOnlyDictionary<string, string[]>? Details { get; } = details;
}
