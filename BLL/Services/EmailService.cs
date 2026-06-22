using BLL.Interfaces;
using DAL.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace BLL.Services
{
    /// <summary>
    /// Implements SMTP email sending with safety logging falls backs.
    /// </summary>
    public class EmailService : IEmailService
    {
        private readonly IEmailSenderProvider _emailSenderProvider;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IEmailSenderProvider emailSenderProvider, ILogger<EmailService> logger)
        {
            _emailSenderProvider = emailSenderProvider;
            _logger = logger;
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

            try
            {
                await _emailSenderProvider.SendEmailAsync(email, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send account setup email to {Email}", email);
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

            try
            {
                await _emailSenderProvider.SendEmailAsync(email, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password-reset notification to {Email}", email);
            }
        }
    }
}
