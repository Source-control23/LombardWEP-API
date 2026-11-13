using System.Net;
using System.Net.Mail;

public interface IEmailService
{
    Task<bool> SendEmailAsync(string toEmail, string subject, string bodyHtml);
}

public class EmailService : IEmailService
{
    private readonly SmtpClient _smtpClient;
    private readonly string _fromEmail;
    private readonly string? _toEmail;          // config fallback (kept for safety)
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _fromEmail = config["EmailSettings:FromEmail"];
        _toEmail = config["EmailSettings:ToEmail"];   // fallback only
        _logger = logger;

        _smtpClient = new SmtpClient
        {
            Host = config["EmailSettings:SmtpHost"],
            Port = int.Parse(config["EmailSettings:SmtpPort"]),
            EnableSsl = false,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            //  Credentials = new NetworkCredential(
            //      config["EmailSettings:Username"],
            //      config["EmailSettings:SmtpSecret"]   // read from IIS env var, not appsettings
            //  )
        };
    }

    public async Task<bool> SendEmailAsync(string toEmail, string subject, string bodyHtml)
    {
        try
        {
            _logger.LogInformation("Email method started.");

            // Use the caller-supplied address when available;
            // fall back to the config default so existing flows keep working.
            string recipient = !string.IsNullOrWhiteSpace(toEmail) ? toEmail : _toEmail;

            if (string.IsNullOrWhiteSpace(recipient))
            {
                _logger.LogError("No recipient address available — toEmail param and config ToEmail are both empty.");
                return false;
            }

            var mail = new MailMessage
            {
                From = new MailAddress(_fromEmail),
                Subject = subject,
                Body = bodyHtml,
                IsBodyHtml = true
            };

            mail.To.Add(recipient);

            await _smtpClient.SendMailAsync(mail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending email to {Recipient}", toEmail);
            return false;
        }
    }
}