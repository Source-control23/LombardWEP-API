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

    public EmailService(IConfiguration config)
    {
        _fromEmail = config["EmailSettings:FromEmail"];

        _smtpClient = new SmtpClient
        {
            Host = config["EmailSettings:SmtpHost"],
            Port = int.Parse(config["EmailSettings:SmtpPort"]),
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(
        config["EmailSettings:Username"],
        config["EmailSettings:Password"]
    )
        };

    }

    public async Task<bool> SendEmailAsync(string toEmail, string subject, string bodyHtml)
    {
        try
        {
            var mail = new MailMessage
            {
                From = new MailAddress(_fromEmail),
                Subject = subject,
                Body = bodyHtml,
                IsBodyHtml = true
            };

            mail.To.Add(await GetEmailAddress());

            await _smtpClient.SendMailAsync(mail);
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }

    }

    public async Task<string> GetEmailAddress()
    {
        return "ayush.kamble@cdpindia.com";
    }
}
