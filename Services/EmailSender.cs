using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace e_Commerce_application.Services
{
    public class EmailOptions
    {
        public string? Host { get; set; }
        public int Port { get; set; } = 587;
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? From { get; set; }
        public string? FromName { get; set; }
        public bool EnableSsl { get; set; } = true;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
    }

    // Sends mail through any SMTP server (Gmail, Zoho, Brevo, Mailgun...). Used for order
    // updates and for account emails such as password resets. Does nothing until configured.
    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailOptions _options;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (!_options.IsConfigured)
            {
                _logger.LogInformation("Email not configured; skipped \"{Subject}\" to {Email}", subject, email);
                return;
            }

            using var message = new MailMessage
            {
                From = new MailAddress(_options.From!, _options.FromName),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };
            message.To.Add(email);

            using var client = new SmtpClient(_options.Host, _options.Port) { EnableSsl = _options.EnableSsl };
            if (!string.IsNullOrEmpty(_options.Username))
            {
                client.Credentials = new NetworkCredential(_options.Username, _options.Password);
            }
            await client.SendMailAsync(message);
        }
    }
}
