using Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    /// <summary>
    /// Repository interface for Subject data access operations.
    /// Supports the three role-based access patterns: Admin (all), Lecturer (assigned), Student (active).
    /// </summary>
    public interface ISubjectRepository
    {
        /// <summary>Admin: Get all subjects with their assigned lecturer.</summary>
        Task<IEnumerable<Subject>> GetAllAsync();

        /// <summary>Lecturer: Get only subjects assigned to a specific lecturer.</summary>
        Task<IEnumerable<Subject>> GetByLecturerIdAsync(Guid lecturerId);

        /// <summary>Student: Get all active subjects available for chat selection.</summary>
        Task<IEnumerable<Subject>> GetActiveAsync();

        /// <summary>Get a single subject by ID, including Lecturer navigation property.</summary>
        Task<Subject?> GetByIdAsync(Guid id);

        /// <summary>Create a new subject.</summary>
        Task<Subject> CreateAsync(Subject subject);

        /// <summary>Update an existing subject.</summary>
        Task<Subject> UpdateAsync(Subject subject);

        /// <summary>Delete a subject by ID. Returns false if not found.</summary>
        Task<bool> DeleteAsync(Guid id);

        /// <summary>
        /// Check if a subject code already exists, optionally excluding a specific subject ID
        /// (used for update validation to avoid false positives on self-check).
        /// </summary>
        Task<bool> ExistsAsync(string subjectCode, Guid? excludeId = null);

        Task<(IReadOnlyList<Subject> Items, int TotalCount)> GetPagedAsync(
            string? search, Core.Entities.SubjectStatus? status, int pageIndex, int pageSize);
        Task<(IReadOnlyList<Subject> Items, int TotalCount)> GetPagedByLecturerAsync(
            Guid lecturerId, string? search, int pageIndex, int pageSize);
        Task<(IReadOnlyList<Subject> Items, int TotalCount)> GetPagedActiveAsync(
            string? search, int pageIndex, int pageSize);
    }
}
