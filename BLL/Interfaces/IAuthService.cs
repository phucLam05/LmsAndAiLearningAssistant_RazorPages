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

        /// <summary>
        /// Login by UserCode (e.g. STU123456) plus password. Used by the LMS UI so users
        /// don't have to remember their email — admins hand out a UserCode when creating accounts.
        /// </summary>
        Task<User?> LoginByCodeAsync(string userCode, string password);

        Task<Result> ActivateAccountAsync(Guid userId, string temporaryPassword, string newPassword);

        /// <summary>
        /// Admin-only: reset a user's password to a new random string and return the plain value
        /// so the admin can communicate it to the user out-of-band.
        /// </summary>
        Task<Result<string>> AdminResetPasswordAsync(Guid userId);

        string DecryptEmail(string encryptedEmailBase64);
    }
}
