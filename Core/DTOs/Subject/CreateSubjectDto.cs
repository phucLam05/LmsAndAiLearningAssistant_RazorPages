using System;
using System.ComponentModel.DataAnnotations;

namespace Core.DTOs.Subject
{
    /// <summary>
    /// DTO for Admin to create a new Subject.
    /// LecturerId is optional — Admin can assign a lecturer later via AssignLecturerDto.
    /// </summary>
    public class CreateSubjectDto
    {
        [Required(ErrorMessage = "Subject code is required.")]
        [MaxLength(50, ErrorMessage = "Subject code must not exceed 50 characters.")]
        public string SubjectCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Subject name is required.")]
        [MaxLength(255, ErrorMessage = "Subject name must not exceed 255 characters.")]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>Optional: assign exactly 1 lecturer at creation time.</summary>
        public Guid? LecturerId { get; set; }
    }
}
