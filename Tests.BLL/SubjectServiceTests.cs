using BLL.Services;
using Core.DTOs.Subject;
using Core.Entities;
using DAL.Interfaces;
using Moq;

namespace Tests.BLL
{
    /// <summary>
    /// Unit tests for <see cref="SubjectService"/>.
    /// Verifies admin CRUD, lecturer queries, student queries, and business rules.
    /// </summary>
    public class SubjectServiceTests
    {
        // ── Helpers ─────────────────────────────────────────────────────────────

        private static (SubjectService service, Mock<ISubjectRepository> repoMock) BuildService()
        {
            var repoMock = new Mock<ISubjectRepository>();
            var service = new SubjectService(repoMock.Object);
            return (service, repoMock);
        }

        private static Subject MakeSubject(string code = "PRN222", string name = "Razor Pages")
            => new Subject
            {
                Id = Guid.NewGuid(),
                SubjectCode = code,
                Name = name,
                Status = SubjectStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

        // ── GetAllSubjectsAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task GetAllSubjectsAsync_ReturnsAllMappedDtos()
        {
            var (service, repoMock) = BuildService();

            var subjects = new List<Subject>
            {
                MakeSubject("PRN222", "Razor Pages"),
                MakeSubject("PRN221", "WinForms")
            };

            repoMock
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(subjects);

            var result = (await service.GetAllSubjectsAsync()).ToList();

            Assert.Equal(2, result.Count);
            Assert.Contains(result, d => d.SubjectCode == "PRN222");
            Assert.Contains(result, d => d.SubjectCode == "PRN221");
        }

        [Fact]
        public async Task GetAllSubjectsAsync_EmptyRepository_ReturnsEmptyList()
        {
            var (service, repoMock) = BuildService();

            repoMock
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(new List<Subject>());

            var result = await service.GetAllSubjectsAsync();

            Assert.Empty(result);
        }

        // ── GetSubjectByIdAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task GetSubjectByIdAsync_ExistingId_ReturnsDto()
        {
            var (service, repoMock) = BuildService();

            var subject = MakeSubject();
            repoMock
                .Setup(r => r.GetByIdAsync(subject.Id))
                .ReturnsAsync(subject);

            var result = await service.GetSubjectByIdAsync(subject.Id);

            Assert.NotNull(result);
            Assert.Equal(subject.Id, result!.Id);
            Assert.Equal(subject.SubjectCode, result.SubjectCode);
        }

        [Fact]
        public async Task GetSubjectByIdAsync_NonExistingId_ReturnsNull()
        {
            var (service, repoMock) = BuildService();

            repoMock
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Subject?)null);

            var result = await service.GetSubjectByIdAsync(Guid.NewGuid());

            Assert.Null(result);
        }

        // ── CreateSubjectAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task CreateSubjectAsync_DuplicateCode_ReturnsFailure()
        {
            var (service, repoMock) = BuildService();

            repoMock
                .Setup(r => r.ExistsAsync("PRN222", null))
                .ReturnsAsync(true);

            var dto = new CreateSubjectDto
            {
                SubjectCode = "PRN222",
                Name = "Razor Pages"
            };

            var result = await service.CreateSubjectAsync(dto);

            Assert.False(result.Success);
            Assert.Contains("PRN222", result.Error);
            Assert.Contains("already exists", result.Error, StringComparison.OrdinalIgnoreCase);

            // CreateAsync should NOT be called
            repoMock.Verify(r => r.CreateAsync(It.IsAny<Subject>()), Times.Never);
        }

        [Fact]
        public async Task CreateSubjectAsync_UniqueCode_CreatesAndReturnsSuccess()
        {
            var (service, repoMock) = BuildService();

            repoMock
                .Setup(r => r.ExistsAsync("NEW001", null))
                .ReturnsAsync(false);

            repoMock
                .Setup(r => r.CreateAsync(It.IsAny<Subject>()))
                .ReturnsAsync((Subject s) => s);

            var dto = new CreateSubjectDto
            {
                SubjectCode = "NEW001",
                Name = "New Subject",
                Description = "Test Description"
            };

            var result = await service.CreateSubjectAsync(dto);

            Assert.True(result.Success);
            Assert.Null(result.Error);

            repoMock.Verify(r => r.CreateAsync(It.Is<Subject>(
                s => s.SubjectCode == "NEW001" && s.Status == SubjectStatus.Active
            )), Times.Once);
        }

        [Fact]
        public async Task CreateSubjectAsync_TrimsAndUppercasesSubjectCode()
        {
            var (service, repoMock) = BuildService();

            repoMock
                .Setup(r => r.ExistsAsync(It.IsAny<string>(), null))
                .ReturnsAsync(false);

            Subject? capturedSubject = null;
            repoMock
                .Setup(r => r.CreateAsync(It.IsAny<Subject>()))
                .Callback<Subject>(s => capturedSubject = s)
                .ReturnsAsync((Subject s) => s);

            var dto = new CreateSubjectDto
            {
                SubjectCode = " prn222 ",
                Name = "  Razor Pages  "
            };

            await service.CreateSubjectAsync(dto);

            Assert.NotNull(capturedSubject);
            Assert.Equal("PRN222", capturedSubject!.SubjectCode);
            Assert.Equal("Razor Pages", capturedSubject.Name);
        }

        // ── UpdateSubjectAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task UpdateSubjectAsync_SubjectNotFound_ReturnsFailure()
        {
            var (service, repoMock) = BuildService();

            repoMock
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Subject?)null);

            var dto = new UpdateSubjectDto
            {
                Id = Guid.NewGuid(),
                Name = "Updated",
                Status = SubjectStatus.Active
            };

            var result = await service.UpdateSubjectAsync(dto);

            Assert.False(result.Success);
            Assert.Equal("Subject not found.", result.Error);
        }

        [Fact]
        public async Task UpdateSubjectAsync_ExistingSubject_UpdatesAndReturnsSuccess()
        {
            var (service, repoMock) = BuildService();

            var subject = MakeSubject();
            repoMock
                .Setup(r => r.GetByIdAsync(subject.Id))
                .ReturnsAsync(subject);

            repoMock
                .Setup(r => r.UpdateAsync(It.IsAny<Subject>()))
                .ReturnsAsync((Subject s) => s);

            var lecturerId = Guid.NewGuid();
            var dto = new UpdateSubjectDto
            {
                Id = subject.Id,
                Name = "Updated Name",
                Description = "Updated Desc",
                LecturerId = lecturerId,
                Status = SubjectStatus.Inactive
            };

            var result = await service.UpdateSubjectAsync(dto);

            Assert.True(result.Success);
            Assert.Equal("Updated Name", subject.Name);
            Assert.Equal("Updated Desc", subject.Description);
            Assert.Equal(lecturerId, subject.LecturerId);
            Assert.Equal(SubjectStatus.Inactive, subject.Status);
        }

        // ── DeleteSubjectAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task DeleteSubjectAsync_ExistingSubject_ReturnsSuccess()
        {
            var (service, repoMock) = BuildService();

            var id = Guid.NewGuid();
            repoMock
                .Setup(r => r.DeleteAsync(id))
                .ReturnsAsync(true);

            var result = await service.DeleteSubjectAsync(id);

            Assert.True(result.Success);
            Assert.Null(result.Error);
        }

        [Fact]
        public async Task DeleteSubjectAsync_NonExistingSubject_ReturnsFailure()
        {
            var (service, repoMock) = BuildService();

            repoMock
                .Setup(r => r.DeleteAsync(It.IsAny<Guid>()))
                .ReturnsAsync(false);

            var result = await service.DeleteSubjectAsync(Guid.NewGuid());

            Assert.False(result.Success);
            Assert.Equal("Subject not found.", result.Error);
        }

        // ── AssignLecturerAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task AssignLecturerAsync_SubjectNotFound_ReturnsFailure()
        {
            var (service, repoMock) = BuildService();

            repoMock
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Subject?)null);

            var dto = new AssignLecturerDto
            {
                SubjectId = Guid.NewGuid(),
                LecturerId = Guid.NewGuid()
            };

            var result = await service.AssignLecturerAsync(dto);

            Assert.False(result.Success);
            Assert.Equal("Subject not found.", result.Error);
        }

        [Fact]
        public async Task AssignLecturerAsync_ValidSubject_AssignsLecturerAndReturnsSuccess()
        {
            var (service, repoMock) = BuildService();

            var subject = MakeSubject();
            var lecturerId = Guid.NewGuid();

            repoMock
                .Setup(r => r.GetByIdAsync(subject.Id))
                .ReturnsAsync(subject);

            repoMock
                .Setup(r => r.UpdateAsync(It.IsAny<Subject>()))
                .ReturnsAsync((Subject s) => s);

            var dto = new AssignLecturerDto
            {
                SubjectId = subject.Id,
                LecturerId = lecturerId
            };

            var result = await service.AssignLecturerAsync(dto);

            Assert.True(result.Success);
            Assert.Equal(lecturerId, subject.LecturerId);
        }

        [Fact]
        public async Task AssignLecturerAsync_NullLecturerId_RemovesAssignmentAndReturnsSuccess()
        {
            var (service, repoMock) = BuildService();

            var subject = MakeSubject();
            subject.LecturerId = Guid.NewGuid(); // already assigned

            repoMock
                .Setup(r => r.GetByIdAsync(subject.Id))
                .ReturnsAsync(subject);

            repoMock
                .Setup(r => r.UpdateAsync(It.IsAny<Subject>()))
                .ReturnsAsync((Subject s) => s);

            var dto = new AssignLecturerDto
            {
                SubjectId = subject.Id,
                LecturerId = null // remove
            };

            var result = await service.AssignLecturerAsync(dto);

            Assert.True(result.Success);
            Assert.Null(subject.LecturerId);
        }

        // ── GetSubjectsByLecturerAsync ───────────────────────────────────────────

        [Fact]
        public async Task GetSubjectsByLecturerAsync_ReturnsMappedDtos()
        {
            var (service, repoMock) = BuildService();

            var lecturerId = Guid.NewGuid();
            var subjects = new List<Subject>
            {
                new Subject { Id = Guid.NewGuid(), SubjectCode = "PRN222", Name = "Razor", LecturerId = lecturerId, Status = SubjectStatus.Active }
            };

            repoMock
                .Setup(r => r.GetByLecturerIdAsync(lecturerId))
                .ReturnsAsync(subjects);

            var result = (await service.GetSubjectsByLecturerAsync(lecturerId)).ToList();

            Assert.Single(result);
            Assert.Equal("PRN222", result[0].SubjectCode);
        }

        // ── GetActiveSubjectsAsync ───────────────────────────────────────────────

        [Fact]
        public async Task GetActiveSubjectsAsync_ReturnsOnlyActiveSubjects()
        {
            var (service, repoMock) = BuildService();

            var activeSubjects = new List<Subject>
            {
                MakeSubject("ACT001", "Active One"),
                MakeSubject("ACT002", "Active Two")
            };

            repoMock
                .Setup(r => r.GetActiveAsync())
                .ReturnsAsync(activeSubjects);

            var result = (await service.GetActiveSubjectsAsync()).ToList();

            Assert.Equal(2, result.Count);
            Assert.All(result, dto => Assert.Equal(SubjectStatus.Active, dto.Status));
        }

        // ── GetPagedAllSubjectsAsync ─────────────────────────────────────────────

        [Fact]
        public async Task GetPagedAllSubjectsAsync_ReturnsPagedResult()
        {
            var (service, repoMock) = BuildService();

            var subjects = new List<Subject> { MakeSubject() }.AsReadOnly();
            repoMock
                .Setup(r => r.GetPagedAsync(null, null, 1, 10))
                .ReturnsAsync(((IReadOnlyList<Subject>)subjects, 1));

            var result = await service.GetPagedAllSubjectsAsync(null, null, 1, 10);

            Assert.Equal(1, result.TotalCount);
            Assert.Single(result.Items);
        }

        [Fact]
        public async Task GetPagedAllSubjectsAsync_ParsesStatusStringCorrectly()
        {
            var (service, repoMock) = BuildService();

            var subjects = new List<Subject> { MakeSubject() }.AsReadOnly();
            repoMock
                .Setup(r => r.GetPagedAsync(null, SubjectStatus.Inactive, 1, 10))
                .ReturnsAsync(((IReadOnlyList<Subject>)subjects, 1));

            // Pass "Inactive" as a string — should parse to SubjectStatus.Inactive
            var result = await service.GetPagedAllSubjectsAsync(null, "Inactive", 1, 10);

            Assert.NotNull(result);
            repoMock.Verify(r => r.GetPagedAsync(null, SubjectStatus.Inactive, 1, 10), Times.Once);
        }
    }
}
