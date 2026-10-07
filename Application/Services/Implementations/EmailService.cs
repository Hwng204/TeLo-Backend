using System.Net;
using System.Net.Mail;
using Application.Services.Interface;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.Services.Implement;

public sealed class EmailService(ILogger<EmailService> logger, IConfiguration configuration) : IEmailService
{
    // Preserve the existing authentication contract, without logging OTPs or message bodies.
    public async Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        try { await SendCoreAsync(to, subject, body, true, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { logger.LogWarning("Authentication email delivery failed."); }
    }

    // Queue delivery must observe failures; completion means SMTP accepted the mail.
    public Task SendNotificationEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default) =>
        SendCoreAsync(to, subject, body, false, cancellationToken);

    private async Task SendCoreAsync(string to, string subject, string body, bool html, CancellationToken ct)
    {
        var email = configuration["EmailSettings:Email"];
        var password = configuration["EmailSettings:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("EMAIL_NOT_CONFIGURED");
        var host = configuration["EmailSettings:Host"] ?? "smtp.gmail.com";
        var port = int.TryParse(configuration["EmailSettings:Port"], out var configuredPort) ? configuredPort : 587;
        using var client = new SmtpClient(host, port)
        {
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(email, password),
            EnableSsl = true
        };
        using var message = new MailMessage
        {
            From = new MailAddress(email, configuration["EmailSettings:DisplayName"] ?? "TeLo School Management"),
            Subject = subject.Replace('\r', ' ').Replace('\n', ' '),
            Body = body,
            IsBodyHtml = html,
            BodyEncoding = System.Text.Encoding.UTF8,
            SubjectEncoding = System.Text.Encoding.UTF8
        };
        message.To.Add(new MailAddress(to));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        await client.SendMailAsync(message, timeout.Token);
    }
}
