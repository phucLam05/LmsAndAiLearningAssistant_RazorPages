using BLL.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace BLL.Services
{
    /// <summary>
    /// Implements SMTP email sending with safety logging falls backs.
    /// </summary>
    public class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;
        private readonly string _host;
        private readonly int _port;
        private readonly string _username;
        private readonly string _password;
        private readonly bool _enableSsl;
        private readonly string _fromAddress;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _logger = logger;
            _host = configuration["SmtpSettings:Host"] ?? string.Empty;
            _port = int.TryParse(configuration["SmtpSettings:Port"], out var port) ? port : 587;
            _username = configuration["SmtpSettings:Username"] ?? string.Empty;
            _password = configuration["SmtpSettings:Password"] ?? string.Empty;
            _enableSsl = bool.TryParse(configuration["SmtpSettings:EnableSsl"], out var ssl) && ssl;
            _fromAddress = configuration["SmtpSettings:FromAddress"] ?? "no-reply@lmsai.com";
        }

        public async Task SendFirstTimeLoginEmailAsync(string email, string fullName, string userCode, string temporaryPassword)
        {
            var subject = "LMS AI Learning Assistant - Your New Account Details";
            var body = $@"
Hello {fullName},

An account has been created for you on the LMS AI Learning Assistant platform.

Below are your login credentials:
- Login Email: {email}
- User Code: {userCode}
- Temporary Password: {temporaryPassword}

To log in:
1. Navigate to the login page.
2. Enter your Email and Temporary Password.
3. You will be prompted to change your password immediately to activate your account.

Best regards,
LMS AI Team
";

            if (string.IsNullOrWhiteSpace(_host))
            {
                // Print to console and log so developers can find credentials locally without a mail server
                _logger.LogWarning("SMTP Host is not configured. Fallback details generated for {Email}: UserCode: {UserCode}, TempPassword: {Password}", email, userCode, temporaryPassword);
                Console.WriteLine("\n==================================================");
                Console.WriteLine($"[EMAIL FALLBACK] Sent to: {email}");
                Console.WriteLine($"User Code: {userCode}");
                Console.WriteLine($"Temporary Password: {temporaryPassword}");
                Console.WriteLine("==================================================\n");
                return;
            }

            try
            {
                using var mailMessage = new MailMessage(_fromAddress, email, subject, body);
                using var smtpClient = new SmtpClient(_host, _port);
                
                if (!string.IsNullOrWhiteSpace(_username))
                {
                    smtpClient.Credentials = new NetworkCredential(_username, _password);
                }
                
                smtpClient.EnableSsl = _enableSsl;
                await smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation("Account setup notification successfully sent to {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}. Logging fallback credentials to console.", email);
                Console.WriteLine($"[EMAIL SEND ERROR] Fallback details for {email}: UserCode={userCode}, TempPassword={temporaryPassword}");
            }
        }

        public async Task SendPasswordResetNotificationAsync(string email, string fullName, string newPassword)
        {
            var subject = "LMS AI - Mật khẩu của bạn đã được đặt lại";
            var body = $@"Xin chào {fullName},

Mật khẩu tài khoản LMS AI của bạn vừa được quản trị viên đặt lại.

Mật khẩu mới: {newPassword}

Vui lòng đăng nhập và đổi sang mật khẩu cá nhân ngay lập tức.

Trân trọng,
Đội ngũ LMS AI";

            if (string.IsNullOrWhiteSpace(_host))
            {
                _logger.LogWarning("SMTP not configured. Password-reset for {Email}: new password = {Password}", email, newPassword);
                Console.WriteLine($"[EMAIL FALLBACK - Password reset] {email} -> {newPassword}");
                return;
            }

            try
            {
                using var mail = new MailMessage(_fromAddress, email, subject, body);
                using var smtp = new SmtpClient(_host, _port);
                if (!string.IsNullOrWhiteSpace(_username))
                    smtp.Credentials = new NetworkCredential(_username, _password);
                smtp.EnableSsl = _enableSsl;
                await smtp.SendMailAsync(mail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password-reset notification to {Email}. Fallback: {Password}", email, newPassword);
                Console.WriteLine($"[EMAIL SEND ERROR - Password reset] {email} -> {newPassword}");
            }
        }
    }
}
