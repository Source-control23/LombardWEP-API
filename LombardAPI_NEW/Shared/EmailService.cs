using System.Net;
using System.Net.Mail;

public interface IEmailService
{
    Task<bool> SendEmailAsync(string toEmail, string subject, string bodyHtml);

    /// <summary>
    /// Sends to dynamically resolved recipients with optional CC.
    /// Unlike the single-recipient overload, this NEVER falls back to the static config address.
    /// </summary>
    Task<bool> SendEmailAsync(IEnumerable<string> toEmails, IEnumerable<string>? ccEmails, string subject, string bodyHtml);
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

    public async Task<bool> SendEmailAsync(IEnumerable<string> toEmails, IEnumerable<string>? ccEmails, string subject, string bodyHtml)
    {
        try
        {
            var to = (toEmails ?? Enumerable.Empty<string>())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (to.Count == 0)
            {
                _logger.LogError("No dynamic recipient address supplied. Email not sent.");
                return false;
            }

            // CC must not repeat a To address.
            var cc = (ccEmails ?? Enumerable.Empty<string>())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(e => !to.Contains(e, StringComparer.OrdinalIgnoreCase))
                .ToList();

            // Prevent header injection through the subject line.
            string safeSubject = (subject ?? string.Empty).Replace("\r", " ").Replace("\n", " ");

            using var mail = new MailMessage
            {
                From = new MailAddress(_fromEmail),
                Subject = safeSubject,
                Body = bodyHtml,
                IsBodyHtml = true
            };

            // MailAddress validates syntax and throws FormatException on malformed input.
            foreach (var address in to) mail.To.Add(new MailAddress(address));
            foreach (var address in cc) mail.CC.Add(new MailAddress(address));

            _logger.LogInformation("Sending email. To count: {ToCount}, CC count: {CcCount}", to.Count, cc.Count);

            await _smtpClient.SendMailAsync(mail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending email to dynamic recipients.");
            return false;
        }
    }
}