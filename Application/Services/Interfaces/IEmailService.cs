namespace Application.Services.Interface;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
    Task SendNotificationEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}
