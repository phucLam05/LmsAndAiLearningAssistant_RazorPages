using System.ComponentModel.DataAnnotations;

namespace Core.DTOs.Auth
{
    /// <summary>
    /// Data Transfer Object for user registration.
    /// Carries user input from the Presentation Layer to the Business Access Layer.
    /// </summary>
    public class RegisterDTO
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
        /// Requires a minimum length to ensure basic security.
        /// </summary>
        [Required(ErrorMessage = "Password is required.")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters long.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Password confirmation.
        /// Must match the Password field.
        /// </summary>
        [Required(ErrorMessage = "Confirm Password is required.")]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;

        /// <summary>
        /// User's full name.
        /// </summary>
        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters.")]
        public string FullName { get; set; } = string.Empty;
    }
}
