using Core.Entities;
using System;

namespace Core.DTOs.Subject
{
    /// <summary>
    /// Read-only DTO used to display subject information in views.
    /// Flattens the Lecturer navigation property into a single LecturerName string.
    /// </summary>
    public class SubjectDto
    {
        public Guid Id { get; set; }
        public string SubjectCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid? LecturerId { get; set; }
        public string? LecturerName { get; set; }
        public SubjectStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
