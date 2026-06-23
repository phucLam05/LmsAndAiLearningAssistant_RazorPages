using DAL.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace DAL.Providers
{
    /// <summary>
    /// Implements SMTP email sending with safety logging falls backs.
    /// </summary>
    public class SmtpProvider : IEmailSenderProvider
    {
        private readonly ILogger<SmtpProvider> _logger;
        private readonly string _host;
        private readonly int _port;
        private readonly string _username;
        private readonly string _password;
        private readonly bool _enableSsl;
        private readonly string _fromAddress;

        public SmtpProvider(IConfiguration configuration, ILogger<SmtpProvider> logger)
        {
            _logger = logger;
            _host = configuration["SmtpSettings:Host"] ?? string.Empty;
            _port = int.TryParse(configuration["SmtpSettings:Port"], out var port) ? port : 587;
            _username = configuration["SmtpSettings:Username"] ?? string.Empty;
            _password = configuration["SmtpSettings:Password"] ?? string.Empty;
            _enableSsl = bool.TryParse(configuration["SmtpSettings:EnableSsl"], out var ssl) && ssl;
            _fromAddress = configuration["SmtpSettings:FromAddress"] ?? "no-reply@lmsai.com";
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(_host))
            {
                _logger.LogWarning("SMTP Host is not configured. Fallback email printed to console.");
                Console.WriteLine("\n==================================================");
                Console.WriteLine($"[EMAIL FALLBACK] Sent to: {to}");
                Console.WriteLine($"Subject: {subject}");
                Console.WriteLine($"Body: {body}");
                Console.WriteLine("==================================================\n");
                return;
            }

            try
            {
                using var mailMessage = new MailMessage(_fromAddress, to, subject, body);
                using var smtpClient = new SmtpClient(_host, _port);
                
                if (!string.IsNullOrWhiteSpace(_username))
                {
                    smtpClient.Credentials = new NetworkCredential(_username, _password);
                }
                
                smtpClient.EnableSsl = _enableSsl;
                await smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation("Email successfully sent to {Email}", to);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}. Logging fallback to console.", to);
                Console.WriteLine($"[EMAIL SEND ERROR] Fallback details for {to}:\nSubject: {subject}\nBody: {body}");
                throw;
            }
        }
    }
}
