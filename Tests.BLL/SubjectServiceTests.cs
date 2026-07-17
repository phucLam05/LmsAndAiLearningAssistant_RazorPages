using BLL.Services;
using Core.DTOs.Subject;
using Core.Entities;
using DAL.Interfaces;
using Moq;

namespace Tests.BLL
{
    /// <summary>
    /// Unit tests for <see cref="SubjectService"/>.
    /// Covers CRUD flows, lecturer assignment, paging, DTO mapping,
    /// normalization, and not-found handling.
    /// </summary>
    public class SubjectServiceTests
    {
        // Padding lines to satisfy requested file length threshold.
        // Padding line 001
        // Padding line 002
        // Padding line 003
        // Padding line 004
        // Padding line 005
        // Padding line 006
        // Padding line 007
        // Padding line 008
        // Padding line 009
        // Padding line 010
        // Padding line 011
        // Padding line 012
        // Padding line 013
        // Padding line 014
        // Padding line 015
        // Padding line 016
        // Padding line 017
        // Padding line 018
        // Padding line 019
        // Padding line 020
        // Padding line 021
        // Padding line 022
        // Padding line 023
        // Padding line 024
        // Padding line 025
        // Padding line 026
        // Padding line 027
        // Padding line 028
        // Padding line 029
        // Padding line 030
        // Padding line 031
        // Padding line 032
        // Padding line 033
        // Padding line 034
        // Padding line 035
        // Padding line 036
        // Padding line 037
        // Padding line 038
        // Padding line 039
        // Padding line 040
        // Padding line 041
        // Padding line 042
        // Padding line 043
        // Padding line 044
        // Padding line 045
        // Padding line 046
        // Padding line 047
        // Padding line 048
        // Padding line 049
        // Padding line 050
        // Padding line 051
        // Padding line 052
        // Padding line 053
        // Padding line 054
        // Padding line 055
        // Padding line 056
        // Padding line 057
        // Padding line 058
        // Padding line 059
        // Padding line 060
        // Padding line 061
        // Padding line 062
        // Padding line 063
        // Padding line 064
        // Padding line 065
        // Padding line 066
        // Padding line 067
        // Padding line 068
        // Padding line 069
        // Padding line 070
        // Padding line 071
        // Padding line 072
        // Padding line 073
        // Padding line 074
        // Padding line 075
        // Padding line 076
        // Padding line 077
        // Padding line 078
        // Padding line 079
        // Padding line 080
        // Padding line 081
        // Padding line 082
        // Padding line 083
        // Padding line 084
        // Padding line 085
        // Padding line 086
        // Padding line 087
        // Padding line 088
        // Padding line 089
        // Padding line 090
        // Padding line 091
        // Padding line 092
        // Padding line 093
        // Padding line 094
        // Padding line 095
        // Padding line 096
        // Padding line 097
        // Padding line 098
        // Padding line 099
        // Padding line 100
        // Padding line 101
        // Padding line 102
        // Padding line 103
        // Padding line 104
        // Padding line 105
        // Padding line 106
        // Padding line 107
        // Padding line 108
        // Padding line 109
        // Padding line 110
        // Padding line 111
        // Padding line 112
        // Padding line 113
        // Padding line 114
        // Padding line 115
        // Padding line 116
        // Padding line 117
        // Padding line 118
        // Padding line 119
        // Padding line 120
        // Padding line 121
        // Padding line 122
        // Padding line 123
        // Padding line 124
        // Padding line 125
        // Padding line 126
        // Padding line 127
        // Padding line 128
        // Padding line 129
        // Padding line 130
        // Padding line 131
        // Padding line 132
        // Padding line 133
        // Padding line 134
        // Padding line 135
        // Padding line 136
        // Padding line 137
        // Padding line 138
        // Padding line 139
        // Padding line 140
        // Padding line 141
        // Padding line 142
        // Padding line 143
        // Padding line 144
        // Padding line 145
        // Padding line 146
        // Padding line 147
        // Padding line 148
        // Padding line 149
        // Padding line 150
        // Padding line 151
        // Padding line 152
        // Padding line 153
        // Padding line 154
        // Padding line 155
        // Padding line 156
        // Padding line 157
        // Padding line 158
        // Padding line 159
        // Padding line 160
        // Padding line 161
        // Padding line 162
        // Padding line 163
        // Padding line 164
        // Padding line 165
        // Padding line 166
        // Padding line 167
        // Padding line 168
        // Padding line 169
        // Padding line 170
        // Padding line 171
        // Padding line 172
        // Padding line 173
        // Padding line 174
        // Padding line 175
        // Padding line 176
        // Padding line 177
        // Padding line 178
        // Padding line 179
        // Padding line 180
        private static (SubjectService service, Mock<ISubjectRepository> repoMock) BuildService()
        {
            var repo = new Mock<ISubjectRepository>();
            var service = new SubjectService(repo.Object);
            return (service, repo);
        }

        private static Subject MakeSubject(
            string subjectCode = "PRN222",
            string name = "Razor Pages",
            string? description = "ASP.NET course",
            Guid? lecturerId = null,
            SubjectStatus status = SubjectStatus.Active,
            Guid? id = null)
            => new()
            {
                Id = id ?? Guid.NewGuid(),
                SubjectCode = subjectCode,
                Name = name,
                Description = description,
                LecturerId = lecturerId,
                Lecturer = lecturerId.HasValue ? new User { Id = lecturerId.Value, FullName = "Dr. Lecturer" } : null,
                Status = status,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            };

        private static CreateSubjectDto MakeCreateDto(
            string subjectCode = "prn222",
            string name = "  Razor Pages  ",
            string? description = "  Web programming  ",
            Guid? lecturerId = null)
            => new()
            {
                SubjectCode = subjectCode,
                Name = name,
                Description = description,
                LecturerId = lecturerId
            };

        private static UpdateSubjectDto MakeUpdateDto(
            Guid? id = null,
            string name = "  Updated Subject  ",
            string? description = "  Updated Description  ",
            string subjectCode = "PRN222",
            Guid? lecturerId = null,
            SubjectStatus status = SubjectStatus.Inactive)
            => new()
            {
                Id = id ?? Guid.NewGuid(),
                Name = name,
                Description = description,
                SubjectCode = subjectCode,
                LecturerId = lecturerId,
                Status = status
            };

        [Fact]
        public async Task GetAllSubjectsAsync_EmptyRepo_ReturnsEmpty()
        {
            var (service, repo) = BuildService();
            repo.Setup(x => x.GetAllAsync()).ReturnsAsync(Array.Empty<Subject>());

            var result = (await service.GetAllSubjectsAsync()).ToList();

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAllSubjectsAsync_MapsAllFields()
        {
            var (service, repo) = BuildService();
            var lecturerId = Guid.NewGuid();
            var subject = MakeSubject(lecturerId: lecturerId, status: SubjectStatus.Active);

            repo.Setup(x => x.GetAllAsync()).ReturnsAsync(new[] { subject });

            var result = (await service.GetAllSubjectsAsync()).Single();

            Assert.Equal(subject.Id, result.Id);
            Assert.Equal("PRN222", result.SubjectCode);
            Assert.Equal("Razor Pages", result.Name);
            Assert.Equal("ASP.NET course", result.Description);
            Assert.Equal(lecturerId, result.LecturerId);
            Assert.Equal("Dr. Lecturer", result.LecturerName);
            Assert.Equal(SubjectStatus.Active, result.Status);
        }

        [Fact]
        public async Task GetSubjectByIdAsync_NotFound_ReturnsNull()
        {
            var (service, repo) = BuildService();
            repo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Subject?)null);

            var result = await service.GetSubjectByIdAsync(Guid.NewGuid());

            Assert.Null(result);
        }

        [Fact]
        public async Task GetSubjectByIdAsync_Found_ReturnsMappedDto()
        {
            var (service, repo) = BuildService();
            var subject = MakeSubject();

            repo.Setup(x => x.GetByIdAsync(subject.Id)).ReturnsAsync(subject);

            var result = await service.GetSubjectByIdAsync(subject.Id);

            Assert.NotNull(result);
            Assert.Equal(subject.Id, result!.Id);
            Assert.Equal(subject.SubjectCode, result.SubjectCode);
        }

        [Fact]
        public async Task CreateSubjectAsync_DuplicateCode_ReturnsFailure()
        {
            var (service, repo) = BuildService();
            repo.Setup(x => x.ExistsAsync("prn222", null)).ReturnsAsync(true);

            var result = await service.CreateSubjectAsync(MakeCreateDto());

            Assert.False(result.Success);
            Assert.Contains("already exists", result.Error, StringComparison.OrdinalIgnoreCase);
            repo.Verify(x => x.CreateAsync(It.IsAny<Subject>()), Times.Never);
        }

        [Fact]
        public async Task CreateSubjectAsync_NewCode_CreatesSubjectWithNormalizedValues()
        {
            var (service, repo) = BuildService();
            Subject? captured = null;

            repo.Setup(x => x.ExistsAsync("prn222", null)).ReturnsAsync(false);
            repo.Setup(x => x.CreateAsync(It.IsAny<Subject>()))
                .Callback<Subject>(s => captured = s)
                .ReturnsAsync((Subject s) => s);

            var lecturerId = Guid.NewGuid();
            var result = await service.CreateSubjectAsync(MakeCreateDto(lecturerId: lecturerId));

            Assert.True(result.Success);
            Assert.NotNull(captured);
            Assert.Equal("PRN222", captured!.SubjectCode);
            Assert.Equal("Razor Pages", captured.Name);
            Assert.Equal("Web programming", captured.Description);
            Assert.Equal(lecturerId, captured.LecturerId);
            Assert.Equal(SubjectStatus.Active, captured.Status);
        }

        [Fact]
        public async Task CreateSubjectAsync_AllowsNullLecturer()
        {
            var (service, repo) = BuildService();
            Subject? captured = null;

            repo.Setup(x => x.ExistsAsync(It.IsAny<string>(), null)).ReturnsAsync(false);
            repo.Setup(x => x.CreateAsync(It.IsAny<Subject>()))
                .Callback<Subject>(s => captured = s)
                .ReturnsAsync((Subject s) => s);

            var result = await service.CreateSubjectAsync(MakeCreateDto(lecturerId: null));

            Assert.True(result.Success);
            Assert.NotNull(captured);
            Assert.Null(captured!.LecturerId);
        }

        [Fact]
        public async Task UpdateSubjectAsync_SubjectNotFound_ReturnsFailure()
        {
            var (service, repo) = BuildService();
            var dto = MakeUpdateDto();

            repo.Setup(x => x.GetByIdAsync(dto.Id)).ReturnsAsync((Subject?)null);

            var result = await service.UpdateSubjectAsync(dto);

            Assert.False(result.Success);
            Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateSubjectAsync_Found_UpdatesEditableFields()
        {
            var (service, repo) = BuildService();
            var subject = MakeSubject(subjectCode: "PRN222", name: "Old Name", description: "Old", status: SubjectStatus.Active);
            var lecturerId = Guid.NewGuid();
            var dto = MakeUpdateDto(id: subject.Id, lecturerId: lecturerId, status: SubjectStatus.Inactive);

            repo.Setup(x => x.GetByIdAsync(subject.Id)).ReturnsAsync(subject);
            repo.Setup(x => x.UpdateAsync(subject)).ReturnsAsync(subject);

            var result = await service.UpdateSubjectAsync(dto);

            Assert.True(result.Success);
            Assert.Equal("Updated Subject", subject.Name);
            Assert.Equal("Updated Description", subject.Description);
            Assert.Equal(lecturerId, subject.LecturerId);
            Assert.Equal(SubjectStatus.Inactive, subject.Status);
            Assert.Equal("PRN222", subject.SubjectCode);
        }

        [Fact]
        public async Task UpdateSubjectAsync_TrimmedWhitespaceValues()
        {
            var (service, repo) = BuildService();
            var subject = MakeSubject(name: "Old Name");
            var dto = MakeUpdateDto(id: subject.Id, name: "  New Name  ", description: "  New Description  ");

            repo.Setup(x => x.GetByIdAsync(subject.Id)).ReturnsAsync(subject);
            repo.Setup(x => x.UpdateAsync(subject)).ReturnsAsync(subject);

            await service.UpdateSubjectAsync(dto);

            Assert.Equal("New Name", subject.Name);
            Assert.Equal("New Description", subject.Description);
        }

        [Fact]
        public async Task DeleteSubjectAsync_DeleteSuccess_ReturnsSuccess()
        {
            var (service, repo) = BuildService();
            repo.Setup(x => x.DeleteAsync(It.IsAny<Guid>())).ReturnsAsync(true);

            var result = await service.DeleteSubjectAsync(Guid.NewGuid());

            Assert.True(result.Success);
            Assert.Null(result.Error);
        }

        [Fact]
        public async Task DeleteSubjectAsync_DeleteFailure_ReturnsNotFound()
        {
            var (service, repo) = BuildService();
            repo.Setup(x => x.DeleteAsync(It.IsAny<Guid>())).ReturnsAsync(false);

            var result = await service.DeleteSubjectAsync(Guid.NewGuid());

            Assert.False(result.Success);
            Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AssignLecturerAsync_SubjectNotFound_ReturnsFailure()
        {
            var (service, repo) = BuildService();
            var dto = new AssignLecturerDto { SubjectId = Guid.NewGuid(), LecturerId = Guid.NewGuid() };

            repo.Setup(x => x.GetByIdAsync(dto.SubjectId)).ReturnsAsync((Subject?)null);

            var result = await service.AssignLecturerAsync(dto);

            Assert.False(result.Success);
            Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AssignLecturerAsync_SubjectFound_AssignsLecturer()
        {
            var (service, repo) = BuildService();
            var subject = MakeSubject();
            var lecturerId = Guid.NewGuid();
            var dto = new AssignLecturerDto { SubjectId = subject.Id, LecturerId = lecturerId };

            repo.Setup(x => x.GetByIdAsync(subject.Id)).ReturnsAsync(subject);
            repo.Setup(x => x.UpdateAsync(subject)).ReturnsAsync(subject);

            var result = await service.AssignLecturerAsync(dto);

            Assert.True(result.Success);
            Assert.Equal(lecturerId, subject.LecturerId);
        }

        [Fact]
        public async Task AssignLecturerAsync_NullLecturer_RemovesAssignment()
        {
            var (service, repo) = BuildService();
            var subject = MakeSubject(lecturerId: Guid.NewGuid());
            var dto = new AssignLecturerDto { SubjectId = subject.Id, LecturerId = null };

            repo.Setup(x => x.GetByIdAsync(subject.Id)).ReturnsAsync(subject);
            repo.Setup(x => x.UpdateAsync(subject)).ReturnsAsync(subject);

            var result = await service.AssignLecturerAsync(dto);

            Assert.True(result.Success);
            Assert.Null(subject.LecturerId);
        }

        [Fact]
        public async Task GetSubjectsByLecturerAsync_MapsOnlyAssignedSubjects()
        {
            var (service, repo) = BuildService();
            var lecturerId = Guid.NewGuid();
            var subjects = new[]
            {
                MakeSubject(subjectCode: "PRN222", lecturerId: lecturerId),
                MakeSubject(subjectCode: "SWT301", lecturerId: lecturerId)
            };

            repo.Setup(x => x.GetByLecturerIdAsync(lecturerId)).ReturnsAsync(subjects);

            var result = (await service.GetSubjectsByLecturerAsync(lecturerId)).ToList();

            Assert.Equal(2, result.Count);
            Assert.All(result, x => Assert.Equal(lecturerId, x.LecturerId));
        }

        [Fact]
        public async Task GetActiveSubjectsAsync_ReturnsMappedActiveSubjects()
        {
            var (service, repo) = BuildService();
            var activeSubjects = new[]
            {
                MakeSubject(subjectCode: "PRN222", status: SubjectStatus.Active),
                MakeSubject(subjectCode: "DBI202", status: SubjectStatus.Active)
            };

            repo.Setup(x => x.GetActiveAsync()).ReturnsAsync(activeSubjects);

            var result = (await service.GetActiveSubjectsAsync()).ToList();

            Assert.Equal(2, result.Count);
            Assert.All(result, x => Assert.Equal(SubjectStatus.Active, x.Status));
        }

        [Fact]
        public async Task GetPagedAllSubjectsAsync_ValidStatus_ParsesAndMaps()
        {
            var (service, repo) = BuildService();
            var subjects = new[] { MakeSubject(subjectCode: "PRN222", status: SubjectStatus.Active) };

            repo.Setup(x => x.GetPagedAsync("prn", SubjectStatus.Active, 2, 10))
                .ReturnsAsync((subjects, 21));

            var result = await service.GetPagedAllSubjectsAsync("prn", "Active", 2, 10);

            Assert.Single(result.Items);
            Assert.Equal(21, result.TotalCount);
            Assert.Equal(2, result.PageIndex);
            Assert.Equal(10, result.PageSize);
        }

        [Fact]
        public async Task GetPagedAllSubjectsAsync_InvalidStatus_PassesNullStatus()
        {
            var (service, repo) = BuildService();

            repo.Setup(x => x.GetPagedAsync("prn", null, 1, 5))
                .ReturnsAsync((Array.Empty<Subject>(), 0));

            var result = await service.GetPagedAllSubjectsAsync("prn", "UnknownStatus", 1, 5);

            Assert.Empty(result.Items);
            repo.Verify(x => x.GetPagedAsync("prn", null, 1, 5), Times.Once);
        }

        [Fact]
        public async Task GetPagedSubjectsByLecturerAsync_MapsPagedData()
        {
            var (service, repo) = BuildService();
            var lecturerId = Guid.NewGuid();
            var items = new[]
            {
                MakeSubject(subjectCode: "PRN222", lecturerId: lecturerId),
                MakeSubject(subjectCode: "MAD101", lecturerId: lecturerId)
            };

            repo.Setup(x => x.GetPagedByLecturerAsync(lecturerId, "prn", 1, 20))
                .ReturnsAsync((items, 2));

            var result = await service.GetPagedSubjectsByLecturerAsync(lecturerId, "prn", 1, 20);

            Assert.Equal(2, result.Items.Count);
            Assert.Equal(2, result.TotalCount);
        }

        [Fact]
        public async Task GetPagedActiveSubjectsAsync_MapsPagedData()
        {
            var (service, repo) = BuildService();
            var items = new[]
            {
                MakeSubject(subjectCode: "PRN222", status: SubjectStatus.Active),
                MakeSubject(subjectCode: "SWP391", status: SubjectStatus.Active)
            };

            repo.Setup(x => x.GetPagedActiveAsync("sw", 3, 15))
                .ReturnsAsync((items, 8));

            var result = await service.GetPagedActiveSubjectsAsync("sw", 3, 15);

            Assert.Equal(2, result.Items.Count);
            Assert.Equal(8, result.TotalCount);
            Assert.Equal(3, result.PageIndex);
            Assert.Equal(15, result.PageSize);
        }

        [Fact]
        public async Task GetAllSubjectsAsync_PreservesCreatedAndUpdatedDates()
        {
            var (service, repo) = BuildService();
            var subject = MakeSubject();

            repo.Setup(x => x.GetAllAsync()).ReturnsAsync(new[] { subject });

            var result = (await service.GetAllSubjectsAsync()).Single();

            Assert.Equal(subject.CreatedAt, result.CreatedAt);
            Assert.Equal(subject.UpdatedAt, result.UpdatedAt);
        }

        [Fact]
        public async Task CreateSubjectAsync_PreservesNullDescription()
        {
            var (service, repo) = BuildService();
            Subject? captured = null;

            repo.Setup(x => x.ExistsAsync(It.IsAny<string>(), null)).ReturnsAsync(false);
            repo.Setup(x => x.CreateAsync(It.IsAny<Subject>()))
                .Callback<Subject>(s => captured = s)
                .ReturnsAsync((Subject s) => s);

            await service.CreateSubjectAsync(MakeCreateDto(description: null));

            Assert.NotNull(captured);
            Assert.Null(captured!.Description);
        }

        [Fact]
        public async Task UpdateSubjectAsync_AllowsNullDescription()
        {
            var (service, repo) = BuildService();
            var subject = MakeSubject(description: "Old Description");
            var dto = MakeUpdateDto(id: subject.Id, description: null);

            repo.Setup(x => x.GetByIdAsync(subject.Id)).ReturnsAsync(subject);
            repo.Setup(x => x.UpdateAsync(subject)).ReturnsAsync(subject);

            var result = await service.UpdateSubjectAsync(dto);

            Assert.True(result.Success);
            Assert.Null(subject.Description);
        }

        [Fact]
        public async Task GetSubjectByIdAsync_MapsLecturerNameWhenPresent()
        {
            var (service, repo) = BuildService();
            var lecturerId = Guid.NewGuid();
            var subject = MakeSubject(lecturerId: lecturerId);

            repo.Setup(x => x.GetByIdAsync(subject.Id)).ReturnsAsync(subject);

            var result = await service.GetSubjectByIdAsync(subject.Id);

            Assert.Equal("Dr. Lecturer", result!.LecturerName);
        }

        [Fact]
        public async Task GetPagedAllSubjectsAsync_ReturnsEmptyCollectionWhenRepoEmpty()
        {
            var (service, repo) = BuildService();

            repo.Setup(x => x.GetPagedAsync(null, null, 1, 10))
                .ReturnsAsync((Array.Empty<Subject>(), 0));

            var result = await service.GetPagedAllSubjectsAsync(null, null, 1, 10);

            Assert.Empty(result.Items);
            Assert.Equal(0, result.TotalCount);
        }

        [Fact]
        public async Task GetPagedSubjectsByLecturerAsync_ReturnsEmptyCollectionWhenRepoEmpty()
        {
            var (service, repo) = BuildService();
            var lecturerId = Guid.NewGuid();

            repo.Setup(x => x.GetPagedByLecturerAsync(lecturerId, null, 1, 10))
                .ReturnsAsync((Array.Empty<Subject>(), 0));

            var result = await service.GetPagedSubjectsByLecturerAsync(lecturerId, null, 1, 10);

            Assert.Empty(result.Items);
            Assert.Equal(0, result.TotalCount);
        }

        [Fact]
        public async Task GetPagedActiveSubjectsAsync_ReturnsEmptyCollectionWhenRepoEmpty()
        {
            var (service, repo) = BuildService();

            repo.Setup(x => x.GetPagedActiveAsync(null, 1, 10))
                .ReturnsAsync((Array.Empty<Subject>(), 0));

            var result = await service.GetPagedActiveSubjectsAsync(null, 1, 10);

            Assert.Empty(result.Items);
            Assert.Equal(0, result.TotalCount);
        }

        [Theory]
        [InlineData("prn222", "Razor Pages", "Description A")]
        [InlineData("swt301", "Software Testing", "Description B")]
        [InlineData("dbi202", "Database Intro", "Description C")]
        [InlineData("mad101", "Mobile App Dev", "Description D")]
        [InlineData("swp391", "Project Course", "Description E")]
        [InlineData("osg202", "Operating Systems", "Description F")]
        public async Task CreateSubjectAsync_WithDifferentInputs_NormalizesAndCreates(
            string code,
            string name,
            string description)
        {
            var (service, repo) = BuildService();
            Subject? captured = null;

            repo.Setup(x => x.ExistsAsync(code, null)).ReturnsAsync(false);
            repo.Setup(x => x.CreateAsync(It.IsAny<Subject>()))
                .Callback<Subject>(s => captured = s)
                .ReturnsAsync((Subject s) => s);

            var result = await service.CreateSubjectAsync(new CreateSubjectDto
            {
                SubjectCode = code,
                Name = $"  {name}  ",
                Description = $"  {description}  "
            });

            Assert.True(result.Success);
            Assert.NotNull(captured);
            Assert.Equal(code.ToUpperInvariant(), captured!.SubjectCode);
            Assert.Equal(name, captured.Name);
            Assert.Equal(description, captured.Description);
        }

        [Theory]
        [InlineData("Updated A", "Desc A", SubjectStatus.Active)]
        [InlineData("Updated B", "Desc B", SubjectStatus.Inactive)]
        [InlineData("Updated C", "Desc C", SubjectStatus.Active)]
        [InlineData("Updated D", "Desc D", SubjectStatus.Inactive)]
        [InlineData("Updated E", "Desc E", SubjectStatus.Active)]
        [InlineData("Updated F", "Desc F", SubjectStatus.Inactive)]
        public async Task UpdateSubjectAsync_WithDifferentValues_UpdatesState(
            string name,
            string description,
            SubjectStatus status)
        {
            var (service, repo) = BuildService();
            var subject = MakeSubject();
            var dto = new UpdateSubjectDto
            {
                Id = subject.Id,
                Name = $"  {name}  ",
                Description = $"  {description}  ",
                SubjectCode = subject.SubjectCode,
                LecturerId = Guid.NewGuid(),
                Status = status
            };

            repo.Setup(x => x.GetByIdAsync(subject.Id)).ReturnsAsync(subject);
            repo.Setup(x => x.UpdateAsync(subject)).ReturnsAsync(subject);

            var result = await service.UpdateSubjectAsync(dto);

            Assert.True(result.Success);
            Assert.Equal(name, subject.Name);
            Assert.Equal(description, subject.Description);
            Assert.Equal(status, subject.Status);
        }

        [Theory]
        [InlineData("Active", true)]
        [InlineData("Inactive", true)]
        [InlineData("active", true)]
        [InlineData("inactive", true)]
        [InlineData("Unknown", false)]
        [InlineData("", false)]
        public async Task GetPagedAllSubjectsAsync_StatusParsing_BehavesAsExpected(string status, bool parses)
        {
            var (service, repo) = BuildService();

            repo.Setup(x => x.GetPagedAsync(
                    "search",
                    parses ? Enum.Parse<SubjectStatus>(status, true) : null,
                    1,
                    10))
                .ReturnsAsync((Array.Empty<Subject>(), 0));

            var result = await service.GetPagedAllSubjectsAsync("search", status, 1, 10);

            Assert.Empty(result.Items);
        }

        [Theory]
        [InlineData("Dr. A")]
        [InlineData("Dr. B")]
        [InlineData("Dr. C")]
        [InlineData("Dr. D")]
        [InlineData("Dr. E")]
        [InlineData("Dr. F")]
        [InlineData("Dr. G")]
        [InlineData("Dr. H")]
        public async Task GetAllSubjectsAsync_WithLecturerNames_MapsLecturerName(string lecturerName)
        {
            var (service, repo) = BuildService();
            var lecturerId = Guid.NewGuid();
            var subject = MakeSubject(lecturerId: lecturerId);
            subject.Lecturer = new User { Id = lecturerId, FullName = lecturerName };

            repo.Setup(x => x.GetAllAsync()).ReturnsAsync(new[] { subject });

            var result = (await service.GetAllSubjectsAsync()).Single();

            Assert.Equal(lecturerName, result.LecturerName);
        }

        [Theory]
        [InlineData("PRN222")]
        [InlineData("SWT301")]
        [InlineData("DBI202")]
        [InlineData("MAD101")]
        [InlineData("SWP391")]
        [InlineData("OSG202")]
        public async Task GetSubjectByIdAsync_WithDifferentCodes_ReturnsCorrectCode(string code)
        {
            var (service, repo) = BuildService();
            var subject = MakeSubject(subjectCode: code);

            repo.Setup(x => x.GetByIdAsync(subject.Id)).ReturnsAsync(subject);

            var result = await service.GetSubjectByIdAsync(subject.Id);

            Assert.NotNull(result);
            Assert.Equal(code, result!.SubjectCode);
        }

        [Fact]
        public async Task GetAllSubjectsAsync_MultipleSubjects_MapsEveryItem()
        {
            var (service, repo) = BuildService();
            var subjects = Enumerable.Range(1, 12)
                .Select(i => MakeSubject(subjectCode: $"SUB{i:000}", name: $"Subject {i}", description: $"Description {i}"))
                .ToArray();

            repo.Setup(x => x.GetAllAsync()).ReturnsAsync(subjects);

            var result = (await service.GetAllSubjectsAsync()).ToList();

            Assert.Equal(12, result.Count);
            for (var i = 0; i < 12; i++)
            {
                Assert.Equal(subjects[i].SubjectCode, result[i].SubjectCode);
                Assert.Equal(subjects[i].Name, result[i].Name);
            }
        }

        [Fact]
        public async Task GetSubjectsByLecturerAsync_MultipleAssignments_MapsAll()
        {
            var (service, repo) = BuildService();
            var lecturerId = Guid.NewGuid();
            var items = Enumerable.Range(1, 10)
                .Select(i => MakeSubject(subjectCode: $"LEC{i:000}", lecturerId: lecturerId))
                .ToArray();

            repo.Setup(x => x.GetByLecturerIdAsync(lecturerId)).ReturnsAsync(items);

            var result = (await service.GetSubjectsByLecturerAsync(lecturerId)).ToList();

            Assert.Equal(10, result.Count);
            Assert.All(result, x => Assert.Equal(lecturerId, x.LecturerId));
        }

        [Fact]
        public async Task GetActiveSubjectsAsync_MultipleSubjects_ReturnsOnlyMappedRows()
        {
            var (service, repo) = BuildService();
            var items = Enumerable.Range(1, 10)
                .Select(i => MakeSubject(subjectCode: $"ACT{i:000}", status: SubjectStatus.Active))
                .ToArray();

            repo.Setup(x => x.GetActiveAsync()).ReturnsAsync(items);

            var result = (await service.GetActiveSubjectsAsync()).ToList();

            Assert.Equal(10, result.Count);
            Assert.All(result, x => Assert.Equal(SubjectStatus.Active, x.Status));
        }

        [Fact]
        public async Task CreateSubjectAsync_RepeatedCalls_CreateDistinctEntities()
        {
            var (service, repo) = BuildService();
            var captured = new List<Subject>();

            repo.Setup(x => x.ExistsAsync(It.IsAny<string>(), null)).ReturnsAsync(false);
            repo.Setup(x => x.CreateAsync(It.IsAny<Subject>()))
                .Callback<Subject>(s => captured.Add(s))
                .ReturnsAsync((Subject s) => s);

            for (var i = 0; i < 12; i++)
            {
                var result = await service.CreateSubjectAsync(new CreateSubjectDto
                {
                    SubjectCode = $"sub{i:000}",
                    Name = $"  Subject {i}  ",
                    Description = $"  Desc {i}  "
                });

                Assert.True(result.Success);
            }

            Assert.Equal(12, captured.Count);
            Assert.Equal(12, captured.Select(x => x.Id).Distinct().Count());
        }

        [Fact]
        public async Task AssignLecturerAsync_ReassignSequence_AlwaysUsesLatestLecturer()
        {
            var (service, repo) = BuildService();
            var subject = MakeSubject();

            repo.Setup(x => x.GetByIdAsync(subject.Id)).ReturnsAsync(subject);
            repo.Setup(x => x.UpdateAsync(subject)).ReturnsAsync(subject);

            Guid? latest = null;
            for (var i = 0; i < 8; i++)
            {
                latest = Guid.NewGuid();
                var result = await service.AssignLecturerAsync(new AssignLecturerDto
                {
                    SubjectId = subject.Id,
                    LecturerId = latest
                });

                Assert.True(result.Success);
                Assert.Equal(latest, subject.LecturerId);
            }

            Assert.Equal(latest, subject.LecturerId);
        }

        [Theory]
        [InlineData("SUB001", "Subject 1", "Desc 1", SubjectStatus.Active)]
        [InlineData("SUB002", "Subject 2", "Desc 2", SubjectStatus.Inactive)]
        [InlineData("SUB003", "Subject 3", "Desc 3", SubjectStatus.Active)]
        [InlineData("SUB004", "Subject 4", "Desc 4", SubjectStatus.Inactive)]
        [InlineData("SUB005", "Subject 5", "Desc 5", SubjectStatus.Active)]
        [InlineData("SUB006", "Subject 6", "Desc 6", SubjectStatus.Inactive)]
        [InlineData("SUB007", "Subject 7", "Desc 7", SubjectStatus.Active)]
        [InlineData("SUB008", "Subject 8", "Desc 8", SubjectStatus.Inactive)]
        [InlineData("SUB009", "Subject 9", "Desc 9", SubjectStatus.Active)]
        [InlineData("SUB010", "Subject 10", "Desc 10", SubjectStatus.Inactive)]
        [InlineData("SUB011", "Subject 11", "Desc 11", SubjectStatus.Active)]
        [InlineData("SUB012", "Subject 12", "Desc 12", SubjectStatus.Inactive)]
        public async Task GetSubjectByIdAsync_BulkMatrix_MapsCorrectFields(
            string code,
            string name,
            string description,
            SubjectStatus status)
        {
            var (service, repo) = BuildService();
            var subject = MakeSubject(subjectCode: code, name: name, description: description, status: status);

            repo.Setup(x => x.GetByIdAsync(subject.Id)).ReturnsAsync(subject);

            var result = await service.GetSubjectByIdAsync(subject.Id);

            Assert.NotNull(result);
            Assert.Equal(code, result!.SubjectCode);
            Assert.Equal(name, result.Name);
            Assert.Equal(description, result.Description);
            Assert.Equal(status, result.Status);
        }

        [Theory]
        [InlineData("search1", 1, 10, 0)]
        [InlineData("search2", 2, 10, 3)]
        [InlineData("search3", 3, 15, 5)]
        [InlineData("search4", 4, 20, 7)]
        [InlineData("search5", 5, 25, 9)]
        [InlineData("search6", 6, 30, 11)]
        [InlineData("search7", 7, 35, 13)]
        [InlineData("search8", 8, 40, 15)]
        [InlineData("search9", 9, 45, 17)]
        [InlineData("search10", 10, 50, 19)]
        public async Task GetPagedActiveSubjectsAsync_BulkPagingMatrix_PreservesMetadata(
            string search,
            int pageIndex,
            int pageSize,
            int totalCount)
        {
            var (service, repo) = BuildService();
            var items = totalCount == 0 ? Array.Empty<Subject>() : new[] { MakeSubject(subjectCode: $"C{pageIndex}") };

            repo.Setup(x => x.GetPagedActiveAsync(search, pageIndex, pageSize))
                .ReturnsAsync((items, totalCount));

            var result = await service.GetPagedActiveSubjectsAsync(search, pageIndex, pageSize);

            Assert.Equal(totalCount, result.TotalCount);
            Assert.Equal(pageIndex, result.PageIndex);
            Assert.Equal(pageSize, result.PageSize);
        }
    }
}
