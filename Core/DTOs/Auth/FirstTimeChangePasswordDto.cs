using System;
using System.ComponentModel.DataAnnotations;

namespace Core.DTOs.Auth
{
    public class FirstTimeChangePasswordDto
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string TemporaryPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "New password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
