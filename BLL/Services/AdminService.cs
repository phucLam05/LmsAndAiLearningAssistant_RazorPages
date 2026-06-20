using BLL.Interfaces;
using Core.DTOs.Admin;
using Core.DTOs.Common;
using Core.Entities;
using DAL.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services
{
    public class AdminService : IAdminService
    {
        private readonly IUserRepository _userRepository;
        private readonly IDocumentRepository _documentRepository;
        private readonly IDocumentChunkRepository _documentChunkRepository;
        private readonly ISubjectRepository _subjectRepository;

        public AdminService(
            IUserRepository userRepository,
            IDocumentRepository documentRepository,
            IDocumentChunkRepository documentChunkRepository,
            ISubjectRepository subjectRepository)
        {
            _userRepository = userRepository;
            _documentRepository = documentRepository;
            _documentChunkRepository = documentChunkRepository;
            _subjectRepository = subjectRepository;
        }

        public async Task<bool> ChangeUserRoleAsync(Guid userId, UserRole newRole)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return false;
            }

            user.Role = newRole;
            await _userRepository.UpdateAsync(user);
            return true;
        }

        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            return await _userRepository.GetAllUsersAsync();
        }

        public async Task<PagedResult<User>> GetPagedUsersAsync(string? search, string? role, int pageIndex, int pageSize)
        {
            var q = _userRepository.Query();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                q = q.Where(u =>
                    (u.UserCode != null && u.UserCode.ToLower().Contains(s)) ||
                    (u.FullName != null && u.FullName.ToLower().Contains(s)) ||
                    (u.EmailEncrypt != null && u.EmailEncrypt.ToLower().Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(role) && Enum.TryParse<UserRole>(role, true, out var parsedRole))
            {
                q = q.Where(u => u.Role == parsedRole);
            }

            var total = q.Count();
            var items = q
                .OrderByDescending(u => u.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<User>
            {
                Items = items,
                TotalCount = total,
                PageIndex = pageIndex,
                PageSize = pageSize,
            };
        }

        public async Task<DashboardStatsDto> GetDashboardStatsAsync()
        {
            // Users
            var allUsers = (await _userRepository.GetAllUsersAsync()).ToList();

            // Subjects
            var allSubjects = (await _subjectRepository.GetAllAsync()).ToList();

            // Documents
            var allDocuments = (await _documentRepository.GetAllWithDetailsAsync()).ToList();

            // Chunks
            var totalChunks = await _documentChunkRepository.CountAllAsync();

            var stats = new DashboardStatsDto
            {
                // Users
                TotalUsers = allUsers.Count,
                TotalStudents = allUsers.Count(u => u.Role == UserRole.Student),
                TotalLecturers = allUsers.Count(u => u.Role == UserRole.Lecturer),
                TotalAdmins = allUsers.Count(u => u.Role == UserRole.Admin),
                ActiveUsers = allUsers.Count(u => u.Status == UserStatus.Active),
                InactiveUsers = allUsers.Count(u => u.Status == UserStatus.Inactive),

                // Subjects
                TotalSubjects = allSubjects.Count,
                ActiveSubjects = allSubjects.Count(s => s.Status == SubjectStatus.Active),

                // Documents
                TotalDocuments = allDocuments.Count,
                PendingDocuments = allDocuments.Count(d => d.Status == DocumentStatus.Pending),
                ProcessingDocuments = allDocuments.Count(d => d.Status == DocumentStatus.Processing),
                SuccessDocuments = allDocuments.Count(d => d.Status == DocumentStatus.Success),
                FailedDocuments = allDocuments.Count(d => d.Status == DocumentStatus.Failed),
                TotalStorageUsedBytes = allDocuments.Sum(d => d.FileSize),
                TotalDocumentChunks = totalChunks,

                // Roles distribution (for donut chart)
                UsersByRole = new List<RoleDistributionDto>
                {
                    new() { Role = "Student", Count = allUsers.Count(u => u.Role == UserRole.Student) },
                    new() { Role = "Lecturer", Count = allUsers.Count(u => u.Role == UserRole.Lecturer) },
                    new() { Role = "Admin", Count = allUsers.Count(u => u.Role == UserRole.Admin) },
                },
            };

            // Last 14 days activity (users + docs created)
            var today = DateTime.UtcNow.Date;
            for (int i = 13; i >= 0; i--)
            {
                var day = today.AddDays(-i);
                var next = day.AddDays(1);
                stats.Last14DaysActivity.Add(new DailyActivityDto
                {
                    Day = day,
                    NewUsers = allUsers.Count(u => u.CreatedAt >= day && u.CreatedAt < next),
                    NewDocuments = allDocuments.Count(d => d.CreatedAt >= day && d.CreatedAt < next),
                });
            }

            // Top subjects by document count
            var topSubjects = allSubjects
                .Select(s => new
                {
                    Subject = s,
                    Documents = allDocuments.Where(d => d.SubjectId == s.Id).ToList(),
                })
                .OrderByDescending(x => x.Documents.Count)
                .ThenByDescending(x => x.Documents.Sum(d => d.FileSize))
                .Take(5)
                .ToList();

            foreach (var ts in topSubjects)
            {
                stats.TopSubjects.Add(new TopSubjectDto
                {
                    Id = ts.Subject.Id,
                    SubjectCode = ts.Subject.SubjectCode,
                    Name = ts.Subject.Name,
                    DocumentCount = ts.Documents.Count,
                    StorageBytes = ts.Documents.Sum(d => d.FileSize),
                    ChunkCount = 0, // Could join with chunks if needed; keeping 0 for perf.
                });
            }

            return stats;
        }
    }
}
