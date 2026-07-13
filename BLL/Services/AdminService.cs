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
        private readonly IChatSessionRepository _chatSessionRepository;
        private readonly IChatMessageRepository _chatMessageRepository;

        public AdminService(
            IUserRepository userRepository,
            IDocumentRepository documentRepository,
            IDocumentChunkRepository documentChunkRepository,
            ISubjectRepository subjectRepository,
            IChatSessionRepository chatSessionRepository,
            IChatMessageRepository chatMessageRepository)
        {
            _userRepository = userRepository;
            _documentRepository = documentRepository;
            _documentChunkRepository = documentChunkRepository;
            _subjectRepository = subjectRepository;
            _chatSessionRepository = chatSessionRepository;
            _chatMessageRepository = chatMessageRepository;
        }

        public async Task<bool> ChangeUserRoleAsync(Guid userId, UserRole newRole)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;
            user.Role = newRole;
            await _userRepository.UpdateAsync(user);
            return true;
        }

        public async Task<IEnumerable<User>> GetAllUsersAsync()
            => await _userRepository.GetAllUsersAsync();

        public async Task<PagedResult<User>> GetPagedUsersAsync(string? search, string? role, int pageIndex, int pageSize)
        {
            UserRole? parsedRole = null;
            if (!string.IsNullOrWhiteSpace(role) && Enum.TryParse<UserRole>(role, true, out var r))
                parsedRole = r;

            var total = await _userRepository.CountUsersAsync(search, parsedRole, status: null);
            var items = await _userRepository.QueryUsersAsync(search, parsedRole, status: null, pageIndex, pageSize);

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
            // Parallel data fetch
            var allUsers = (await _userRepository.GetAllUsersAsync()).ToList();
            var allSubjects = (await _subjectRepository.GetAllAsync()).ToList();
            var allDocuments = (await _documentRepository.GetAllWithDetailsAsync()).ToList();
            var totalChunks = await _documentChunkRepository.CountAllAsync();

            // Chat stats
            var totalChatSessions = await _chatSessionRepository.CountAllAsync();
            var totalChatMessages = await _chatMessageRepository.CountAllAsync();
            var allSessions = (await _chatSessionRepository.GetAllAsync()).ToList();
            var tokenBreakdown = await _chatMessageRepository.GetTokenBreakdownAsync();
            var subjectMsgStats = await _chatMessageRepository.GetStatsGroupedBySubjectAsync();

            var today = DateTime.UtcNow.Date;
            var last7Days = today.AddDays(-7);
            var last30Days = today.AddDays(-30);

            var stats = new DashboardStatsDto
            {
                // Users
                TotalUsers = allUsers.Count,
                TotalStudents = allUsers.Count(u => u.Role == UserRole.Student),
                TotalLecturers = allUsers.Count(u => u.Role == UserRole.Lecturer),
                TotalAdmins = allUsers.Count(u => u.Role == UserRole.Admin),
                ActiveUsers = allUsers.Count(u => u.Status == UserStatus.Active),
                InactiveUsers = allUsers.Count(u => u.Status == UserStatus.Inactive),
                NewUsersLast7Days = allUsers.Count(u => u.CreatedAt >= last7Days),

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
                NewDocumentsLast7Days = allDocuments.Count(d => d.CreatedAt >= last7Days),

                // Chat
                TotalChatSessions = totalChatSessions,
                TotalChatMessages = totalChatMessages,
                TotalChatSessionsLast7Days = allSessions.Count(s => s.CreatedAt >= last7Days),
                TotalChatSessionsLast30Days = allSessions.Count(s => s.CreatedAt >= last30Days),
                AvgMessagesPerSession = totalChatSessions > 0
                    ? Math.Round((double)totalChatMessages / totalChatSessions, 1) : 0,

                // Tokens
                TotalTokensConsumed = tokenBreakdown.PromptTokens + tokenBreakdown.CompletionTokens,
                PromptTokensTotal = tokenBreakdown.PromptTokens,
                CompletionTokensTotal = tokenBreakdown.CompletionTokens,

                // Roles distribution (for donut chart)
                UsersByRole = new List<RoleDistributionDto>
                {
                    new() { Role = "Student",  Count = allUsers.Count(u => u.Role == UserRole.Student) },
                    new() { Role = "Lecturer", Count = allUsers.Count(u => u.Role == UserRole.Lecturer) },
                    new() { Role = "Admin",    Count = allUsers.Count(u => u.Role == UserRole.Admin) },
                },
            };

            // ── Last 14 days activity (users + docs + chat sessions) ──
            for (int i = 13; i >= 0; i--)
            {
                var day = today.AddDays(-i);
                var next = day.AddDays(1);
                stats.Last14DaysActivity.Add(new DailyActivityDto
                {
                    Day = day,
                    NewUsers = allUsers.Count(u => u.CreatedAt >= day && u.CreatedAt < next),
                    NewDocuments = allDocuments.Count(d => d.CreatedAt >= day && d.CreatedAt < next),
                    NewChatSessions = allSessions.Count(s => s.CreatedAt >= day && s.CreatedAt < next),
                });
            }

            // ── Top subjects by document count ──
            foreach (var s in allSubjects
                .Select(s => new { Subject = s, Documents = allDocuments.Where(d => d.SubjectId == s.Id).ToList() })
                .OrderByDescending(x => x.Documents.Count)
                .ThenByDescending(x => x.Documents.Sum(d => d.FileSize))
                .Take(5))
            {
                stats.TopSubjects.Add(new TopSubjectDto
                {
                    Id = s.Subject.Id,
                    SubjectCode = s.Subject.SubjectCode,
                    Name = s.Subject.Name,
                    DocumentCount = s.Documents.Count,
                    StorageBytes = s.Documents.Sum(d => d.FileSize),
                    ChunkCount = 0,
                });
            }

            // ── Top subjects by chat session count ──
            stats.TopSubjectsByChat = allSessions
                .GroupBy(s => s.SubjectId ?? Guid.Empty)
                .Select(g =>
                {
                    var subj = allSubjects.FirstOrDefault(s => s.Id == g.Key);
                    return new SubjectChatRankDto
                    {
                        SubjectId = g.Key,
                        SubjectCode = subj?.SubjectCode ?? "?",
                        Name = subj?.Name ?? "Unknown",
                        ChatCount = g.Count(),
                    };
                })
                .OrderByDescending(x => x.ChatCount)
                .Take(8)
                .ToList();

            // ── Detailed per-subject stats ──
            var sessionsBySubject = allSessions
                .Where(s => s.SubjectId.HasValue)
                .GroupBy(s => s.SubjectId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            var msgStatsBySubject = subjectMsgStats.ToDictionary(s => s.SubjectId);

            stats.SubjectDetailStats = allSubjects
                .Select(s =>
                {
                    var docs = allDocuments.Where(d => d.SubjectId == s.Id).ToList();
                    sessionsBySubject.TryGetValue(s.Id, out var sessionCount);
                    msgStatsBySubject.TryGetValue(s.Id, out var msgStat);
                    return new SubjectDetailStatsDto
                    {
                        SubjectId = s.Id,
                        SubjectCode = s.SubjectCode,
                        Name = s.Name,
                        DocumentCount = docs.Count,
                        StorageBytes = docs.Sum(d => d.FileSize),
                        ChatSessionCount = sessionCount,
                        ChatMessageCount = msgStat?.MessageCount ?? 0,
                        TokensConsumed = (msgStat?.PromptTokens ?? 0) + (msgStat?.CompletionTokens ?? 0),
                    };
                })
                .OrderByDescending(x => x.ChatSessionCount)
                .ThenByDescending(x => x.DocumentCount)
                .ToList();

            return stats;
        }
    }
}
