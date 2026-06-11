using System.ComponentModel.DataAnnotations;

namespace Core.DTOs.Auth
{
    /// <summary>
    /// Data Transfer Object for user login.
    /// Carries user login credentials from the Presentation Layer to the Business Access Layer.
    /// </summary>
    public class LoginDto
    {
        /// <summary>
        /// User's email address.
        /// Must be a valid email format and is required.
        /// </summary>
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// User's password.
        /// Required for login.
        /// </summary>
        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
