using Core.Entities;
using System;
using System.ComponentModel.DataAnnotations;

namespace Core.DTOs.Subject
{
    /// <summary>
    /// DTO for Admin to update an existing Subject.
    /// SubjectCode is NOT editable after creation to preserve data integrity.
    /// </summary>
    public class UpdateSubjectDto
    {
        [Required]
        public Guid Id { get; set; }

        [Required(ErrorMessage = "Subject name is required.")]
        [MaxLength(255, ErrorMessage = "Subject name must not exceed 255 characters.")]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>SubjectCode is read-only on update but included for view rendering.</summary>
        public string SubjectCode { get; set; } = string.Empty;

        /// <summary>Assign exactly 1 lecturer. Set to null to un-assign.</summary>
        public Guid? LecturerId { get; set; }

        public SubjectStatus Status { get; set; } = SubjectStatus.Active;
    }
}
