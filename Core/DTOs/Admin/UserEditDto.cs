using System;
using System.ComponentModel.DataAnnotations;
using Core.Entities;

namespace Core.DTOs.Admin
{
    public class UserEditDto
    {
        [Required]
        public Guid Id { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public UserRole Role { get; set; }

        [Required]
        public UserStatus Status { get; set; }

        /// <summary>User code (student or lecturer ID). Optional — null means no change.</summary>
        public string? UserCode { get; set; }
    }
}
