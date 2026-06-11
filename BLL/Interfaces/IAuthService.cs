using Core.DTOs.Auth;
using Core.DTOs.Common;
using Core.Entities;
using System;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    /// <summary>
    /// Service interface for authentication operations.
    /// Handles user registration, login, and first-time account activation.
    /// </summary>
    public interface IAuthService
    {
        Task<(bool Success, string ErrorMessage)> RegisterAsync(RegisterDto registerDto);

        Task<User?> LoginAsync(LoginDto loginDto);

        Task<Result> ActivateAccountAsync(Guid userId, string temporaryPassword, string newPassword);
        string DecryptEmail(string encryptedEmailBase64);
    }
}
