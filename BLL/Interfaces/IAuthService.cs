using Core.DTOs.Auth;
using Core.Entities;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    /// <summary>
    /// Service interface for authentication operations.
    /// Handles business logic for user registration and login.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Registers a new user based on the provided DTO.
        /// </summary>
        /// <param name="registerDto">The registration data.</param>
        /// <returns>A tuple containing a boolean success flag and an error message if any.</returns>
        Task<(bool Success, string ErrorMessage)> RegisterAsync(RegisterDTO registerDto);

        /// <summary>
        /// Authenticates a user based on login credentials.
        /// </summary>
        /// <param name="loginDto">The login data.</param>
        /// <returns>The authenticated user if successful, otherwise null.</returns>
        Task<User?> LoginAsync(LoginDTO loginDto);
    }
}
