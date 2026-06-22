using System.ComponentModel.DataAnnotations;

namespace PL.ViewModels.Auth
{
    /// <summary>
    /// View model for the login form in the Presentation Layer.
    /// </summary>
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
