using LombardAPI_NEW.Services;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

public interface IEmailService
{
    Task<bool> SendEmailAsync(string toEmail, string subject, string bodyHtml);
}

public class EmailService : IEmailService
{
    private readonly SmtpClient _smtpClient;
    private readonly string _fromEmail;
    private readonly string? _toEmail;
    private readonly ILogger<EmailService> _logger;
    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _fromEmail = config["EmailSettings:FromEmail"];
        _toEmail = config["EmailSettings:ToEmail"];
        _logger = logger;
        _smtpClient = new SmtpClient
        {
            Host = config["EmailSettings:SmtpHost"],
            Port = int.Parse(config["EmailSettings:SmtpPort"]),
            EnableSsl = false,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            //        Credentials = new NetworkCredential(
            //    config["EmailSettings:Username"],
            //    config["EmailSettings:SmtpSecret"]
            //)
        };

    }
    public async Task<bool> SendEmailAsync(string toEmail, string subject, string bodyHtml)
    {
        try
        {
            _logger.LogInformation("Email Method started.");
            var mail = new MailMessage
            {
                From = new MailAddress(_fromEmail),
                Subject = subject,
                Body = bodyHtml,
                IsBodyHtml = true
            };

            mail.To.Add(_toEmail);

            await _smtpClient.SendMailAsync(mail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Eror while sending email : {ex}");
            return false;
        }
    }
}