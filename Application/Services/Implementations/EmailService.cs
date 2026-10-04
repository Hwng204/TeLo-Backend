using System.Net;
using System.Net.Mail;
using Application.Services.Interface;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.Services.Implement;

public sealed class EmailService(ILogger<EmailService> logger, IConfiguration configuration) : IEmailService
{
    public async Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        try
        {
            var host = configuration["EmailSettings:Host"] ?? "smtp.gmail.com";
            var port = int.Parse(configuration["EmailSettings:Port"] ?? "587");
            var email = configuration["EmailSettings:Email"];
            var password = configuration["EmailSettings:Password"];
            var displayName = configuration["EmailSettings:DisplayName"] ?? "TeLo School Management";

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                logger.LogWarning("Chưa cấu hình EmailSettings trong appsettings.json. Không thể gửi mail tới {To}", to);
                
                // Fallback to dummy logging if not configured
                logger.LogInformation("DUMMY EMAIL SERVICE - TO: {Email}, SUBJECT: {Subject}, BODY: {Body}", to, subject, body);
                return;
            }

            using var client = new SmtpClient(host, port);
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(email, password);
            client.EnableSsl = true;

            var mailMessage = new MailMessage
            {
                From = new MailAddress(email, displayName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            mailMessage.To.Add(to);

            await client.SendMailAsync(mailMessage, cancellationToken);
            logger.LogInformation("Đã gửi email thành công tới {To}", to);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi gửi email tới {To}", to);
        }
    }
}
