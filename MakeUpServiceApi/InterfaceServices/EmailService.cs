using MakeUpServiceApi.Interface;
using System.Net.Mail;

namespace MakeUpServiceApi.InterfaceServices
{
    public class EmailService: IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;
        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }
        public async Task SendEmailAsync(string email, string subject, string body)
        {
            try
            {
                string senderEmail = _config["EmailSettings:SenderEmail"];
                string key = _config["EmailSettings:EmailKey"];
                string smtpServer = _config["EmailSettings:SmtpServer"];
                string senderDisplayName = "MakeUp Service";

                MailAddress fromAddress = new MailAddress(senderEmail, senderDisplayName);
                MailAddress toAddress = new MailAddress(email);

                MailMessage msg = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body,
                    BodyEncoding = System.Text.Encoding.UTF8,
                    IsBodyHtml = true
                };

                SmtpClient client = new SmtpClient(smtpServer, 587)
                {
                    EnableSsl = true,
                    UseDefaultCredentials = false,
                    Credentials = new System.Net.NetworkCredential(senderEmail, key)
                };

                await client.SendMailAsync(msg);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending email to {email}: {ex.Message}");
                throw;
            }
        }
    }
}
