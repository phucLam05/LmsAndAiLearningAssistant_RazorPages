using System;
using System.Collections.Generic;

namespace Core.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public string UserCode { get; set; } = string.Empty;
        public string? EmailEncrypt { get; set; }
        public string? EmailHash { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.Student;
        public UserStatus Status { get; set; } = UserStatus.Active;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ICollection<Subject> AssignedSubjects { get; set; } = new List<Subject>();
    }
}
