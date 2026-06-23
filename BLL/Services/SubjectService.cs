using BLL.Interfaces;
using Core.DTOs.Common;
using Core.DTOs.Subject;
using Core.Entities;
using DAL.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services
{
    /// <summary>
    /// Business logic implementation for Subject management.
    /// Enforces the 1-lecturer-per-subject constraint and role-based data access.
    /// </summary>
    public class SubjectService : ISubjectService
    {
        private readonly ISubjectRepository _subjectRepo;

        public SubjectService(ISubjectRepository subjectRepo)
        {
            _subjectRepo = subjectRepo;
        }

        // ── ADMIN ────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<IEnumerable<SubjectDto>> GetAllSubjectsAsync()
        {
            var subjects = await _subjectRepo.GetAllAsync();
            return subjects.Select(MapToDto);
        }

        /// <inheritdoc/>
        public async Task<SubjectDto?> GetSubjectByIdAsync(Guid id)
        {
            var subject = await _subjectRepo.GetByIdAsync(id);
            return subject == null ? null : MapToDto(subject);
        }

        /// <inheritdoc/>
        public async Task<(bool Success, string? Error)> CreateSubjectAsync(CreateSubjectDto dto)
        {
            // Validate unique subject code
            if (await _subjectRepo.ExistsAsync(dto.SubjectCode))
                return (false, $"Subject code '{dto.SubjectCode}' already exists.");

            var subject = new Subject
            {
                Id = Guid.NewGuid(),
                SubjectCode = dto.SubjectCode.Trim().ToUpper(),
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim(),
                LecturerId = dto.LecturerId,   // 1 lecturer or null
                Status = SubjectStatus.Active
            };

            await _subjectRepo.CreateAsync(subject);
            return (true, null);
        }

        /// <inheritdoc/>
        public async Task<(bool Success, string? Error)> UpdateSubjectAsync(UpdateSubjectDto dto)
        {
            var subject = await _subjectRepo.GetByIdAsync(dto.Id);
            if (subject == null)
                return (false, "Subject not found.");

            subject.Name = dto.Name.Trim();
            subject.Description = dto.Description?.Trim();
            subject.LecturerId = dto.LecturerId;   // enforce exactly 1 or null
            subject.Status = dto.Status;

            await _subjectRepo.UpdateAsync(subject);
            return (true, null);
        }

        /// <inheritdoc/>
        public async Task<(bool Success, string? Error)> DeleteSubjectAsync(Guid id)
        {
            var deleted = await _subjectRepo.DeleteAsync(id);
            return deleted
                ? (true, null)
                : (false, "Subject not found.");
        }

        /// <inheritdoc/>
        public async Task<(bool Success, string? Error)> AssignLecturerAsync(AssignLecturerDto dto)
        {
            var subject = await _subjectRepo.GetByIdAsync(dto.SubjectId);
            if (subject == null)
                return (false, "Subject not found.");

            // Core business rule: exactly 1 lecturer per subject (or null to remove)
            subject.LecturerId = dto.LecturerId;

            await _subjectRepo.UpdateAsync(subject);
            return (true, null);
        }

        // ── LECTURER ─────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<IEnumerable<SubjectDto>> GetSubjectsByLecturerAsync(Guid lecturerId)
        {
            var subjects = await _subjectRepo.GetByLecturerIdAsync(lecturerId);
            return subjects.Select(MapToDto);
        }

        // ── STUDENT ──────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<IEnumerable<SubjectDto>> GetActiveSubjectsAsync()
        {
            var subjects = await _subjectRepo.GetActiveAsync();
            return subjects.Select(MapToDto);
        }

        public async Task<PagedResult<SubjectDto>> GetPagedAllSubjectsAsync(string? search, string? status, int pageIndex, int pageSize)
        {
            var statusEnum = !string.IsNullOrWhiteSpace(status) && Enum.TryParse<SubjectStatus>(status, true, out var s)
                ? s : (SubjectStatus?)null;
            var (items, total) = await _subjectRepo.GetPagedAsync(search, statusEnum, pageIndex, pageSize);
            return new PagedResult<SubjectDto>
            {
                Items = items.Select(MapToDto).ToList(),
                TotalCount = total, PageIndex = pageIndex, PageSize = pageSize
            };
        }

        public async Task<PagedResult<SubjectDto>> GetPagedSubjectsByLecturerAsync(Guid lecturerId, string? search, int pageIndex, int pageSize)
        {
            var (items, total) = await _subjectRepo.GetPagedByLecturerAsync(lecturerId, search, pageIndex, pageSize);
            return new PagedResult<SubjectDto>
            {
                Items = items.Select(MapToDto).ToList(),
                TotalCount = total, PageIndex = pageIndex, PageSize = pageSize
            };
        }

        public async Task<PagedResult<SubjectDto>> GetPagedActiveSubjectsAsync(string? search, int pageIndex, int pageSize)
        {
            var (items, total) = await _subjectRepo.GetPagedActiveAsync(search, pageIndex, pageSize);
            return new PagedResult<SubjectDto>
            {
                Items = items.Select(MapToDto).ToList(),
                TotalCount = total, PageIndex = pageIndex, PageSize = pageSize
            };
        }

        // ── HELPERS ──────────────────────────────────────────────────────────────

        private static SubjectDto MapToDto(Subject s) => new SubjectDto
        {
            Id = s.Id,
            SubjectCode = s.SubjectCode,
            Name = s.Name,
            Description = s.Description,
            LecturerId = s.LecturerId,
            LecturerName = s.Lecturer?.FullName,
            Status = s.Status,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        };
    }
}
