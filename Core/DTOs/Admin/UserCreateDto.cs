using System.ComponentModel.DataAnnotations;
using Core.Entities;

namespace Core.DTOs.Admin
{
    public class UserCreateDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public UserRole Role { get; set; } = UserRole.Student;

        [Required]
        [StringLength(50)]
        public string UserCode { get; set; } = string.Empty;
    }
}
