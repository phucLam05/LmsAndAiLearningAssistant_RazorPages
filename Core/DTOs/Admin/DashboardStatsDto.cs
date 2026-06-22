using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.DTOs.Admin
{
    public class DashboardStatsDto
    {
        // Headline counts
        public int TotalUsers { get; set; }
        public int TotalStudents { get; set; }
        public int TotalLecturers { get; set; }
        public int TotalAdmins { get; set; }
        public int ActiveUsers { get; set; }
        public int InactiveUsers { get; set; }
        public int NewUsersLast7Days { get; set; }

        public int TotalSubjects { get; set; }
        public int ActiveSubjects { get; set; }

        public int TotalDocuments { get; set; }
        public int PendingDocuments { get; set; }
        public int ProcessingDocuments { get; set; }
        public int SuccessDocuments { get; set; }
        public int FailedDocuments { get; set; }
        public long TotalStorageUsedBytes { get; set; }
        public int TotalDocumentChunks { get; set; }

        // Chat statistics
        public int TotalChatSessions { get; set; }
        public int TotalChatMessages { get; set; }

        // Charts
        public List<DailyActivityDto> Last14DaysActivity { get; set; } = new();
        public List<RoleDistributionDto> UsersByRole { get; set; } = new();
        public List<TopSubjectDto> TopSubjects { get; set; } = new();
        public List<SubjectChatRankDto> TopSubjectsByChat { get; set; } = new();

        // Aliases for the Razor views
        public long TotalTokensConsumed { get; set; }
        public int AdminCount => UsersByRole.FirstOrDefault(r => r.Role == "Admin")?.Count ?? 0;
        public int LecturerCount => UsersByRole.FirstOrDefault(r => r.Role == "Lecturer")?.Count ?? 0;
        public int StudentCount => UsersByRole.FirstOrDefault(r => r.Role == "Student")?.Count ?? 0;
        public List<DailyActivityDto> DailyActivity => Last14DaysActivity;
    }

    public class DailyActivityDto
    {
        public DateTime Day { get; set; }
        public int NewUsers { get; set; }
        public int NewDocuments { get; set; }
        // Aliases for Razor views
        public DateTime Date => Day;
        public int Count => NewUsers + NewDocuments;
    }

    public class RoleDistributionDto
    {
        public string Role { get; set; } = string.Empty;
        int _count;
        public int Count { get => _count; set => _count = value; }
    }

    public class TopSubjectDto
    {
        public Guid Id { get; set; }
        public string SubjectCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int DocumentCount { get; set; }
        public long StorageBytes { get; set; }
        public int ChunkCount { get; set; }
    }

    public class SubjectChatRankDto
    {
        public Guid SubjectId { get; set; }
        public string SubjectCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int ChatCount { get; set; }
    }
}
