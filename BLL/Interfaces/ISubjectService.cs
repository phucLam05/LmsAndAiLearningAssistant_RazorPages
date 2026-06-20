using Core.DTOs.Common;
using Core.DTOs.Subject;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    /// <summary>
    /// Business logic interface for Subject management.
    /// Methods are grouped by the role that is allowed to call them.
    /// </summary>
    public interface ISubjectService
    {
        // ── ADMIN ────────────────────────────────────────────────────────────────

        /// <summary>Get all subjects regardless of status. Admin only.</summary>
        Task<IEnumerable<SubjectDto>> GetAllSubjectsAsync();

        /// <summary>Get a single subject by ID. Admin only.</summary>
        Task<SubjectDto?> GetSubjectByIdAsync(Guid id);

        /// <summary>Create a new subject with an optional lecturer assignment.</summary>
        Task<(bool Success, string? Error)> CreateSubjectAsync(CreateSubjectDto dto);

        /// <summary>Update subject name, description, lecturer, and status.</summary>
        Task<(bool Success, string? Error)> UpdateSubjectAsync(UpdateSubjectDto dto);

        /// <summary>Soft-delete or permanently remove a subject.</summary>
        Task<(bool Success, string? Error)> DeleteSubjectAsync(Guid id);

        /// <summary>
        /// Assign exactly 1 lecturer to a subject (or remove assignment by passing null).
        /// This is the core constraint: 1 Subject → max 1 Lecturer.
        /// </summary>
        Task<(bool Success, string? Error)> AssignLecturerAsync(AssignLecturerDto dto);

        // ── LECTURER ─────────────────────────────────────────────────────────────

        /// <summary>Get only subjects assigned to a specific lecturer.</summary>
        Task<IEnumerable<SubjectDto>> GetSubjectsByLecturerAsync(Guid lecturerId);

        // ── STUDENT ──────────────────────────────────────────────────────────────

        /// <summary>Get all active subjects available for students to select for chat.</summary>
        Task<IEnumerable<SubjectDto>> GetActiveSubjectsAsync();

        Task<PagedResult<SubjectDto>> GetPagedAllSubjectsAsync(string? search, string? status, int pageIndex, int pageSize);
        Task<PagedResult<SubjectDto>> GetPagedSubjectsByLecturerAsync(Guid lecturerId, string? search, int pageIndex, int pageSize);
        Task<PagedResult<SubjectDto>> GetPagedActiveSubjectsAsync(string? search, int pageIndex, int pageSize);
    }
}
