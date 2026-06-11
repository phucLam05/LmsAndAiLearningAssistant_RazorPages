using System;
using System.ComponentModel.DataAnnotations;

namespace Core.DTOs.Subject
{
    /// <summary>
    /// DTO for Admin to assign (or un-assign) exactly 1 Lecturer to a Subject.
    /// Setting LecturerId to null removes the current assignment.
    /// </summary>
    public class AssignLecturerDto
    {
        [Required]
        public Guid SubjectId { get; set; }

        /// <summary>The Lecturer's UserId. Null = remove assignment.</summary>
        public Guid? LecturerId { get; set; }
    }
}
