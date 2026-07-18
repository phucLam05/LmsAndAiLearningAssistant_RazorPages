using BLL.Services;
using Core.DTOs.Admin;
using Core.DTOs.Common;
using Core.Entities;
using DAL.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using System.Linq;

namespace Tests.BLL
{
    /// <summary>
    /// Extended integration-style unit tests for <see cref="AdminService"/> covering
    /// advanced dashboard scenarios, edge cases, and all management operations.
    /// </summary>
    public class AdminServiceExtendedTests
    {
        // ── Helpers ─────────────────────────────────────────────────────────────

        private static (
            AdminService service,
            Mock<IUserRepository> userRepoMock,
            Mock<IDocumentRepository> docRepoMock,
            Mock<ISubjectRepository> subjectRepoMock
        ) BuildService()
        {
            var userRepo    = new Mock<IUserRepository>();
            var docRepo     = new Mock<IDocumentRepository>();
            var subjectRepo = new Mock<ISubjectRepository>();
            var logger      = Mock.Of<ILogger<AdminService>>();

            var service = new AdminService(
                userRepo.Object,
                docRepo.Object,
                subjectRepo.Object,
                logger);

            return (service, userRepo, docRepo, subjectRepo);
        }

        private static User MakeUser(
            UserRole role     = UserRole.Student,
            UserStatus status = UserStatus.Active,
            Guid? id          = null,
            string userCode   = "HE001",
            string fullName   = "Test User",
            DateTime? createdAt = null)
            => new User
            {
                Id           = id ?? Guid.NewGuid(),
                UserCode     = userCode,
                FullName     = fullName,
                Role         = role,
                Status       = status,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass"),
                EmailHash    = "hash",
                EmailEncrypt = "enc",
                CreatedAt    = createdAt ?? DateTime.UtcNow,
                UpdatedAt    = DateTime.UtcNow
            };

        private static Document MakeDocument(
            DocumentStatus status = DocumentStatus.Success,
            Guid? subjectId       = null)
            => new Document
            {
                Id        = Guid.NewGuid(),
                FileName  = "file.pdf",
                FileUrl   = "test/file.pdf",
                Status    = status,
                SubjectId = subjectId ?? Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

        private static Subject MakeSubject(
            SubjectStatus status = SubjectStatus.Active,
            string code         = "PRN222")
            => new Subject
            {
                Id          = Guid.NewGuid(),
                SubjectCode = code,
                Name        = "Test Subject",
                Status      = status,
                CreatedAt   = DateTime.UtcNow,
                UpdatedAt   = DateTime.UtcNow
            };

        // ── GetDashboardStatsAsync — detailed counting ────────────────────────────

        [Fact]
        public async Task GetDashboardStats_CountsAdminsSeparately()
        {
            var (service, userRepo, docRepo, subjectRepo) = BuildService();

            var users = new List<User>
            {
                MakeUser(UserRole.Admin,   userCode: "ADM001"),
                MakeUser(UserRole.Admin,   userCode: "ADM002"),
                MakeUser(UserRole.Student, userCode: "HE001"),
                MakeUser(UserRole.Lecturer, userCode: "LEC001")
            };

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(users);
            docRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Document>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject>());

            var stats = await service.GetDashboardStatsAsync();

            Assert.Equal(4, stats.TotalUsers);
            Assert.Equal(1, stats.TotalStudents);
            Assert.Equal(1, stats.TotalLecturers);
        }

        [Fact]
        public async Task GetDashboardStats_AllRoles_CountsCorrectly()
        {
            var (service, userRepo, docRepo, subjectRepo) = BuildService();

            var users = new List<User>
            {
                MakeUser(UserRole.Admin,    userCode: "ADM001"),
                MakeUser(UserRole.Student,  userCode: "HE001"),
                MakeUser(UserRole.Student,  userCode: "HE002"),
                MakeUser(UserRole.Student,  userCode: "HE003"),
                MakeUser(UserRole.Lecturer, userCode: "LEC001"),
                MakeUser(UserRole.Lecturer, userCode: "LEC002")
            };

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(users);
            docRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Document>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject>());

            var stats = await service.GetDashboardStatsAsync();

            Assert.Equal(6, stats.TotalUsers);
            Assert.Equal(3, stats.TotalStudents);
            Assert.Equal(2, stats.TotalLecturers);
        }

        [Fact]
        public async Task GetDashboardStats_DocumentsByStatus_CountsCorrectly()
        {
            var (service, userRepo, docRepo, subjectRepo) = BuildService();

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject>());

            var documents = new List<Document>
            {
                MakeDocument(DocumentStatus.Success),
                MakeDocument(DocumentStatus.Success),
                MakeDocument(DocumentStatus.Failed),
                MakeDocument(DocumentStatus.Processing),
                MakeDocument(DocumentStatus.Pending),
                MakeDocument(DocumentStatus.Pending)
            };

            docRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(documents);

            var stats = await service.GetDashboardStatsAsync();

            Assert.Equal(6, stats.TotalDocuments);
            Assert.Equal(2, stats.IndexedDocuments);
        }

        [Fact]
        public async Task GetDashboardStats_AllDocumentsFailed_IndexedIsZero()
        {
            var (service, userRepo, docRepo, subjectRepo) = BuildService();

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject>());

            var documents = new List<Document>
            {
                MakeDocument(DocumentStatus.Failed),
                MakeDocument(DocumentStatus.Failed)
            };

            docRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(documents);

            var stats = await service.GetDashboardStatsAsync();

            Assert.Equal(2, stats.TotalDocuments);
            Assert.Equal(0, stats.IndexedDocuments);
        }

        [Fact]
        public async Task GetDashboardStats_AllDocumentsSuccess_IndexedEqualsTotal()
        {
            var (service, userRepo, docRepo, subjectRepo) = BuildService();

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject>());

            var documents = new List<Document>
            {
                MakeDocument(DocumentStatus.Success),
                MakeDocument(DocumentStatus.Success),
                MakeDocument(DocumentStatus.Success)
            };

            docRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(documents);

            var stats = await service.GetDashboardStatsAsync();

            Assert.Equal(stats.TotalDocuments, stats.IndexedDocuments);
        }

        [Fact]
        public async Task GetDashboardStats_ActiveSubjects_CountedCorrectly()
        {
            var (service, userRepo, docRepo, subjectRepo) = BuildService();

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            docRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Document>());

            var subjects = new List<Subject>
            {
                MakeSubject(SubjectStatus.Active,   "PRN222"),
                MakeSubject(SubjectStatus.Active,   "SWD392"),
                MakeSubject(SubjectStatus.Inactive, "PRF192")
            };

            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(subjects);

            var stats = await service.GetDashboardStatsAsync();

            Assert.Equal(3, stats.TotalSubjects);
        }

        [Fact]
        public async Task GetDashboardStats_EmptySystem_AllCountsAreZero()
        {
            var (service, userRepo, docRepo, subjectRepo) = BuildService();

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            docRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Document>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject>());

            var stats = await service.GetDashboardStatsAsync();

            Assert.Equal(0, stats.TotalUsers);
            Assert.Equal(0, stats.TotalStudents);
            Assert.Equal(0, stats.TotalLecturers);
            Assert.Equal(0, stats.TotalDocuments);
            Assert.Equal(0, stats.TotalSubjects);
            Assert.Equal(0, stats.IndexedDocuments);
        }

        // ── GetPagedUsersAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task GetPagedUsersAsync_ReturnsCorrectPage()
        {
            var (service, userRepo, _, _) = BuildService();

            var users = new List<User>
            {
                MakeUser(userCode: "HE001"),
                MakeUser(userCode: "HE002")
            };

            userRepo
                .Setup(r => r.GetPagedUsersAsync(null, null, null, 1, 10))
                .ReturnsAsync((users, 25));

            var result = await service.GetPagedUsersAsync(null, null, null, 1, 10);

            Assert.Equal(2,  result.Items.Count());
            Assert.Equal(25, result.TotalCount);
            Assert.Equal(1,  result.PageIndex);
            Assert.Equal(10, result.PageSize);
        }

        [Fact]
        public async Task GetPagedUsersAsync_EmptyPage_ReturnsEmptyItems()
        {
            var (service, userRepo, _, _) = BuildService();

            userRepo
                .Setup(r => r.GetPagedUsersAsync(null, null, null, 99, 10))
                .ReturnsAsync((new List<User>(), 25));

            var result = await service.GetPagedUsersAsync(null, null, null, 99, 10);

            Assert.Empty(result.Items);
            Assert.Equal(25, result.TotalCount);
        }

        [Fact]
        public async Task GetPagedUsersAsync_WithSearch_PassesSearchToRepo()
        {
            var (service, userRepo, _, _) = BuildService();

            userRepo
                .Setup(r => r.GetPagedUsersAsync("Nguyen", null, null, 1, 10))
                .ReturnsAsync((new List<User>(), 0));

            await service.GetPagedUsersAsync("Nguyen", null, null, 1, 10);

            userRepo.Verify(r => r.GetPagedUsersAsync("Nguyen", null, null, 1, 10), Times.Once);
        }

        [Fact]
        public async Task GetPagedUsersAsync_WithRoleFilter_PassesRoleToRepo()
        {
            var (service, userRepo, _, _) = BuildService();

            userRepo
                .Setup(r => r.GetPagedUsersAsync(null, UserRole.Student, null, 1, 10))
                .ReturnsAsync((new List<User>(), 0));

            await service.GetPagedUsersAsync(null, UserRole.Student, null, 1, 10);

            userRepo.Verify(r => r.GetPagedUsersAsync(null, UserRole.Student, null, 1, 10), Times.Once);
        }

        [Fact]
        public async Task GetPagedUsersAsync_WithStatusFilter_PassesStatusToRepo()
        {
            var (service, userRepo, _, _) = BuildService();

            userRepo
                .Setup(r => r.GetPagedUsersAsync(null, null, UserStatus.Inactive, 1, 10))
                .ReturnsAsync((new List<User>(), 0));

            await service.GetPagedUsersAsync(null, null, UserStatus.Inactive, 1, 10);

            userRepo.Verify(r => r.GetPagedUsersAsync(null, null, UserStatus.Inactive, 1, 10), Times.Once);
        }

        // ── GetUserByIdAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task GetUserByIdAsync_UserExists_ReturnsUser()
        {
            var (service, userRepo, _, _) = BuildService();

            var user = MakeUser();
            userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

            var result = await service.GetUserByIdAsync(user.Id);

            Assert.NotNull(result);
            Assert.Equal(user.Id, result!.Id);
        }

        [Fact]
        public async Task GetUserByIdAsync_UserNotFound_ReturnsNull()
        {
            var (service, userRepo, _, _) = BuildService();

            userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

            var result = await service.GetUserByIdAsync(Guid.NewGuid());

            Assert.Null(result);
        }

        // ── ToggleUserStatusAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task ToggleUserStatusAsync_ActiveUser_SetsToInactive()
        {
            var (service, userRepo, _, _) = BuildService();

            var user = MakeUser(status: UserStatus.Active);
            userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            await service.ToggleUserStatusAsync(user.Id);

            Assert.Equal(UserStatus.Inactive, user.Status);
        }

        [Fact]
        public async Task ToggleUserStatusAsync_InactiveUser_SetsToActive()
        {
            var (service, userRepo, _, _) = BuildService();

            var user = MakeUser(status: UserStatus.Inactive);
            userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            await service.ToggleUserStatusAsync(user.Id);

            Assert.Equal(UserStatus.Active, user.Status);
        }

        [Fact]
        public async Task ToggleUserStatusAsync_UserNotFound_DoesNotThrow()
        {
            var (service, userRepo, _, _) = BuildService();

            userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

            var exception = await Record.ExceptionAsync(
                () => service.ToggleUserStatusAsync(Guid.NewGuid()));

            Assert.Null(exception);
        }

        [Fact]
        public async Task ToggleUserStatusAsync_CallsUpdateAsync()
        {
            var (service, userRepo, _, _) = BuildService();

            var user = MakeUser(status: UserStatus.Active);
            userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            await service.ToggleUserStatusAsync(user.Id);

            userRepo.Verify(r => r.UpdateAsync(user), Times.Once);
        }

        [Fact]
        public async Task ToggleUserStatusAsync_DoubleToggle_RestoresOriginalStatus()
        {
            var (service, userRepo, _, _) = BuildService();

            var user = MakeUser(status: UserStatus.Active);
            userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            await service.ToggleUserStatusAsync(user.Id);
            Assert.Equal(UserStatus.Inactive, user.Status);

            await service.ToggleUserStatusAsync(user.Id);
            Assert.Equal(UserStatus.Active, user.Status);
        }

        // ── GetAllUsersAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllUsersAsync_ReturnsAllUsers()
        {
            var (service, userRepo, _, _) = BuildService();

            var users = new List<User>
            {
                MakeUser(UserRole.Admin),
                MakeUser(UserRole.Student),
                MakeUser(UserRole.Lecturer)
            };

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(users);

            var result = await service.GetAllUsersAsync();

            Assert.Equal(3, result.Count());
        }

        [Fact]
        public async Task GetAllUsersAsync_EmptyRepo_ReturnsEmpty()
        {
            var (service, userRepo, _, _) = BuildService();

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());

            var result = await service.GetAllUsersAsync();

            Assert.Empty(result);
        }

        // ── GetRecentUsersAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task GetRecentUsersAsync_ReturnsLimitedUsers()
        {
            var (service, userRepo, _, _) = BuildService();

            var now = DateTime.UtcNow;
            var users = Enumerable.Range(0, 20)
                .Select(i => MakeUser(createdAt: now.AddMinutes(-i)))
                .ToList();

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(users);

            var result = (await service.GetRecentUsersAsync(5)).ToList();

            Assert.Equal(5, result.Count);
        }

        [Fact]
        public async Task GetRecentUsersAsync_ReturnsNewestFirst()
        {
            var (service, userRepo, _, _) = BuildService();

            var now = DateTime.UtcNow;
            var oldest = MakeUser(createdAt: now.AddDays(-10), userCode: "OLD001");
            var newest = MakeUser(createdAt: now.AddDays(-1),  userCode: "NEW001");
            var middle = MakeUser(createdAt: now.AddDays(-5),  userCode: "MID001");

            userRepo.Setup(r => r.GetAllUsersAsync())
                .ReturnsAsync(new List<User> { oldest, newest, middle });

            var result = (await service.GetRecentUsersAsync(3)).ToList();

            // newest should be first
            Assert.Equal("NEW001", result[0].UserCode);
        }

        [Fact]
        public async Task GetRecentUsersAsync_FewerUsersThanLimit_ReturnsAllUsers()
        {
            var (service, userRepo, _, _) = BuildService();

            var users = new List<User>
            {
                MakeUser(userCode: "HE001"),
                MakeUser(userCode: "HE002")
            };

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(users);

            var result = await service.GetRecentUsersAsync(10);

            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task GetRecentUsersAsync_ZeroLimit_ReturnsEmpty()
        {
            var (service, userRepo, _, _) = BuildService();

            var users = new List<User> { MakeUser() };
            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(users);

            var result = await service.GetRecentUsersAsync(0);

            Assert.Empty(result);
        }

        // ── GetDocumentsBySubjectAsync ────────────────────────────────────────────

        [Fact]
        public async Task GetDocumentsBySubjectAsync_ReturnsDocumentsForSubject()
        {
            var (service, _, docRepo, _) = BuildService();

            var subjectId = Guid.NewGuid();
            var docs = new List<Document>
            {
                MakeDocument(subjectId: subjectId),
                MakeDocument(subjectId: subjectId)
            };

            docRepo.Setup(r => r.GetBySubjectIdAsync(subjectId)).ReturnsAsync(docs);

            var result = (await service.GetDocumentsBySubjectAsync(subjectId)).ToList();

            Assert.Equal(2, result.Count);
            Assert.All(result, d => Assert.Equal(subjectId, d.SubjectId));
        }

        [Fact]
        public async Task GetDocumentsBySubjectAsync_NoDocuments_ReturnsEmpty()
        {
            var (service, _, docRepo, _) = BuildService();

            docRepo
                .Setup(r => r.GetBySubjectIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new List<Document>());

            var result = await service.GetDocumentsBySubjectAsync(Guid.NewGuid());

            Assert.Empty(result);
        }

        // ── GetAllSubjectsAsync (admin) ───────────────────────────────────────────

        [Fact]
        public async Task AdminGetAllSubjectsAsync_ReturnsAllSubjects()
        {
            var (service, _, _, subjectRepo) = BuildService();

            var subjects = new List<Subject>
            {
                MakeSubject(SubjectStatus.Active,   "PRN222"),
                MakeSubject(SubjectStatus.Inactive, "SWD392"),
                MakeSubject(SubjectStatus.Active,   "PRF192")
            };

            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(subjects);

            var result = (await service.GetAllSubjectsAsync()).ToList();

            Assert.Equal(3, result.Count);
        }

        [Fact]
        public async Task AdminGetAllSubjectsAsync_IncludesInactiveSubjects()
        {
            var (service, _, _, subjectRepo) = BuildService();

            var subjects = new List<Subject>
            {
                MakeSubject(SubjectStatus.Active,   "PRN222"),
                MakeSubject(SubjectStatus.Inactive, "SWD392")
            };

            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(subjects);

            var result = (await service.GetAllSubjectsAsync()).ToList();

            Assert.Equal(2, result.Count);
            Assert.Contains(result, s => s.Status == SubjectStatus.Inactive);
        }

        // ── GetDocumentByIdAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task GetDocumentByIdAsync_DocumentExists_ReturnsDocument()
        {
            var (service, _, docRepo, _) = BuildService();

            var doc = MakeDocument();
            docRepo.Setup(r => r.GetByIdAsync(doc.Id)).ReturnsAsync(doc);

            var result = await service.GetDocumentByIdAsync(doc.Id);

            Assert.NotNull(result);
            Assert.Equal(doc.Id, result!.Id);
        }

        [Fact]
        public async Task GetDocumentByIdAsync_DocumentNotFound_ReturnsNull()
        {
            var (service, _, docRepo, _) = BuildService();

            docRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Document?)null);

            var result = await service.GetDocumentByIdAsync(Guid.NewGuid());

            Assert.Null(result);
        }

        // ── Repository call verification ──────────────────────────────────────────

        [Fact]
        public async Task GetDashboardStats_CallsAllThreeRepositories()
        {
            var (service, userRepo, docRepo, subjectRepo) = BuildService();

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            docRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Document>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject>());

            await service.GetDashboardStatsAsync();

            userRepo.Verify(r => r.GetAllUsersAsync(), Times.AtLeastOnce);
            docRepo.Verify(r => r.GetAllAsync(), Times.AtLeastOnce);
            subjectRepo.Verify(r => r.GetAllAsync(), Times.AtLeastOnce);
        }

        [Fact]
        public async Task GetAllUsersAsync_CallsGetAllUsersOnRepo()
        {
            var (service, userRepo, _, _) = BuildService();

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());

            await service.GetAllUsersAsync();

            userRepo.Verify(r => r.GetAllUsersAsync(), Times.Once);
        }

        [Fact]
        public async Task GetDocumentsBySubjectAsync_CallsRepoWithCorrectSubjectId()
        {
            var (service, _, docRepo, _) = BuildService();

            var subjectId = Guid.NewGuid();
            docRepo.Setup(r => r.GetBySubjectIdAsync(subjectId)).ReturnsAsync(new List<Document>());

            await service.GetDocumentsBySubjectAsync(subjectId);

            docRepo.Verify(r => r.GetBySubjectIdAsync(subjectId), Times.Once);
        }

        // ── Mixed data scenarios ──────────────────────────────────────────────────

        [Fact]
        public async Task GetDashboardStats_MixedDocumentStatuses_CountsOnlySuccess()
        {
            var (service, userRepo, docRepo, subjectRepo) = BuildService();

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());
            subjectRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject>());

            var docs = new List<Document>
            {
                MakeDocument(DocumentStatus.Success),
                MakeDocument(DocumentStatus.Success),
                MakeDocument(DocumentStatus.Processing),
                MakeDocument(DocumentStatus.Failed),
                MakeDocument(DocumentStatus.Pending)
            };

            docRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(docs);

            var stats = await service.GetDashboardStatsAsync();

            Assert.Equal(5, stats.TotalDocuments);
            Assert.Equal(2, stats.IndexedDocuments);
        }

        [Fact]
        public async Task GetRecentUsersAsync_LargeDataset_StillReturnsCorrectCount()
        {
            var (service, userRepo, _, _) = BuildService();

            var now = DateTime.UtcNow;
            var users = Enumerable.Range(0, 100)
                .Select(i => MakeUser(createdAt: now.AddMinutes(-i), userCode: $"HE{i:D3}"))
                .ToList();

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(users);

            var result = await service.GetRecentUsersAsync(10);

            Assert.Equal(10, result.Count());
        }
    }
}
