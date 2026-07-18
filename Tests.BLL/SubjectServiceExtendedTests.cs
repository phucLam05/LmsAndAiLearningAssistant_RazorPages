using BLL.Services;
using Core.DTOs.Common;
using Core.DTOs.Subject;
using Core.Entities;
using DAL.Interfaces;
using Moq;
using System.Linq;

namespace Tests.BLL
{
    /// <summary>
    /// Extended unit tests for <see cref="SubjectService"/> covering all CRUD operations,
    /// pagination, lecturer-assignment rules, status filtering, and DTO mapping.
    /// Complements SubjectServiceTests.cs with deeper coverage.
    /// </summary>
    public class SubjectServiceExtendedTests
    {
        // ── Helpers ─────────────────────────────────────────────────────────────

        private static (SubjectService service, Mock<ISubjectRepository> repoMock) BuildService()
        {
            var repo    = new Mock<ISubjectRepository>();
            var service = new SubjectService(repo.Object);
            return (service, repo);
        }

        private static Subject MakeSubject(
            string code        = "PRN222",
            string name        = "C# Programming",
            SubjectStatus status = SubjectStatus.Active,
            Guid? lecturerId   = null,
            Guid? id           = null)
            => new Subject
            {
                Id          = id ?? Guid.NewGuid(),
                SubjectCode = code,
                Name        = name,
                Description = "Test subject description",
                LecturerId  = lecturerId,
                Status      = status,
                CreatedAt   = DateTime.UtcNow,
                UpdatedAt   = DateTime.UtcNow
            };

        private static Subject MakeSubjectWithLecturer(
            string lecturerName = "Dr. Nguyen",
            string code         = "PRN222")
        {
            var lecturerId = Guid.NewGuid();
            var subject    = MakeSubject(code: code, lecturerId: lecturerId);
            subject.Lecturer = new User
            {
                Id       = lecturerId,
                FullName = lecturerName,
                UserCode = "LEC001",
                Role     = UserRole.Lecturer
            };
            return subject;
        }

        // ── GetAllSubjectsAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllSubjectsAsync_ReturnsAllSubjectsAsDtos()
        {
            var (service, repo) = BuildService();

            var subjects = new List<Subject>
            {
                MakeSubject("PRN222", "C#"),
                MakeSubject("PRF192", "Python"),
                MakeSubject("SWD392", "Web Dev")
            };

            repo.Setup(r => r.GetAllAsync()).ReturnsAsync(subjects);

            var result = (await service.GetAllSubjectsAsync()).ToList();

            Assert.Equal(3, result.Count);
            Assert.Contains(result, d => d.SubjectCode == "PRN222");
            Assert.Contains(result, d => d.SubjectCode == "PRF192");
            Assert.Contains(result, d => d.SubjectCode == "SWD392");
        }

        [Fact]
        public async Task GetAllSubjectsAsync_EmptyRepository_ReturnsEmptyList()
        {
            var (service, repo) = BuildService();

            repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject>());

            var result = await service.GetAllSubjectsAsync();

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAllSubjectsAsync_MapsLecturerName()
        {
            var (service, repo) = BuildService();

            var subject = MakeSubjectWithLecturer("Dr. Smith", "PRN222");
            repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject> { subject });

            var result = (await service.GetAllSubjectsAsync()).Single();

            Assert.Equal("Dr. Smith", result.LecturerName);
        }

        [Fact]
        public async Task GetAllSubjectsAsync_SubjectWithNoLecturer_LecturerNameIsNull()
        {
            var (service, repo) = BuildService();

            var subject = MakeSubject(lecturerId: null);
            subject.Lecturer = null;
            repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject> { subject });

            var result = (await service.GetAllSubjectsAsync()).Single();

            Assert.Null(result.LecturerName);
        }

        // ── GetSubjectByIdAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task GetSubjectByIdAsync_SubjectExists_ReturnsDto()
        {
            var (service, repo) = BuildService();

            var subject = MakeSubject("PRN222");
            repo.Setup(r => r.GetByIdAsync(subject.Id)).ReturnsAsync(subject);

            var result = await service.GetSubjectByIdAsync(subject.Id);

            Assert.NotNull(result);
            Assert.Equal("PRN222", result!.SubjectCode);
        }

        [Fact]
        public async Task GetSubjectByIdAsync_SubjectNotFound_ReturnsNull()
        {
            var (service, repo) = BuildService();

            repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Subject?)null);

            var result = await service.GetSubjectByIdAsync(Guid.NewGuid());

            Assert.Null(result);
        }

        [Fact]
        public async Task GetSubjectByIdAsync_MapsAllFields()
        {
            var (service, repo) = BuildService();

            var lecturerId = Guid.NewGuid();
            var subject = new Subject
            {
                Id          = Guid.NewGuid(),
                SubjectCode = "PRN222",
                Name        = "C# Programming",
                Description = "Object-oriented programming with C#",
                LecturerId  = lecturerId,
                Status      = SubjectStatus.Active,
                CreatedAt   = new DateTime(2025, 1, 1),
                UpdatedAt   = new DateTime(2025, 6, 1)
            };

            repo.Setup(r => r.GetByIdAsync(subject.Id)).ReturnsAsync(subject);

            var result = await service.GetSubjectByIdAsync(subject.Id);

            Assert.NotNull(result);
            Assert.Equal(subject.Id,          result!.Id);
            Assert.Equal("PRN222",            result.SubjectCode);
            Assert.Equal("C# Programming",   result.Name);
            Assert.Equal("Object-oriented programming with C#", result.Description);
            Assert.Equal(lecturerId,          result.LecturerId);
            Assert.Equal(SubjectStatus.Active, result.Status);
        }

        // ── CreateSubjectAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task CreateSubjectAsync_NewSubjectCode_CreatesAndReturnsSuccess()
        {
            var (service, repo) = BuildService();

            repo.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            repo.Setup(r => r.CreateAsync(It.IsAny<Subject>())).Returns(Task.CompletedTask);

            var dto = new CreateSubjectDto
            {
                SubjectCode = "PRN222",
                Name        = "C# Programming",
                Description = "Learn C#",
                LecturerId  = Guid.NewGuid()
            };

            var (success, error) = await service.CreateSubjectAsync(dto);

            Assert.True(success);
            Assert.Null(error);
        }

        [Fact]
        public async Task CreateSubjectAsync_DuplicateSubjectCode_ReturnsFailure()
        {
            var (service, repo) = BuildService();

            repo.Setup(r => r.ExistsAsync("PRN222")).ReturnsAsync(true);

            var dto = new CreateSubjectDto
            {
                SubjectCode = "PRN222",
                Name        = "C# Programming"
            };

            var (success, error) = await service.CreateSubjectAsync(dto);

            Assert.False(success);
            Assert.Contains("PRN222", error);
        }

        [Fact]
        public async Task CreateSubjectAsync_CodeNormalizedToUppercase()
        {
            var (service, repo) = BuildService();

            Subject? capturedSubject = null;
            repo.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            repo
                .Setup(r => r.CreateAsync(It.IsAny<Subject>()))
                .Callback<Subject>(s => capturedSubject = s)
                .Returns(Task.CompletedTask);

            var dto = new CreateSubjectDto
            {
                SubjectCode = "prn222",
                Name        = "C# Programming"
            };

            await service.CreateSubjectAsync(dto);

            Assert.NotNull(capturedSubject);
            Assert.Equal("PRN222", capturedSubject!.SubjectCode);
        }

        [Fact]
        public async Task CreateSubjectAsync_NameIsTrimmed()
        {
            var (service, repo) = BuildService();

            Subject? capturedSubject = null;
            repo.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            repo
                .Setup(r => r.CreateAsync(It.IsAny<Subject>()))
                .Callback<Subject>(s => capturedSubject = s)
                .Returns(Task.CompletedTask);

            var dto = new CreateSubjectDto
            {
                SubjectCode = "PRN222",
                Name        = "  C# Programming  "
            };

            await service.CreateSubjectAsync(dto);

            Assert.Equal("C# Programming", capturedSubject!.Name);
        }

        [Fact]
        public async Task CreateSubjectAsync_StatusIsActive()
        {
            var (service, repo) = BuildService();

            Subject? capturedSubject = null;
            repo.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            repo
                .Setup(r => r.CreateAsync(It.IsAny<Subject>()))
                .Callback<Subject>(s => capturedSubject = s)
                .Returns(Task.CompletedTask);

            var dto = new CreateSubjectDto { SubjectCode = "PRN222", Name = "C#" };

            await service.CreateSubjectAsync(dto);

            Assert.Equal(SubjectStatus.Active, capturedSubject!.Status);
        }

        [Fact]
        public async Task CreateSubjectAsync_NewIdIsNotEmpty()
        {
            var (service, repo) = BuildService();

            Subject? capturedSubject = null;
            repo.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            repo
                .Setup(r => r.CreateAsync(It.IsAny<Subject>()))
                .Callback<Subject>(s => capturedSubject = s)
                .Returns(Task.CompletedTask);

            var dto = new CreateSubjectDto { SubjectCode = "PRN222", Name = "C#" };

            await service.CreateSubjectAsync(dto);

            Assert.NotEqual(Guid.Empty, capturedSubject!.Id);
        }

        [Fact]
        public async Task CreateSubjectAsync_WithLecturerId_SetsonSubject()
        {
            var (service, repo) = BuildService();

            Subject? capturedSubject = null;
            var lecturerId = Guid.NewGuid();
            repo.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            repo
                .Setup(r => r.CreateAsync(It.IsAny<Subject>()))
                .Callback<Subject>(s => capturedSubject = s)
                .Returns(Task.CompletedTask);

            var dto = new CreateSubjectDto
            {
                SubjectCode = "PRN222",
                Name        = "C#",
                LecturerId  = lecturerId
            };

            await service.CreateSubjectAsync(dto);

            Assert.Equal(lecturerId, capturedSubject!.LecturerId);
        }

        [Fact]
        public async Task CreateSubjectAsync_WithNullLecturerId_LecturerIdIsNull()
        {
            var (service, repo) = BuildService();

            Subject? capturedSubject = null;
            repo.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            repo
                .Setup(r => r.CreateAsync(It.IsAny<Subject>()))
                .Callback<Subject>(s => capturedSubject = s)
                .Returns(Task.CompletedTask);

            var dto = new CreateSubjectDto
            {
                SubjectCode = "PRN222",
                Name        = "C#",
                LecturerId  = null
            };

            await service.CreateSubjectAsync(dto);

            Assert.Null(capturedSubject!.LecturerId);
        }

        // ── UpdateSubjectAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task UpdateSubjectAsync_SubjectNotFound_ReturnsFailure()
        {
            var (service, repo) = BuildService();

            repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Subject?)null);

            var dto = new UpdateSubjectDto
            {
                Id         = Guid.NewGuid(),
                Name       = "New Name",
                LecturerId = null,
                Status     = SubjectStatus.Active
            };

            var (success, error) = await service.UpdateSubjectAsync(dto);

            Assert.False(success);
            Assert.Equal("Subject not found.", error);
        }

        [Fact]
        public async Task UpdateSubjectAsync_ValidSubject_UpdatesFieldsAndReturnsSuccess()
        {
            var (service, repo) = BuildService();

            var subject = MakeSubject("PRN222", "Old Name");
            repo.Setup(r => r.GetByIdAsync(subject.Id)).ReturnsAsync(subject);
            repo.Setup(r => r.UpdateAsync(It.IsAny<Subject>())).Returns(Task.CompletedTask);

            var newLecturerId = Guid.NewGuid();
            var dto = new UpdateSubjectDto
            {
                Id          = subject.Id,
                Name        = "New Name",
                Description = "Updated description",
                LecturerId  = newLecturerId,
                Status      = SubjectStatus.Inactive
            };

            var (success, error) = await service.UpdateSubjectAsync(dto);

            Assert.True(success);
            Assert.Null(error);
            Assert.Equal("New Name",         subject.Name);
            Assert.Equal("Updated description", subject.Description);
            Assert.Equal(newLecturerId,      subject.LecturerId);
            Assert.Equal(SubjectStatus.Inactive, subject.Status);
        }

        [Fact]
        public async Task UpdateSubjectAsync_NameIsTrimmed()
        {
            var (service, repo) = BuildService();

            var subject = MakeSubject();
            repo.Setup(r => r.GetByIdAsync(subject.Id)).ReturnsAsync(subject);
            repo.Setup(r => r.UpdateAsync(It.IsAny<Subject>())).Returns(Task.CompletedTask);

            var dto = new UpdateSubjectDto
            {
                Id         = subject.Id,
                Name       = "  Trimmed Name  ",
                LecturerId = null,
                Status     = SubjectStatus.Active
            };

            await service.UpdateSubjectAsync(dto);

            Assert.Equal("Trimmed Name", subject.Name);
        }

        [Fact]
        public async Task UpdateSubjectAsync_SetLecturerToNull_RemovesLecturer()
        {
            var (service, repo) = BuildService();

            var subject = MakeSubject(lecturerId: Guid.NewGuid());
            repo.Setup(r => r.GetByIdAsync(subject.Id)).ReturnsAsync(subject);
            repo.Setup(r => r.UpdateAsync(It.IsAny<Subject>())).Returns(Task.CompletedTask);

            var dto = new UpdateSubjectDto
            {
                Id         = subject.Id,
                Name       = subject.Name,
                LecturerId = null,
                Status     = subject.Status
            };

            await service.UpdateSubjectAsync(dto);

            Assert.Null(subject.LecturerId);
        }

        [Fact]
        public async Task UpdateSubjectAsync_CallsRepositoryUpdate()
        {
            var (service, repo) = BuildService();

            var subject = MakeSubject();
            repo.Setup(r => r.GetByIdAsync(subject.Id)).ReturnsAsync(subject);
            repo.Setup(r => r.UpdateAsync(It.IsAny<Subject>())).Returns(Task.CompletedTask);

            var dto = new UpdateSubjectDto
            {
                Id         = subject.Id,
                Name       = "Updated",
                LecturerId = null,
                Status     = SubjectStatus.Active
            };

            await service.UpdateSubjectAsync(dto);

            repo.Verify(r => r.UpdateAsync(subject), Times.Once);
        }

        // ── DeleteSubjectAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteSubjectAsync_SubjectFound_ReturnsSuccess()
        {
            var (service, repo) = BuildService();

            repo.Setup(r => r.DeleteAsync(It.IsAny<Guid>())).ReturnsAsync(true);

            var (success, error) = await service.DeleteSubjectAsync(Guid.NewGuid());

            Assert.True(success);
            Assert.Null(error);
        }

        [Fact]
        public async Task DeleteSubjectAsync_SubjectNotFound_ReturnsFailure()
        {
            var (service, repo) = BuildService();

            repo.Setup(r => r.DeleteAsync(It.IsAny<Guid>())).ReturnsAsync(false);

            var (success, error) = await service.DeleteSubjectAsync(Guid.NewGuid());

            Assert.False(success);
            Assert.Equal("Subject not found.", error);
        }

        [Fact]
        public async Task DeleteSubjectAsync_CallsRepositoryWithCorrectId()
        {
            var (service, repo) = BuildService();

            var id = Guid.NewGuid();
            repo.Setup(r => r.DeleteAsync(id)).ReturnsAsync(true);

            await service.DeleteSubjectAsync(id);

            repo.Verify(r => r.DeleteAsync(id), Times.Once);
        }

        // ── AssignLecturerAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task AssignLecturerAsync_SubjectNotFound_ReturnsFailure()
        {
            var (service, repo) = BuildService();

            repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Subject?)null);

            var dto = new AssignLecturerDto { SubjectId = Guid.NewGuid(), LecturerId = Guid.NewGuid() };

            var (success, error) = await service.AssignLecturerAsync(dto);

            Assert.False(success);
            Assert.Equal("Subject not found.", error);
        }

        [Fact]
        public async Task AssignLecturerAsync_SubjectFound_AssignsLecturerAndReturnsSuccess()
        {
            var (service, repo) = BuildService();

            var subject    = MakeSubject();
            var lecturerId = Guid.NewGuid();

            repo.Setup(r => r.GetByIdAsync(subject.Id)).ReturnsAsync(subject);
            repo.Setup(r => r.UpdateAsync(It.IsAny<Subject>())).Returns(Task.CompletedTask);

            var dto = new AssignLecturerDto { SubjectId = subject.Id, LecturerId = lecturerId };

            var (success, error) = await service.AssignLecturerAsync(dto);

            Assert.True(success);
            Assert.Null(error);
            Assert.Equal(lecturerId, subject.LecturerId);
        }

        [Fact]
        public async Task AssignLecturerAsync_NullLecturerId_RemovesLecturer()
        {
            var (service, repo) = BuildService();

            var subject = MakeSubject(lecturerId: Guid.NewGuid());
            repo.Setup(r => r.GetByIdAsync(subject.Id)).ReturnsAsync(subject);
            repo.Setup(r => r.UpdateAsync(It.IsAny<Subject>())).Returns(Task.CompletedTask);

            var dto = new AssignLecturerDto { SubjectId = subject.Id, LecturerId = null };

            await service.AssignLecturerAsync(dto);

            Assert.Null(subject.LecturerId);
        }

        [Fact]
        public async Task AssignLecturerAsync_ReplacesExistingLecturer()
        {
            var (service, repo) = BuildService();

            var oldLecturerId = Guid.NewGuid();
            var newLecturerId = Guid.NewGuid();
            var subject = MakeSubject(lecturerId: oldLecturerId);

            repo.Setup(r => r.GetByIdAsync(subject.Id)).ReturnsAsync(subject);
            repo.Setup(r => r.UpdateAsync(It.IsAny<Subject>())).Returns(Task.CompletedTask);

            var dto = new AssignLecturerDto { SubjectId = subject.Id, LecturerId = newLecturerId };

            await service.AssignLecturerAsync(dto);

            Assert.Equal(newLecturerId, subject.LecturerId);
            Assert.NotEqual(oldLecturerId, subject.LecturerId);
        }

        // ── GetSubjectsByLecturerAsync ────────────────────────────────────────────

        [Fact]
        public async Task GetSubjectsByLecturerAsync_ReturnsOnlyLecturerSubjects()
        {
            var (service, repo) = BuildService();

            var lecturerId = Guid.NewGuid();
            var subjects = new List<Subject>
            {
                MakeSubject("PRN222", lecturerId: lecturerId),
                MakeSubject("SWD392", lecturerId: lecturerId)
            };

            repo.Setup(r => r.GetByLecturerIdAsync(lecturerId)).ReturnsAsync(subjects);

            var result = (await service.GetSubjectsByLecturerAsync(lecturerId)).ToList();

            Assert.Equal(2, result.Count);
            Assert.All(result, s => Assert.Equal(lecturerId, s.LecturerId));
        }

        [Fact]
        public async Task GetSubjectsByLecturerAsync_LecturerHasNoSubjects_ReturnsEmpty()
        {
            var (service, repo) = BuildService();

            var lecturerId = Guid.NewGuid();
            repo.Setup(r => r.GetByLecturerIdAsync(lecturerId)).ReturnsAsync(new List<Subject>());

            var result = await service.GetSubjectsByLecturerAsync(lecturerId);

            Assert.Empty(result);
        }

        // ── GetActiveSubjectsAsync ────────────────────────────────────────────────

        [Fact]
        public async Task GetActiveSubjectsAsync_ReturnsOnlyActiveSubjects()
        {
            var (service, repo) = BuildService();

            var activeSubjects = new List<Subject>
            {
                MakeSubject("PRN222", status: SubjectStatus.Active),
                MakeSubject("SWD392", status: SubjectStatus.Active)
            };

            repo.Setup(r => r.GetActiveAsync()).ReturnsAsync(activeSubjects);

            var result = (await service.GetActiveSubjectsAsync()).ToList();

            Assert.Equal(2, result.Count);
            Assert.All(result, s => Assert.Equal(SubjectStatus.Active, s.Status));
        }

        [Fact]
        public async Task GetActiveSubjectsAsync_NoActiveSubjects_ReturnsEmpty()
        {
            var (service, repo) = BuildService();

            repo.Setup(r => r.GetActiveAsync()).ReturnsAsync(new List<Subject>());

            var result = await service.GetActiveSubjectsAsync();

            Assert.Empty(result);
        }

        // ── GetPagedAllSubjectsAsync ──────────────────────────────────────────────

        [Fact]
        public async Task GetPagedAllSubjectsAsync_ReturnsPaginatedResult()
        {
            var (service, repo) = BuildService();

            var subjects = new List<Subject>
            {
                MakeSubject("PRN222"),
                MakeSubject("SWD392")
            };

            repo
                .Setup(r => r.GetPagedAsync(null, null, 1, 10))
                .ReturnsAsync((subjects, 25)); // total 25, showing 2 on page 1

            var result = await service.GetPagedAllSubjectsAsync(null, null, 1, 10);

            Assert.Equal(2,  result.Items.Count());
            Assert.Equal(25, result.TotalCount);
            Assert.Equal(1,  result.PageIndex);
            Assert.Equal(10, result.PageSize);
        }

        [Fact]
        public async Task GetPagedAllSubjectsAsync_WithSearch_PassesSearchToRepo()
        {
            var (service, repo) = BuildService();

            repo
                .Setup(r => r.GetPagedAsync("PRN", null, 1, 10))
                .ReturnsAsync((new List<Subject> { MakeSubject("PRN222") }, 1));

            var result = await service.GetPagedAllSubjectsAsync("PRN", null, 1, 10);

            repo.Verify(r => r.GetPagedAsync("PRN", null, 1, 10), Times.Once);
        }

        [Fact]
        public async Task GetPagedAllSubjectsAsync_WithStatusFilter_ParsesStatusEnum()
        {
            var (service, repo) = BuildService();

            repo
                .Setup(r => r.GetPagedAsync(null, SubjectStatus.Active, 1, 10))
                .ReturnsAsync((new List<Subject>(), 0));

            await service.GetPagedAllSubjectsAsync(null, "Active", 1, 10);

            repo.Verify(r => r.GetPagedAsync(null, SubjectStatus.Active, 1, 10), Times.Once);
        }

        [Fact]
        public async Task GetPagedAllSubjectsAsync_InvalidStatus_PassesNullEnum()
        {
            var (service, repo) = BuildService();

            repo
                .Setup(r => r.GetPagedAsync(null, null, 1, 10))
                .ReturnsAsync((new List<Subject>(), 0));

            await service.GetPagedAllSubjectsAsync(null, "InvalidStatus", 1, 10);

            repo.Verify(r => r.GetPagedAsync(null, null, 1, 10), Times.Once);
        }

        [Fact]
        public async Task GetPagedAllSubjectsAsync_EmptyStatus_PassesNullEnum()
        {
            var (service, repo) = BuildService();

            repo
                .Setup(r => r.GetPagedAsync(null, null, 1, 10))
                .ReturnsAsync((new List<Subject>(), 0));

            await service.GetPagedAllSubjectsAsync(null, "", 1, 10);

            repo.Verify(r => r.GetPagedAsync(null, null, 1, 10), Times.Once);
        }

        // ── GetPagedSubjectsByLecturerAsync ────────────────────────────────────────

        [Fact]
        public async Task GetPagedSubjectsByLecturerAsync_ReturnsPaginatedResult()
        {
            var (service, repo) = BuildService();

            var lecturerId = Guid.NewGuid();
            var subjects   = new List<Subject> { MakeSubject("PRN222", lecturerId: lecturerId) };

            repo
                .Setup(r => r.GetPagedByLecturerAsync(lecturerId, null, 1, 5))
                .ReturnsAsync((subjects, 10));

            var result = await service.GetPagedSubjectsByLecturerAsync(lecturerId, null, 1, 5);

            Assert.Single(result.Items);
            Assert.Equal(10, result.TotalCount);
            Assert.Equal(1,  result.PageIndex);
            Assert.Equal(5,  result.PageSize);
        }

        [Fact]
        public async Task GetPagedSubjectsByLecturerAsync_WithSearch_PassesSearchToRepo()
        {
            var (service, repo) = BuildService();

            var lecturerId = Guid.NewGuid();
            repo
                .Setup(r => r.GetPagedByLecturerAsync(lecturerId, "PRN", 1, 10))
                .ReturnsAsync((new List<Subject>(), 0));

            await service.GetPagedSubjectsByLecturerAsync(lecturerId, "PRN", 1, 10);

            repo.Verify(r => r.GetPagedByLecturerAsync(lecturerId, "PRN", 1, 10), Times.Once);
        }

        // ── GetPagedActiveSubjectsAsync ───────────────────────────────────────────

        [Fact]
        public async Task GetPagedActiveSubjectsAsync_ReturnsPaginatedResult()
        {
            var (service, repo) = BuildService();

            var subjects = new List<Subject>
            {
                MakeSubject(status: SubjectStatus.Active),
                MakeSubject(status: SubjectStatus.Active)
            };

            repo
                .Setup(r => r.GetPagedActiveAsync(null, 1, 10))
                .ReturnsAsync((subjects, 50));

            var result = await service.GetPagedActiveSubjectsAsync(null, 1, 10);

            Assert.Equal(2,  result.Items.Count());
            Assert.Equal(50, result.TotalCount);
        }

        [Fact]
        public async Task GetPagedActiveSubjectsAsync_WithSearch_PassesSearchToRepo()
        {
            var (service, repo) = BuildService();

            repo
                .Setup(r => r.GetPagedActiveAsync("OOP", 1, 10))
                .ReturnsAsync((new List<Subject>(), 0));

            await service.GetPagedActiveSubjectsAsync("OOP", 1, 10);

            repo.Verify(r => r.GetPagedActiveAsync("OOP", 1, 10), Times.Once);
        }

        [Fact]
        public async Task GetPagedActiveSubjectsAsync_EmptyResults_ReturnEmptyPaged()
        {
            var (service, repo) = BuildService();

            repo
                .Setup(r => r.GetPagedActiveAsync(null, 2, 10))
                .ReturnsAsync((new List<Subject>(), 0));

            var result = await service.GetPagedActiveSubjectsAsync(null, 2, 10);

            Assert.Empty(result.Items);
            Assert.Equal(0, result.TotalCount);
        }

        // ── DTO mapping correctness ────────────────────────────────────────────────

        [Fact]
        public async Task GetAllSubjectsAsync_DtoHasCorrectSubjectCode()
        {
            var (service, repo) = BuildService();

            var subject = MakeSubject("MATH101");
            repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject> { subject });

            var result = (await service.GetAllSubjectsAsync()).Single();

            Assert.Equal("MATH101", result.SubjectCode);
        }

        [Fact]
        public async Task GetAllSubjectsAsync_DtoHasCorrectStatus()
        {
            var (service, repo) = BuildService();

            var subject = MakeSubject(status: SubjectStatus.Inactive);
            repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject> { subject });

            var result = (await service.GetAllSubjectsAsync()).Single();

            Assert.Equal(SubjectStatus.Inactive, result.Status);
        }

        [Fact]
        public async Task GetAllSubjectsAsync_DtoHasCorrectDescription()
        {
            var (service, repo) = BuildService();

            var subject = MakeSubject();
            subject.Description = "Custom description here";
            repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject> { subject });

            var result = (await service.GetAllSubjectsAsync()).Single();

            Assert.Equal("Custom description here", result.Description);
        }

        // ── Single subject edge cases ─────────────────────────────────────────────

        [Fact]
        public async Task GetAllSubjectsAsync_SingleSubject_ReturnsOneItem()
        {
            var (service, repo) = BuildService();

            repo.Setup(r => r.GetAllAsync())
                .ReturnsAsync(new List<Subject> { MakeSubject() });

            var result = await service.GetAllSubjectsAsync();

            Assert.Single(result);
        }

        [Fact]
        public async Task CreateSubjectAsync_DuplicateCheck_CallsExistsWithCorrectCode()
        {
            var (service, repo) = BuildService();

            repo.Setup(r => r.ExistsAsync("PRN222")).ReturnsAsync(false);
            repo.Setup(r => r.CreateAsync(It.IsAny<Subject>())).Returns(Task.CompletedTask);

            var dto = new CreateSubjectDto { SubjectCode = "prn222", Name = "C#" }; // lowercase input

            await service.CreateSubjectAsync(dto);

            // The ExistsAsync should be called with the original code (before normalization on Subject entity)
            repo.Verify(r => r.ExistsAsync("prn222"), Times.Once);
        }

        [Fact]
        public async Task UpdateSubjectAsync_DescriptionCanBeNull()
        {
            var (service, repo) = BuildService();

            var subject = MakeSubject();
            subject.Description = "Old description";
            repo.Setup(r => r.GetByIdAsync(subject.Id)).ReturnsAsync(subject);
            repo.Setup(r => r.UpdateAsync(It.IsAny<Subject>())).Returns(Task.CompletedTask);

            var dto = new UpdateSubjectDto
            {
                Id          = subject.Id,
                Name        = subject.Name,
                Description = null,
                LecturerId  = null,
                Status      = subject.Status
            };

            await service.UpdateSubjectAsync(dto);

            Assert.Null(subject.Description);
        }
    }
}
