using BLL.Services;
using Core.DTOs.Admin;
using Core.Entities;
using DAL.Interfaces;
using Moq;
using System.Linq;

namespace Tests.BLL
{
    /// <summary>
    /// Unit tests for <see cref="AdminService"/>.
    /// Covers role changes, user listing/paging, and dashboard aggregation logic.
    /// </summary>
    public class AdminServiceTests
    {
        // ── Helpers ─────────────────────────────────────────────────────────────

        private static (
            AdminService service,
            Mock<IUserRepository> userRepoMock,
            Mock<IDocumentRepository> docRepoMock,
            Mock<IDocumentChunkRepository> chunkRepoMock,
            Mock<ISubjectRepository> subjectRepoMock,
            Mock<IChatSessionRepository> sessionRepoMock,
            Mock<IChatMessageRepository> msgRepoMock
        ) BuildService()
        {
            var userRepo = new Mock<IUserRepository>();
            var docRepo = new Mock<IDocumentRepository>();
            var chunkRepo = new Mock<IDocumentChunkRepository>();
            var subjectRepo = new Mock<ISubjectRepository>();
            var sessionRepo = new Mock<IChatSessionRepository>();
            var msgRepo = new Mock<IChatMessageRepository>();

            var service = new AdminService(
                userRepo.Object,
                docRepo.Object,
                chunkRepo.Object,
                subjectRepo.Object,
                sessionRepo.Object,
                msgRepo.Object);

            return (service, userRepo, docRepo, chunkRepo, subjectRepo, sessionRepo, msgRepo);
        }

        /// <summary>Creates a User with sensible defaults.</summary>
        private static User MakeUser(
            UserRole role = UserRole.Student,
            UserStatus status = UserStatus.Active,
            DateTime? createdAt = null)
            => new User
            {
                Id = Guid.NewGuid(),
                FullName = $"User_{Guid.NewGuid():N}",
                Role = role,
                Status = status,
                CreatedAt = createdAt ?? DateTime.UtcNow
            };

        /// <summary>Creates a Subject with sensible defaults.</summary>
        private static Subject MakeSubject(SubjectStatus status = SubjectStatus.Active)
            => new Subject
            {
                Id = Guid.NewGuid(),
                SubjectCode = $"S{Guid.NewGuid():N}".Substring(0, 6).ToUpper(),
                Name = "Test Subject",
                Status = status,
                CreatedAt = DateTime.UtcNow
            };

        /// <summary>Creates a Document with sensible defaults.</summary>
        private static Document MakeDocument(
            Guid? subjectId = null,
            DocumentStatus status = DocumentStatus.Success,
            long fileSize = 1024,
            DateTime? createdAt = null)
            => new Document
            {
                Id = Guid.NewGuid(),
                SubjectId = subjectId ?? Guid.NewGuid(),
                FileName = "test.pdf",
                FileUrl = "subject/test/test.pdf",
                FileSize = fileSize,
                Status = status,
                CreatedAt = createdAt ?? DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

        /// <summary>Creates a ChatSession with sensible defaults.</summary>
        private static ChatSession MakeSession(
            Guid? userId = null,
            Guid? subjectId = null,
            DateTime? createdAt = null)
            => new ChatSession
            {
                Id = Guid.NewGuid(),
                UserId = userId ?? Guid.NewGuid(),
                SubjectId = subjectId,
                Title = "Test Session",
                CreatedAt = createdAt ?? DateTime.UtcNow
            };

        // ── ChangeUserRoleAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task ChangeUserRoleAsync_UserNotFound_ReturnsFalse()
        {
            var (service, userRepo, _, _, _, _, _) = BuildService();

            userRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((User?)null);

            var result = await service.ChangeUserRoleAsync(Guid.NewGuid(), UserRole.Lecturer);

            Assert.False(result);
            userRepo.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task ChangeUserRoleAsync_UserFound_ChangesRoleAndReturnsTrue()
        {
            var (service, userRepo, _, _, _, _, _) = BuildService();

            var user = MakeUser(UserRole.Student);
            userRepo
                .Setup(r => r.GetByIdAsync(user.Id))
                .ReturnsAsync(user);

            userRepo
                .Setup(r => r.UpdateAsync(It.IsAny<User>()))
                .Returns(Task.CompletedTask);

            var result = await service.ChangeUserRoleAsync(user.Id, UserRole.Lecturer);

            Assert.True(result);
            Assert.Equal(UserRole.Lecturer, user.Role);
            userRepo.Verify(r => r.UpdateAsync(It.Is<User>(u => u.Role == UserRole.Lecturer)), Times.Once);
        }

        [Fact]
        public async Task ChangeUserRoleAsync_StudentToAdmin_ChangesRoleCorrectly()
        {
            var (service, userRepo, _, _, _, _, _) = BuildService();

            var user = MakeUser(UserRole.Student);
            userRepo
                .Setup(r => r.GetByIdAsync(user.Id))
                .ReturnsAsync(user);

            userRepo
                .Setup(r => r.UpdateAsync(It.IsAny<User>()))
                .Returns(Task.CompletedTask);

            var result = await service.ChangeUserRoleAsync(user.Id, UserRole.Admin);

            Assert.True(result);
            Assert.Equal(UserRole.Admin, user.Role);
        }

        [Fact]
        public async Task ChangeUserRoleAsync_LecturerToStudent_ChangesRoleCorrectly()
        {
            var (service, userRepo, _, _, _, _, _) = BuildService();

            var user = MakeUser(UserRole.Lecturer);
            userRepo
                .Setup(r => r.GetByIdAsync(user.Id))
                .ReturnsAsync(user);

            userRepo
                .Setup(r => r.UpdateAsync(It.IsAny<User>()))
                .Returns(Task.CompletedTask);

            var result = await service.ChangeUserRoleAsync(user.Id, UserRole.Student);

            Assert.True(result);
            Assert.Equal(UserRole.Student, user.Role);
        }

        [Fact]
        public async Task ChangeUserRoleAsync_SameRole_StillUpdatesAndReturnsTrue()
        {
            var (service, userRepo, _, _, _, _, _) = BuildService();

            var user = MakeUser(UserRole.Admin);
            userRepo
                .Setup(r => r.GetByIdAsync(user.Id))
                .ReturnsAsync(user);

            userRepo
                .Setup(r => r.UpdateAsync(It.IsAny<User>()))
                .Returns(Task.CompletedTask);

            var result = await service.ChangeUserRoleAsync(user.Id, UserRole.Admin);

            Assert.True(result);
            userRepo.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Once);
        }

        [Fact]
        public async Task ChangeUserRoleAsync_EmptyGuid_ReturnsFalse()
        {
            var (service, userRepo, _, _, _, _, _) = BuildService();

            userRepo
                .Setup(r => r.GetByIdAsync(Guid.Empty))
                .ReturnsAsync((User?)null);

            var result = await service.ChangeUserRoleAsync(Guid.Empty, UserRole.Admin);

            Assert.False(result);
        }

        // ── GetAllUsersAsync ─────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllUsersAsync_EmptyRepository_ReturnsEmptyList()
        {
            var (service, userRepo, _, _, _, _, _) = BuildService();

            userRepo
                .Setup(r => r.GetAllUsersAsync())
                .ReturnsAsync(new List<User>());

            var result = await service.GetAllUsersAsync();

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAllUsersAsync_MultipleUsers_ReturnsAll()
        {
            var (service, userRepo, _, _, _, _, _) = BuildService();

            var users = new List<User>
            {
                MakeUser(UserRole.Student),
                MakeUser(UserRole.Lecturer),
                MakeUser(UserRole.Admin)
            };

            userRepo
                .Setup(r => r.GetAllUsersAsync())
                .ReturnsAsync(users);

            var result = (await service.GetAllUsersAsync()).ToList();

            Assert.Equal(3, result.Count);
        }

        [Fact]
        public async Task GetAllUsersAsync_ReturnsSameReferencesFromRepo()
        {
            var (service, userRepo, _, _, _, _, _) = BuildService();

            var user = MakeUser();
            var list = new List<User> { user };

            userRepo
                .Setup(r => r.GetAllUsersAsync())
                .ReturnsAsync(list);

            var result = (await service.GetAllUsersAsync()).ToList();

            Assert.Single(result);
            Assert.Equal(user.Id, result[0].Id);
        }

        // ── GetPagedUsersAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task GetPagedUsersAsync_NoFilter_CallsRepoWithNullRole()
        {
            var (service, userRepo, _, _, _, _, _) = BuildService();

            userRepo
                .Setup(r => r.CountUsersAsync(null, null, null))
                .ReturnsAsync(0);

            userRepo
                .Setup(r => r.QueryUsersAsync(null, null, null, 1, 10))
                .ReturnsAsync(new List<User>());

            var result = await service.GetPagedUsersAsync(null, null, 1, 10);

            Assert.Equal(0, result.TotalCount);
            Assert.Empty(result.Items);
            Assert.Equal(1, result.PageIndex);
            Assert.Equal(10, result.PageSize);
        }

        [Fact]
        public async Task GetPagedUsersAsync_ValidRoleString_ParsesAndFilters()
        {
            var (service, userRepo, _, _, _, _, _) = BuildService();

            var students = new List<User> { MakeUser(UserRole.Student), MakeUser(UserRole.Student) };

            userRepo
                .Setup(r => r.CountUsersAsync(null, UserRole.Student, null))
                .ReturnsAsync(2);

            userRepo
                .Setup(r => r.QueryUsersAsync(null, UserRole.Student, null, 1, 10))
                .ReturnsAsync(students);

            var result = await service.GetPagedUsersAsync(null, "Student", 1, 10);

            Assert.Equal(2, result.TotalCount);
            Assert.Equal(2, result.Items.Count());
        }

        [Fact]
        public async Task GetPagedUsersAsync_InvalidRoleString_TreatsAsNoFilter()
        {
            var (service, userRepo, _, _, _, _, _) = BuildService();

            userRepo
                .Setup(r => r.CountUsersAsync(null, null, null))
                .ReturnsAsync(5);

            userRepo
                .Setup(r => r.QueryUsersAsync(null, null, null, 1, 10))
                .ReturnsAsync(new List<User>());

            var result = await service.GetPagedUsersAsync(null, "InvalidRole", 1, 10);

            // Should fall back to null role filter
            userRepo.Verify(r => r.CountUsersAsync(null, null, null), Times.Once);
        }

        [Fact]
        public async Task GetPagedUsersAsync_WithSearch_PassesSearchToRepo()
        {
            var (service, userRepo, _, _, _, _, _) = BuildService();

            userRepo
                .Setup(r => r.CountUsersAsync("john", null, null))
                .ReturnsAsync(1);

            userRepo
                .Setup(r => r.QueryUsersAsync("john", null, null, 1, 5))
                .ReturnsAsync(new List<User> { MakeUser() });

            var result = await service.GetPagedUsersAsync("john", null, 1, 5);

            Assert.Equal(1, result.TotalCount);
            Assert.Equal(1, result.Items.Count());
            Assert.Equal(5, result.PageSize);
        }

        [Fact]
        public async Task GetPagedUsersAsync_LecturerRole_ParsesCorrectly()
        {
            var (service, userRepo, _, _, _, _, _) = BuildService();

            var lecturers = new List<User> { MakeUser(UserRole.Lecturer) };

            userRepo
                .Setup(r => r.CountUsersAsync(null, UserRole.Lecturer, null))
                .ReturnsAsync(1);

            userRepo
                .Setup(r => r.QueryUsersAsync(null, UserRole.Lecturer, null, 1, 10))
                .ReturnsAsync(lecturers);

            var result = await service.GetPagedUsersAsync(null, "Lecturer", 1, 10);

            userRepo.Verify(r => r.QueryUsersAsync(null, UserRole.Lecturer, null, 1, 10), Times.Once);
        }

        [Fact]
        public async Task GetPagedUsersAsync_AdminRole_ParsesCorrectly()
        {
            var (service, userRepo, _, _, _, _, _) = BuildService();

            var admins = new List<User> { MakeUser(UserRole.Admin) };

            userRepo
                .Setup(r => r.CountUsersAsync(null, UserRole.Admin, null))
                .ReturnsAsync(1);

            userRepo
                .Setup(r => r.QueryUsersAsync(null, UserRole.Admin, null, 2, 5))
                .ReturnsAsync(admins);

            var result = await service.GetPagedUsersAsync(null, "Admin", 2, 5);

            Assert.Equal(1, result.TotalCount);
            Assert.Equal(2, result.PageIndex);
        }

        [Fact]
        public async Task GetPagedUsersAsync_EmptyRoleString_TreatsAsNoFilter()
        {
            var (service, userRepo, _, _, _, _, _) = BuildService();

            userRepo
                .Setup(r => r.CountUsersAsync(null, null, null))
                .ReturnsAsync(0);

            userRepo
                .Setup(r => r.QueryUsersAsync(null, null, null, 1, 10))
                .ReturnsAsync(new List<User>());

            await service.GetPagedUsersAsync(null, "", 1, 10);

            userRepo.Verify(r => r.CountUsersAsync(null, null, null), Times.Once);
        }

        [Fact]
        public async Task GetPagedUsersAsync_WhitespaceRoleString_TreatsAsNoFilter()
        {
            var (service, userRepo, _, _, _, _, _) = BuildService();

            userRepo
                .Setup(r => r.CountUsersAsync(null, null, null))
                .ReturnsAsync(0);

            userRepo
                .Setup(r => r.QueryUsersAsync(null, null, null, 1, 10))
                .ReturnsAsync(new List<User>());

            await service.GetPagedUsersAsync(null, "   ", 1, 10);

            userRepo.Verify(r => r.CountUsersAsync(null, null, null), Times.Once);
        }

        // ── GetDashboardStatsAsync — headline counts ─────────────────────────────

        [Fact]
        public async Task GetDashboardStatsAsync_EmptyData_ReturnsZeroCounts()
        {
            var (service, userRepo, docRepo, chunkRepo, subjectRepo, sessionRepo, msgRepo) = BuildService();

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject>());
            docRepo.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(new List<Document>());
            chunkRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ChatSession>());
            msgRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            msgRepo.Setup(r => r.GetTokenBreakdownAsync()).ReturnsAsync((0L, 0L));
            msgRepo.Setup(r => r.GetStatsGroupedBySubjectAsync()).ReturnsAsync(new List<SubjectMessageStatsDto>());

            var stats = await service.GetDashboardStatsAsync();

            Assert.Equal(0, stats.TotalUsers);
            Assert.Equal(0, stats.TotalStudents);
            Assert.Equal(0, stats.TotalLecturers);
            Assert.Equal(0, stats.TotalAdmins);
            Assert.Equal(0, stats.TotalSubjects);
            Assert.Equal(0, stats.TotalDocuments);
            Assert.Equal(0, stats.TotalChatSessions);
            Assert.Equal(0, stats.TotalChatMessages);
        }

        [Fact]
        public async Task GetDashboardStatsAsync_CountsUsersByRole_Correctly()
        {
            var (service, userRepo, docRepo, chunkRepo, subjectRepo, sessionRepo, msgRepo) = BuildService();

            var users = new List<User>
            {
                MakeUser(UserRole.Student),
                MakeUser(UserRole.Student),
                MakeUser(UserRole.Lecturer),
                MakeUser(UserRole.Admin)
            };

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(users);
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject>());
            docRepo.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(new List<Document>());
            chunkRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ChatSession>());
            msgRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            msgRepo.Setup(r => r.GetTokenBreakdownAsync()).ReturnsAsync((0L, 0L));
            msgRepo.Setup(r => r.GetStatsGroupedBySubjectAsync()).ReturnsAsync(new List<SubjectMessageStatsDto>());

            var stats = await service.GetDashboardStatsAsync();

            Assert.Equal(4, stats.TotalUsers);
            Assert.Equal(2, stats.TotalStudents);
            Assert.Equal(1, stats.TotalLecturers);
            Assert.Equal(1, stats.TotalAdmins);
        }

        [Fact]
        public async Task GetDashboardStatsAsync_CountsActiveAndInactiveUsers_Correctly()
        {
            var (service, userRepo, docRepo, chunkRepo, subjectRepo, sessionRepo, msgRepo) = BuildService();

            var users = new List<User>
            {
                MakeUser(status: UserStatus.Active),
                MakeUser(status: UserStatus.Active),
                MakeUser(status: UserStatus.Inactive)
            };

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(users);
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject>());
            docRepo.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(new List<Document>());
            chunkRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ChatSession>());
            msgRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            msgRepo.Setup(r => r.GetTokenBreakdownAsync()).ReturnsAsync((0L, 0L));
            msgRepo.Setup(r => r.GetStatsGroupedBySubjectAsync()).ReturnsAsync(new List<SubjectMessageStatsDto>());

            var stats = await service.GetDashboardStatsAsync();

            Assert.Equal(2, stats.ActiveUsers);
            Assert.Equal(1, stats.InactiveUsers);
        }

        [Fact]
        public async Task GetDashboardStatsAsync_NewUsersLast7Days_CountsCorrectly()
        {
            var (service, userRepo, docRepo, chunkRepo, subjectRepo, sessionRepo, msgRepo) = BuildService();

            var now = DateTime.UtcNow;
            var users = new List<User>
            {
                MakeUser(createdAt: now.AddDays(-1)),   // within 7 days
                MakeUser(createdAt: now.AddDays(-6)),   // within 7 days
                MakeUser(createdAt: now.AddDays(-8)),   // outside 7 days
                MakeUser(createdAt: now.AddDays(-30)),  // outside 7 days
            };

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(users);
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject>());
            docRepo.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(new List<Document>());
            chunkRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ChatSession>());
            msgRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            msgRepo.Setup(r => r.GetTokenBreakdownAsync()).ReturnsAsync((0L, 0L));
            msgRepo.Setup(r => r.GetStatsGroupedBySubjectAsync()).ReturnsAsync(new List<SubjectMessageStatsDto>());

            var stats = await service.GetDashboardStatsAsync();

            Assert.Equal(2, stats.NewUsersLast7Days);
        }

        [Fact]
        public async Task GetDashboardStatsAsync_CountsSubjectsByStatus_Correctly()
        {
            var (service, userRepo, docRepo, chunkRepo, subjectRepo, sessionRepo, msgRepo) = BuildService();

            var subjects = new List<Subject>
            {
                MakeSubject(SubjectStatus.Active),
                MakeSubject(SubjectStatus.Active),
                MakeSubject(SubjectStatus.Inactive)
            };

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(subjects);
            docRepo.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(new List<Document>());
            chunkRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ChatSession>());
            msgRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            msgRepo.Setup(r => r.GetTokenBreakdownAsync()).ReturnsAsync((0L, 0L));
            msgRepo.Setup(r => r.GetStatsGroupedBySubjectAsync()).ReturnsAsync(new List<SubjectMessageStatsDto>());

            var stats = await service.GetDashboardStatsAsync();

            Assert.Equal(3, stats.TotalSubjects);
            Assert.Equal(2, stats.ActiveSubjects);
        }

        [Fact]
        public async Task GetDashboardStatsAsync_CountsDocumentsByStatus_Correctly()
        {
            var (service, userRepo, docRepo, chunkRepo, subjectRepo, sessionRepo, msgRepo) = BuildService();

            var docs = new List<Document>
            {
                MakeDocument(status: DocumentStatus.Pending),
                MakeDocument(status: DocumentStatus.Processing),
                MakeDocument(status: DocumentStatus.Success),
                MakeDocument(status: DocumentStatus.Success),
                MakeDocument(status: DocumentStatus.Failed)
            };

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject>());
            docRepo.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(docs);
            chunkRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(10);
            sessionRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ChatSession>());
            msgRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            msgRepo.Setup(r => r.GetTokenBreakdownAsync()).ReturnsAsync((0L, 0L));
            msgRepo.Setup(r => r.GetStatsGroupedBySubjectAsync()).ReturnsAsync(new List<SubjectMessageStatsDto>());

            var stats = await service.GetDashboardStatsAsync();

            Assert.Equal(5, stats.TotalDocuments);
            Assert.Equal(1, stats.PendingDocuments);
            Assert.Equal(1, stats.ProcessingDocuments);
            Assert.Equal(2, stats.SuccessDocuments);
            Assert.Equal(1, stats.FailedDocuments);
            Assert.Equal(10, stats.TotalDocumentChunks);
        }

        [Fact]
        public async Task GetDashboardStatsAsync_TotalStorageUsed_SumsFileSizes()
        {
            var (service, userRepo, docRepo, chunkRepo, subjectRepo, sessionRepo, msgRepo) = BuildService();

            var docs = new List<Document>
            {
                MakeDocument(fileSize: 1024),
                MakeDocument(fileSize: 2048),
                MakeDocument(fileSize: 512)
            };

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject>());
            docRepo.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(docs);
            chunkRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ChatSession>());
            msgRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            msgRepo.Setup(r => r.GetTokenBreakdownAsync()).ReturnsAsync((0L, 0L));
            msgRepo.Setup(r => r.GetStatsGroupedBySubjectAsync()).ReturnsAsync(new List<SubjectMessageStatsDto>());

            var stats = await service.GetDashboardStatsAsync();

            Assert.Equal(3584L, stats.TotalStorageUsedBytes);
        }

        [Fact]
        public async Task GetDashboardStatsAsync_TokenBreakdown_AddedCorrectly()
        {
            var (service, userRepo, docRepo, chunkRepo, subjectRepo, sessionRepo, msgRepo) = BuildService();

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject>());
            docRepo.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(new List<Document>());
            chunkRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(5);
            sessionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ChatSession>());
            msgRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(25);
            msgRepo.Setup(r => r.GetTokenBreakdownAsync()).ReturnsAsync((1000L, 2000L));
            msgRepo.Setup(r => r.GetStatsGroupedBySubjectAsync()).ReturnsAsync(new List<SubjectMessageStatsDto>());

            var stats = await service.GetDashboardStatsAsync();

            Assert.Equal(1000L, stats.PromptTokensTotal);
            Assert.Equal(2000L, stats.CompletionTokensTotal);
            Assert.Equal(3000L, stats.TotalTokensConsumed);
        }

        [Fact]
        public async Task GetDashboardStatsAsync_AvgMessagesPerSession_CalculatedCorrectly()
        {
            var (service, userRepo, docRepo, chunkRepo, subjectRepo, sessionRepo, msgRepo) = BuildService();

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject>());
            docRepo.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(new List<Document>());
            chunkRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(4);
            sessionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ChatSession>());
            msgRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(10);
            msgRepo.Setup(r => r.GetTokenBreakdownAsync()).ReturnsAsync((0L, 0L));
            msgRepo.Setup(r => r.GetStatsGroupedBySubjectAsync()).ReturnsAsync(new List<SubjectMessageStatsDto>());

            var stats = await service.GetDashboardStatsAsync();

            Assert.Equal(2.5, stats.AvgMessagesPerSession);
        }

        [Fact]
        public async Task GetDashboardStatsAsync_NoSessions_AvgMessagesIsZero()
        {
            var (service, userRepo, docRepo, chunkRepo, subjectRepo, sessionRepo, msgRepo) = BuildService();

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject>());
            docRepo.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(new List<Document>());
            chunkRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ChatSession>());
            msgRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            msgRepo.Setup(r => r.GetTokenBreakdownAsync()).ReturnsAsync((0L, 0L));
            msgRepo.Setup(r => r.GetStatsGroupedBySubjectAsync()).ReturnsAsync(new List<SubjectMessageStatsDto>());

            var stats = await service.GetDashboardStatsAsync();

            Assert.Equal(0, stats.AvgMessagesPerSession);
        }

        [Fact]
        public async Task GetDashboardStatsAsync_Last14DaysActivity_HasExactly14Entries()
        {
            var (service, userRepo, docRepo, chunkRepo, subjectRepo, sessionRepo, msgRepo) = BuildService();

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject>());
            docRepo.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(new List<Document>());
            chunkRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ChatSession>());
            msgRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            msgRepo.Setup(r => r.GetTokenBreakdownAsync()).ReturnsAsync((0L, 0L));
            msgRepo.Setup(r => r.GetStatsGroupedBySubjectAsync()).ReturnsAsync(new List<SubjectMessageStatsDto>());

            var stats = await service.GetDashboardStatsAsync();

            Assert.Equal(14, stats.Last14DaysActivity.Count);
        }

        [Fact]
        public async Task GetDashboardStatsAsync_Last14DaysActivity_DatesAreConsecutive()
        {
            var (service, userRepo, docRepo, chunkRepo, subjectRepo, sessionRepo, msgRepo) = BuildService();

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject>());
            docRepo.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(new List<Document>());
            chunkRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ChatSession>());
            msgRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            msgRepo.Setup(r => r.GetTokenBreakdownAsync()).ReturnsAsync((0L, 0L));
            msgRepo.Setup(r => r.GetStatsGroupedBySubjectAsync()).ReturnsAsync(new List<SubjectMessageStatsDto>());

            var stats = await service.GetDashboardStatsAsync();
            var activity = stats.Last14DaysActivity;

            // Verify dates are consecutive
            for (int i = 1; i < activity.Count; i++)
            {
                Assert.Equal(activity[i - 1].Day.AddDays(1), activity[i].Day);
            }
        }

        [Fact]
        public async Task GetDashboardStatsAsync_Last14DaysActivity_LastEntryIsToday()
        {
            var (service, userRepo, docRepo, chunkRepo, subjectRepo, sessionRepo, msgRepo) = BuildService();

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject>());
            docRepo.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(new List<Document>());
            chunkRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ChatSession>());
            msgRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            msgRepo.Setup(r => r.GetTokenBreakdownAsync()).ReturnsAsync((0L, 0L));
            msgRepo.Setup(r => r.GetStatsGroupedBySubjectAsync()).ReturnsAsync(new List<SubjectMessageStatsDto>());

            var before = DateTime.UtcNow.Date;
            var stats = await service.GetDashboardStatsAsync();
            var after = DateTime.UtcNow.Date;

            var lastDay = stats.Last14DaysActivity.Last().Day;
            Assert.True(lastDay >= before && lastDay <= after);
        }

        [Fact]
        public async Task GetDashboardStatsAsync_UsersByRole_ContainsThreeEntries()
        {
            var (service, userRepo, docRepo, chunkRepo, subjectRepo, sessionRepo, msgRepo) = BuildService();

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject>());
            docRepo.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(new List<Document>());
            chunkRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ChatSession>());
            msgRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            msgRepo.Setup(r => r.GetTokenBreakdownAsync()).ReturnsAsync((0L, 0L));
            msgRepo.Setup(r => r.GetStatsGroupedBySubjectAsync()).ReturnsAsync(new List<SubjectMessageStatsDto>());

            var stats = await service.GetDashboardStatsAsync();

            Assert.Equal(3, stats.UsersByRole.Count);
            Assert.Contains(stats.UsersByRole, r => r.Role == "Student");
            Assert.Contains(stats.UsersByRole, r => r.Role == "Lecturer");
            Assert.Contains(stats.UsersByRole, r => r.Role == "Admin");
        }

        [Fact]
        public async Task GetDashboardStatsAsync_TopSubjects_AtMostFive()
        {
            var (service, userRepo, docRepo, chunkRepo, subjectRepo, sessionRepo, msgRepo) = BuildService();

            var subjects = Enumerable.Range(0, 8).Select(_ => MakeSubject()).ToList();
            var docs = subjects.SelectMany(s => new[]
            {
                MakeDocument(subjectId: s.Id),
                MakeDocument(subjectId: s.Id)
            }).ToList();

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(subjects);
            docRepo.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(docs);
            chunkRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ChatSession>());
            msgRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            msgRepo.Setup(r => r.GetTokenBreakdownAsync()).ReturnsAsync((0L, 0L));
            msgRepo.Setup(r => r.GetStatsGroupedBySubjectAsync()).ReturnsAsync(new List<SubjectMessageStatsDto>());

            var stats = await service.GetDashboardStatsAsync();

            Assert.True(stats.TopSubjects.Count <= 5);
        }

        [Fact]
        public async Task GetDashboardStatsAsync_ChatSessionsLast7And30Days_CountsCorrectly()
        {
            var (service, userRepo, docRepo, chunkRepo, subjectRepo, sessionRepo, msgRepo) = BuildService();

            var now = DateTime.UtcNow;
            var sessions = new List<ChatSession>
            {
                MakeSession(createdAt: now.AddDays(-1)),   // within 7 & 30
                MakeSession(createdAt: now.AddDays(-6)),   // within 7 & 30
                MakeSession(createdAt: now.AddDays(-10)),  // within 30 only
                MakeSession(createdAt: now.AddDays(-35))   // outside both
            };

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject>());
            docRepo.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(new List<Document>());
            chunkRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(4);
            sessionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(sessions);
            msgRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            msgRepo.Setup(r => r.GetTokenBreakdownAsync()).ReturnsAsync((0L, 0L));
            msgRepo.Setup(r => r.GetStatsGroupedBySubjectAsync()).ReturnsAsync(new List<SubjectMessageStatsDto>());

            var stats = await service.GetDashboardStatsAsync();

            Assert.Equal(2, stats.TotalChatSessionsLast7Days);
            Assert.Equal(3, stats.TotalChatSessionsLast30Days);
        }

        [Fact]
        public async Task GetDashboardStatsAsync_TopSubjectsByChat_GroupsSessionsBySubject()
        {
            var (service, userRepo, docRepo, chunkRepo, subjectRepo, sessionRepo, msgRepo) = BuildService();

            var subjectId = Guid.NewGuid();
            var subject = MakeSubject();
            subject.Id = subjectId;

            var sessions = new List<ChatSession>
            {
                MakeSession(subjectId: subjectId),
                MakeSession(subjectId: subjectId),
                MakeSession(subjectId: subjectId)
            };

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject> { subject });
            docRepo.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(new List<Document>());
            chunkRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(3);
            sessionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(sessions);
            msgRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            msgRepo.Setup(r => r.GetTokenBreakdownAsync()).ReturnsAsync((0L, 0L));
            msgRepo.Setup(r => r.GetStatsGroupedBySubjectAsync()).ReturnsAsync(new List<SubjectMessageStatsDto>());

            var stats = await service.GetDashboardStatsAsync();

            Assert.NotEmpty(stats.TopSubjectsByChat);
            var top = stats.TopSubjectsByChat.First();
            Assert.Equal(3, top.ChatCount);
        }

        [Fact]
        public async Task GetDashboardStatsAsync_SubjectDetailStats_ContainsAllSubjects()
        {
            var (service, userRepo, docRepo, chunkRepo, subjectRepo, sessionRepo, msgRepo) = BuildService();

            var subjects = new List<Subject>
            {
                MakeSubject(),
                MakeSubject(),
                MakeSubject()
            };

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(subjects);
            docRepo.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(new List<Document>());
            chunkRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ChatSession>());
            msgRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            msgRepo.Setup(r => r.GetTokenBreakdownAsync()).ReturnsAsync((0L, 0L));
            msgRepo.Setup(r => r.GetStatsGroupedBySubjectAsync()).ReturnsAsync(new List<SubjectMessageStatsDto>());

            var stats = await service.GetDashboardStatsAsync();

            Assert.Equal(3, stats.SubjectDetailStats.Count);
        }

        [Fact]
        public async Task GetDashboardStatsAsync_NewDocumentsLast7Days_CountsCorrectly()
        {
            var (service, userRepo, docRepo, chunkRepo, subjectRepo, sessionRepo, msgRepo) = BuildService();

            var now = DateTime.UtcNow;
            var docs = new List<Document>
            {
                MakeDocument(createdAt: now.AddDays(-1)),
                MakeDocument(createdAt: now.AddDays(-5)),
                MakeDocument(createdAt: now.AddDays(-10))  // outside 7-day window
            };

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject>());
            docRepo.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(docs);
            chunkRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ChatSession>());
            msgRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            msgRepo.Setup(r => r.GetTokenBreakdownAsync()).ReturnsAsync((0L, 0L));
            msgRepo.Setup(r => r.GetStatsGroupedBySubjectAsync()).ReturnsAsync(new List<SubjectMessageStatsDto>());

            var stats = await service.GetDashboardStatsAsync();

            Assert.Equal(2, stats.NewDocumentsLast7Days);
        }

        [Fact]
        public async Task GetDashboardStatsAsync_SubjectDetailStats_DocumentCountMatchesSubject()
        {
            var (service, userRepo, docRepo, chunkRepo, subjectRepo, sessionRepo, msgRepo) = BuildService();

            var subject = MakeSubject();
            var docs = new List<Document>
            {
                MakeDocument(subjectId: subject.Id),
                MakeDocument(subjectId: subject.Id),
                MakeDocument(subjectId: Guid.NewGuid()) // different subject
            };

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject> { subject });
            docRepo.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(docs);
            chunkRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ChatSession>());
            msgRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            msgRepo.Setup(r => r.GetTokenBreakdownAsync()).ReturnsAsync((0L, 0L));
            msgRepo.Setup(r => r.GetStatsGroupedBySubjectAsync()).ReturnsAsync(new List<SubjectMessageStatsDto>());

            var stats = await service.GetDashboardStatsAsync();

            var detail = stats.SubjectDetailStats.First(s => s.SubjectId == subject.Id);
            Assert.Equal(2, detail.DocumentCount);
        }

        [Fact]
        public async Task GetDashboardStatsAsync_TopSubjects_OrderedByDocumentCountDesc()
        {
            var (service, userRepo, docRepo, chunkRepo, subjectRepo, sessionRepo, msgRepo) = BuildService();

            var subject1 = MakeSubject(); // will have 3 docs
            var subject2 = MakeSubject(); // will have 1 doc

            var docs = new List<Document>
            {
                MakeDocument(subjectId: subject1.Id),
                MakeDocument(subjectId: subject1.Id),
                MakeDocument(subjectId: subject1.Id),
                MakeDocument(subjectId: subject2.Id),
            };

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject> { subject1, subject2 });
            docRepo.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(docs);
            chunkRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ChatSession>());
            msgRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            msgRepo.Setup(r => r.GetTokenBreakdownAsync()).ReturnsAsync((0L, 0L));
            msgRepo.Setup(r => r.GetStatsGroupedBySubjectAsync()).ReturnsAsync(new List<SubjectMessageStatsDto>());

            var stats = await service.GetDashboardStatsAsync();

            Assert.Equal(subject1.Id, stats.TopSubjects[0].Id);
            Assert.Equal(3, stats.TopSubjects[0].DocumentCount);
        }

        [Fact]
        public async Task GetDashboardStatsAsync_SubjectDetailStats_IncludesTokensFromMsgStats()
        {
            var (service, userRepo, docRepo, chunkRepo, subjectRepo, sessionRepo, msgRepo) = BuildService();

            var subject = MakeSubject();
            var msgStats = new List<SubjectMessageStatsDto>
            {
                new SubjectMessageStatsDto
                {
                    SubjectId = subject.Id,
                    MessageCount = 5,
                    PromptTokens = 100,
                    CompletionTokens = 200
                }
            };

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject> { subject });
            docRepo.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(new List<Document>());
            chunkRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(0);
            sessionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ChatSession>());
            msgRepo.Setup(r => r.CountAllAsync()).ReturnsAsync(5);
            msgRepo.Setup(r => r.GetTokenBreakdownAsync()).ReturnsAsync((100L, 200L));
            msgRepo.Setup(r => r.GetStatsGroupedBySubjectAsync()).ReturnsAsync(msgStats);

            var stats = await service.GetDashboardStatsAsync();

            var detail = stats.SubjectDetailStats.First(s => s.SubjectId == subject.Id);
            Assert.Equal(5, detail.ChatMessageCount);
            Assert.Equal(300, detail.TokensConsumed);
        }
    }
}
