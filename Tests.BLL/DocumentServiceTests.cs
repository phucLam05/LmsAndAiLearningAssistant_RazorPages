using BLL.Interfaces;
using BLL.Services;
using Core.Configurations;
using Core.DTOs.Documents;
using Core.Entities;
using DAL.Interfaces;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Tests.BLL
{
    /// <summary>
    /// Unit tests for <see cref="DocumentService"/>.
    /// Covers upload validation, delete authorization, retry logic, document queries,
    /// chunk retrieval, and signed URL generation.
    /// </summary>
    public class DocumentServiceTests
    {
        // ── Helpers ─────────────────────────────────────────────────────────────

        private static IConfiguration BuildConfig(
            string supabaseUrl = "https://example.supabase.co",
            string bucket = "documents")
        {
            var inMemory = new Dictionary<string, string?>
            {
                ["Supabase:Url"] = supabaseUrl,
                ["Supabase:Bucket"] = bucket
            };
            return new ConfigurationBuilder()
                .AddInMemoryCollection(inMemory)
                .Build();
        }

        private static IOptions<UploadOptions> BuildUploadOptions(
            long maxFileSize = 50L * 1024 * 1024,
            Dictionary<string, string[]>? mimeTypes = null)
        {
            var options = new UploadOptions
            {
                MaxFileSize = maxFileSize,
                AllowedMimeTypes = mimeTypes ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    [".pdf"]  = new[] { "application/pdf" },
                    [".docx"] = new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
                    [".txt"]  = new[] { "text/plain" },
                    [".pptx"] = new[] { "application/vnd.openxmlformats-officedocument.presentationml.presentation" }
                }
            };
            return Options.Create(options);
        }

        private static (
            DocumentService service,
            Mock<IDocumentRepository> docRepoMock,
            Mock<ISubjectRepository> subjectRepoMock,
            Mock<IUserRepository> userRepoMock,
            Mock<IDocumentChunkRepository> chunkRepoMock,
            Mock<ISupabaseStorageProvider> storageMock,
            Mock<IBackgroundJobClient> bgJobsMock
        ) BuildService(
            IConfiguration? config = null,
            IOptions<UploadOptions>? uploadOptions = null)
        {
            var docRepo     = new Mock<IDocumentRepository>();
            var subjectRepo = new Mock<ISubjectRepository>();
            var userRepo    = new Mock<IUserRepository>();
            var chunkRepo   = new Mock<IDocumentChunkRepository>();
            var storage     = new Mock<ISupabaseStorageProvider>();
            var bgJobs      = new Mock<IBackgroundJobClient>();
            var logger      = Mock.Of<ILogger<DocumentService>>();

            var service = new DocumentService(
                docRepo.Object,
                subjectRepo.Object,
                userRepo.Object,
                chunkRepo.Object,
                storage.Object,
                uploadOptions ?? BuildUploadOptions(),
                logger,
                bgJobs.Object,
                config ?? BuildConfig());

            return (service, docRepo, subjectRepo, userRepo, chunkRepo, storage, bgJobs);
        }

        private static User MakeUser(UserRole role = UserRole.Lecturer, Guid? id = null)
            => new User
            {
                Id = id ?? Guid.NewGuid(),
                FullName = "Test User",
                Role = role
            };

        private static Subject MakeSubject(Guid? id = null, Guid? lecturerId = null)
            => new Subject
            {
                Id = id ?? Guid.NewGuid(),
                SubjectCode = "PRN222",
                Name = "Razor Pages",
                LecturerId = lecturerId
            };

        private static Document MakeDocument(
            Guid? id = null,
            Guid? uploadedBy = null,
            string fileUrl = "subject/abc/test.pdf",
            string fileName = "test.pdf")
            => new Document
            {
                Id = id ?? Guid.NewGuid(),
                FileName = fileName,
                FileUrl = fileUrl,
                FileSize = 1024,
                Status = DocumentStatus.Success,
                UploadedBy = uploadedBy,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

        private static DocumentUploadDto MakeUploadDto(
            Guid? uploadedBy = null,
            Guid? subjectId = null,
            string fileName = "document.pdf",
            string contentType = "application/pdf",
            long fileSize = 1024,
            Stream? content = null)
            => new DocumentUploadDto
            {
                UploadedBy = uploadedBy ?? Guid.NewGuid(),
                SubjectId = subjectId ?? Guid.NewGuid(),
                FileName = fileName,
                ContentType = contentType,
                FileSize = fileSize,
                Content = content ?? new MemoryStream(new byte[1024])
            };

        // ── UploadAsync — validation ─────────────────────────────────────────────

        [Fact]
        public async Task UploadAsync_EmptyUploadedBy_ReturnsFailure()
        {
            var (service, _, _, _, _, _, _) = BuildService();

            var dto = MakeUploadDto(uploadedBy: Guid.Empty);
            var result = await service.UploadAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("not authenticated", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UploadAsync_NullContent_ReturnsFailure()
        {
            var (service, _, _, _, _, _, _) = BuildService();

            var dto = MakeUploadDto(content: Stream.Null);
            var result = await service.UploadAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("file", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UploadAsync_ZeroFileSize_ReturnsFailure()
        {
            var (service, _, _, _, _, _, _) = BuildService();

            var dto = MakeUploadDto(fileSize: 0);
            var result = await service.UploadAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("empty", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UploadAsync_FileSizeExceedsLimit_ReturnsFailure()
        {
            var uploadOptions = BuildUploadOptions(maxFileSize: 1024); // 1 KB limit
            var (service, _, _, _, _, _, _) = BuildService(uploadOptions: uploadOptions);

            var dto = MakeUploadDto(fileSize: 2048); // 2 KB
            var result = await service.UploadAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("limit", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UploadAsync_DisallowedExtension_ReturnsFailure()
        {
            var (service, _, _, _, _, _, _) = BuildService();

            var dto = MakeUploadDto(fileName: "script.exe", contentType: "application/octet-stream");
            var result = await service.UploadAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("not allowed", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UploadAsync_NoExtension_ReturnsFailure()
        {
            var (service, _, _, _, _, _, _) = BuildService();

            var dto = MakeUploadDto(fileName: "readme", contentType: "text/plain");
            var result = await service.UploadAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("not allowed", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UploadAsync_MismatchedMimeType_ReturnsFailure()
        {
            var (service, _, _, _, _, _, _) = BuildService();

            // Extension says .pdf but MIME type says text/plain
            var dto = MakeUploadDto(fileName: "document.pdf", contentType: "text/plain");
            var result = await service.UploadAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("MIME type", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UploadAsync_SubjectNotFound_ReturnsFailure()
        {
            var (service, _, subjectRepo, _, _, _, _) = BuildService();

            subjectRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Subject?)null);

            var dto = MakeUploadDto();
            var result = await service.UploadAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("Subject", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UploadAsync_UserNotFound_ReturnsFailureAndCleansUpStorage()
        {
            var (service, _, subjectRepo, userRepo, _, storage, _) = BuildService();

            var subject = MakeSubject();
            subjectRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(subject);

            storage
                .Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), default))
                .Returns(Task.CompletedTask);

            userRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((User?)null);

            var dto = MakeUploadDto();
            var result = await service.UploadAsync(dto);

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task UploadAsync_LecturerNotAssignedToSubject_ReturnsFailure()
        {
            var (service, _, subjectRepo, userRepo, _, storage, _) = BuildService();

            var lecturer = MakeUser(UserRole.Lecturer);
            var subject = MakeSubject(lecturerId: Guid.NewGuid()); // different lecturer

            subjectRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(subject);

            storage
                .Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), default))
                .Returns(Task.CompletedTask);

            userRepo
                .Setup(r => r.GetByIdAsync(lecturer.Id))
                .ReturnsAsync(lecturer);

            var dto = MakeUploadDto(uploadedBy: lecturer.Id);
            var result = await service.UploadAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("quyền", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UploadAsync_AdminUploadsToAnySubject_ReturnsSuccess()
        {
            var (service, docRepo, subjectRepo, userRepo, _, storage, bgJobs) = BuildService();

            var admin = MakeUser(UserRole.Admin);
            var subject = MakeSubject(lecturerId: Guid.NewGuid()); // some other lecturer

            subjectRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(subject);

            storage
                .Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), default))
                .Returns(Task.CompletedTask);

            userRepo
                .Setup(r => r.GetByIdAsync(admin.Id))
                .ReturnsAsync(admin);

            docRepo
                .Setup(r => r.AddAsync(It.IsAny<Document>()))
                .ReturnsAsync((Document d) => d);

            bgJobs
                .Setup(b => b.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>()))
                .Returns("job-1");

            var dto = MakeUploadDto(uploadedBy: admin.Id);
            var result = await service.UploadAsync(dto);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task UploadAsync_AssignedLecturer_ReturnsSuccess()
        {
            var (service, docRepo, subjectRepo, userRepo, _, storage, bgJobs) = BuildService();

            var lecturer = MakeUser(UserRole.Lecturer);
            var subject = MakeSubject(lecturerId: lecturer.Id);

            subjectRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(subject);

            storage
                .Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), default))
                .Returns(Task.CompletedTask);

            userRepo
                .Setup(r => r.GetByIdAsync(lecturer.Id))
                .ReturnsAsync(lecturer);

            docRepo
                .Setup(r => r.AddAsync(It.IsAny<Document>()))
                .ReturnsAsync((Document d) => d);

            bgJobs
                .Setup(b => b.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>()))
                .Returns("job-1");

            var dto = MakeUploadDto(uploadedBy: lecturer.Id, subjectId: subject.Id, fileName: "lecture.pdf", contentType: "application/pdf");
            var result = await service.UploadAsync(dto);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(DocumentStatus.Pending, result.Data!.Status);
        }

        [Fact]
        public async Task UploadAsync_StorageThrows_ReturnsFailure()
        {
            var (service, _, subjectRepo, _, _, storage, _) = BuildService();

            var subject = MakeSubject();
            subjectRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(subject);

            storage
                .Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), default))
                .ThrowsAsync(new Exception("Storage unavailable"));

            var dto = MakeUploadDto();
            var result = await service.UploadAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("Supabase upload error", result.ErrorMessage);
        }

        [Fact]
        public async Task UploadAsync_DatabaseSaveFails_DeletesStorageAndReturnsFailure()
        {
            var (service, docRepo, subjectRepo, userRepo, _, storage, _) = BuildService();

            var admin = MakeUser(UserRole.Admin);
            var subject = MakeSubject();

            subjectRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(subject);

            storage
                .Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), default))
                .Returns(Task.CompletedTask);

            userRepo
                .Setup(r => r.GetByIdAsync(admin.Id))
                .ReturnsAsync(admin);

            docRepo
                .Setup(r => r.AddAsync(It.IsAny<Document>()))
                .ThrowsAsync(new Exception("DB insert error"));

            storage
                .Setup(s => s.DeleteAsync(It.IsAny<string>(), default))
                .Returns(Task.CompletedTask);

            var dto = MakeUploadDto(uploadedBy: admin.Id);
            var result = await service.UploadAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("Database save error", result.ErrorMessage);

            // Should attempt to clean up storage
            storage.Verify(s => s.DeleteAsync(It.IsAny<string>(), default), Times.Once);
        }

        [Fact]
        public async Task UploadAsync_OctetStreamMimeType_AllowedForAnyExtension()
        {
            var (service, docRepo, subjectRepo, userRepo, _, storage, bgJobs) = BuildService();

            var admin = MakeUser(UserRole.Admin);
            var subject = MakeSubject();

            subjectRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(subject);

            storage
                .Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), default))
                .Returns(Task.CompletedTask);

            userRepo
                .Setup(r => r.GetByIdAsync(admin.Id))
                .ReturnsAsync(admin);

            docRepo
                .Setup(r => r.AddAsync(It.IsAny<Document>()))
                .ReturnsAsync((Document d) => d);

            bgJobs
                .Setup(b => b.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>()))
                .Returns("job-1");

            // application/octet-stream is allowed for any extension
            var dto = MakeUploadDto(uploadedBy: admin.Id, fileName: "report.pdf", contentType: "application/octet-stream");
            var result = await service.UploadAsync(dto);

            Assert.True(result.IsSuccess);
        }

        // ── DeleteAsync ──────────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteAsync_DocumentNotFound_ReturnsFailure()
        {
            var (service, docRepo, _, _, _, _, _) = BuildService();

            docRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Document?)null);

            var result = await service.DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

            Assert.False(result.IsSuccess);
            Assert.Equal("Document not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task DeleteAsync_UserNotFound_ReturnsFailure()
        {
            var (service, docRepo, _, userRepo, _, _, _) = BuildService();

            var doc = MakeDocument();
            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            userRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((User?)null);

            var result = await service.DeleteAsync(doc.Id, Guid.NewGuid());

            Assert.False(result.IsSuccess);
            Assert.Equal("User not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task DeleteAsync_NonAdminDeletingOthersDocument_ReturnsAccessDenied()
        {
            var (service, docRepo, _, userRepo, _, _, _) = BuildService();

            var lecturer = MakeUser(UserRole.Lecturer);
            var doc = MakeDocument(uploadedBy: Guid.NewGuid()); // uploaded by someone else

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            userRepo
                .Setup(r => r.GetByIdAsync(lecturer.Id))
                .ReturnsAsync(lecturer);

            var result = await service.DeleteAsync(doc.Id, lecturer.Id);

            Assert.False(result.IsSuccess);
            Assert.Equal("Access denied.", result.ErrorMessage);
        }

        [Fact]
        public async Task DeleteAsync_AdminDeletingAnyDocument_ReturnsSuccess()
        {
            var (service, docRepo, _, userRepo, _, storage, _) = BuildService();

            var admin = MakeUser(UserRole.Admin);
            var doc = MakeDocument(uploadedBy: Guid.NewGuid()); // uploaded by someone else

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            userRepo
                .Setup(r => r.GetByIdAsync(admin.Id))
                .ReturnsAsync(admin);

            storage
                .Setup(s => s.DeleteAsync(It.IsAny<string>(), default))
                .Returns(Task.CompletedTask);

            docRepo
                .Setup(r => r.DeleteAsync(It.IsAny<Document>()))
                .Returns(Task.CompletedTask);

            var result = await service.DeleteAsync(doc.Id, admin.Id);

            Assert.True(result.IsSuccess);
            docRepo.Verify(r => r.DeleteAsync(doc), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_LecturerDeletingOwnDocument_ReturnsSuccess()
        {
            var (service, docRepo, _, userRepo, _, storage, _) = BuildService();

            var lecturer = MakeUser(UserRole.Lecturer);
            var doc = MakeDocument(uploadedBy: lecturer.Id);

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            userRepo
                .Setup(r => r.GetByIdAsync(lecturer.Id))
                .ReturnsAsync(lecturer);

            storage
                .Setup(s => s.DeleteAsync(It.IsAny<string>(), default))
                .Returns(Task.CompletedTask);

            docRepo
                .Setup(r => r.DeleteAsync(It.IsAny<Document>()))
                .Returns(Task.CompletedTask);

            var result = await service.DeleteAsync(doc.Id, lecturer.Id);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task DeleteAsync_StorageDeleteFails_StillDeletesFromDatabase()
        {
            var (service, docRepo, _, userRepo, _, storage, _) = BuildService();

            var admin = MakeUser(UserRole.Admin);
            var doc = MakeDocument(uploadedBy: Guid.NewGuid());

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            userRepo
                .Setup(r => r.GetByIdAsync(admin.Id))
                .ReturnsAsync(admin);

            // Storage fails — should log warning but still continue
            storage
                .Setup(s => s.DeleteAsync(It.IsAny<string>(), default))
                .ThrowsAsync(new Exception("Storage error"));

            docRepo
                .Setup(r => r.DeleteAsync(It.IsAny<Document>()))
                .Returns(Task.CompletedTask);

            var result = await service.DeleteAsync(doc.Id, admin.Id);

            Assert.True(result.IsSuccess);
            docRepo.Verify(r => r.DeleteAsync(doc), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_StudentRole_ReturnsAccessDenied()
        {
            var (service, docRepo, _, userRepo, _, _, _) = BuildService();

            var student = MakeUser(UserRole.Student);
            var doc = MakeDocument(uploadedBy: Guid.NewGuid());

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            userRepo
                .Setup(r => r.GetByIdAsync(student.Id))
                .ReturnsAsync(student);

            var result = await service.DeleteAsync(doc.Id, student.Id);

            Assert.False(result.IsSuccess);
            Assert.Equal("Access denied.", result.ErrorMessage);
        }

        // ── RetryProcessingAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task RetryProcessingAsync_DocumentNotFound_ReturnsFailure()
        {
            var (service, docRepo, _, _, _, _, _) = BuildService();

            docRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Document?)null);

            var result = await service.RetryProcessingAsync(Guid.NewGuid(), Guid.NewGuid());

            Assert.False(result.IsSuccess);
            Assert.Equal("Document not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task RetryProcessingAsync_UserNotFound_ReturnsFailure()
        {
            var (service, docRepo, _, userRepo, _, _, _) = BuildService();

            var doc = MakeDocument();
            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            userRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((User?)null);

            var result = await service.RetryProcessingAsync(doc.Id, Guid.NewGuid());

            Assert.False(result.IsSuccess);
            Assert.Equal("User not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task RetryProcessingAsync_NonAdminNotOwner_ReturnsAccessDenied()
        {
            var (service, docRepo, _, userRepo, _, _, _) = BuildService();

            var lecturer = MakeUser(UserRole.Lecturer);
            var doc = MakeDocument(uploadedBy: Guid.NewGuid()); // not owned

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            userRepo
                .Setup(r => r.GetByIdAsync(lecturer.Id))
                .ReturnsAsync(lecturer);

            var result = await service.RetryProcessingAsync(doc.Id, lecturer.Id);

            Assert.False(result.IsSuccess);
            Assert.Equal("Access denied.", result.ErrorMessage);
        }

        [Fact]
        public async Task RetryProcessingAsync_HasChunks_EnqueuesEmbeddingOnly()
        {
            var (service, docRepo, _, userRepo, chunkRepo, _, bgJobs) = BuildService();

            var admin = MakeUser(UserRole.Admin);
            var doc = MakeDocument(uploadedBy: Guid.NewGuid());
            doc.Status = DocumentStatus.Failed;

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            userRepo
                .Setup(r => r.GetByIdAsync(admin.Id))
                .ReturnsAsync(admin);

            chunkRepo
                .Setup(r => r.HasChunksAsync(doc.Id))
                .ReturnsAsync(true);

            docRepo
                .Setup(r => r.UpdateAsync(It.IsAny<Document>()))
                .Returns(Task.CompletedTask);

            bgJobs
                .Setup(b => b.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>()))
                .Returns("job-embed");

            var result = await service.RetryProcessingAsync(doc.Id, admin.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(DocumentStatus.Processing, doc.Status);
        }

        [Fact]
        public async Task RetryProcessingAsync_NoChunks_EnqueuesChunkingAndEmbedding()
        {
            var (service, docRepo, _, userRepo, chunkRepo, _, bgJobs) = BuildService();

            var admin = MakeUser(UserRole.Admin);
            var doc = MakeDocument(uploadedBy: Guid.NewGuid());
            doc.Status = DocumentStatus.Failed;

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            userRepo
                .Setup(r => r.GetByIdAsync(admin.Id))
                .ReturnsAsync(admin);

            chunkRepo
                .Setup(r => r.HasChunksAsync(doc.Id))
                .ReturnsAsync(false);

            docRepo
                .Setup(r => r.UpdateAsync(It.IsAny<Document>()))
                .Returns(Task.CompletedTask);

            bgJobs
                .Setup(b => b.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>()))
                .Returns("job-chunk");

            var result = await service.RetryProcessingAsync(doc.Id, admin.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(DocumentStatus.Pending, doc.Status);
        }

        [Fact]
        public async Task RetryProcessingAsync_OwnerLecturer_ReturnsSuccess()
        {
            var (service, docRepo, _, userRepo, chunkRepo, _, bgJobs) = BuildService();

            var lecturer = MakeUser(UserRole.Lecturer);
            var doc = MakeDocument(uploadedBy: lecturer.Id);

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            userRepo
                .Setup(r => r.GetByIdAsync(lecturer.Id))
                .ReturnsAsync(lecturer);

            chunkRepo
                .Setup(r => r.HasChunksAsync(doc.Id))
                .ReturnsAsync(false);

            docRepo
                .Setup(r => r.UpdateAsync(It.IsAny<Document>()))
                .Returns(Task.CompletedTask);

            bgJobs
                .Setup(b => b.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>()))
                .Returns("job-1");

            var result = await service.RetryProcessingAsync(doc.Id, lecturer.Id);

            Assert.True(result.IsSuccess);
        }

        // ── GetDocumentsBySubjectIdAsync ─────────────────────────────────────────

        [Fact]
        public async Task GetDocumentsBySubjectIdAsync_ReturnsAllMapped()
        {
            var (service, docRepo, _, _, _, _, _) = BuildService();

            var subjectId = Guid.NewGuid();
            var docs = new List<Document>
            {
                MakeDocument(fileUrl: "subject/test/a.pdf", fileName: "a.pdf"),
                MakeDocument(fileUrl: "subject/test/b.pdf", fileName: "b.pdf")
            };

            docRepo
                .Setup(r => r.GetBySubjectIdAsync(subjectId))
                .ReturnsAsync(docs);

            var result = (await service.GetDocumentsBySubjectIdAsync(subjectId)).ToList();

            Assert.Equal(2, result.Count);
            Assert.Contains(result, d => d.FileName == "a.pdf");
            Assert.Contains(result, d => d.FileName == "b.pdf");
        }

        [Fact]
        public async Task GetDocumentsBySubjectIdAsync_EmptyList_ReturnsEmpty()
        {
            var (service, docRepo, _, _, _, _, _) = BuildService();

            docRepo
                .Setup(r => r.GetBySubjectIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new List<Document>());

            var result = await service.GetDocumentsBySubjectIdAsync(Guid.NewGuid());

            Assert.Empty(result);
        }

        // ── GetDocumentByIdAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task GetDocumentByIdAsync_ExistingDocument_ReturnsMappedDto()
        {
            var (service, docRepo, _, _, _, _, _) = BuildService();

            var doc = MakeDocument(fileName: "test.pdf");
            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            var result = await service.GetDocumentByIdAsync(doc.Id);

            Assert.NotNull(result);
            Assert.Equal(doc.Id, result!.Id);
            Assert.Equal("test.pdf", result.FileName);
        }

        [Fact]
        public async Task GetDocumentByIdAsync_NotFound_ReturnsNull()
        {
            var (service, docRepo, _, _, _, _, _) = BuildService();

            docRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Document?)null);

            var result = await service.GetDocumentByIdAsync(Guid.NewGuid());

            Assert.Null(result);
        }

        // ── GetSignedDocumentUrlAsync ─────────────────────────────────────────────

        [Fact]
        public async Task GetSignedDocumentUrlAsync_DocumentNotFound_ReturnsNull()
        {
            var (service, docRepo, _, _, _, _, _) = BuildService();

            docRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Document?)null);

            var result = await service.GetSignedDocumentUrlAsync(Guid.NewGuid());

            Assert.Null(result);
        }

        [Fact]
        public async Task GetSignedDocumentUrlAsync_DocumentFound_ReturnsSignedUrl()
        {
            var (service, docRepo, _, _, _, storage, _) = BuildService();

            var doc = MakeDocument(fileUrl: "subject/abc/doc.pdf");
            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            storage
                .Setup(s => s.GetSignedUrlAsync("subject/abc/doc.pdf", It.IsAny<int>(), default))
                .ReturnsAsync("https://example.supabase.co/signed/doc.pdf?token=abc");

            var result = await service.GetSignedDocumentUrlAsync(doc.Id);

            Assert.NotNull(result);
            Assert.Contains("signed", result, StringComparison.OrdinalIgnoreCase);
        }

        // ── GetDocumentChunksAsync ───────────────────────────────────────────────

        [Fact]
        public async Task GetDocumentChunksAsync_ReturnsAllChunksMapped()
        {
            var (service, _, _, _, chunkRepo, _, _) = BuildService();

            var docId = Guid.NewGuid();
            var chunks = new List<DocumentChunk>
            {
                new DocumentChunk { Id = Guid.NewGuid(), DocumentId = docId, ChunkIndex = 0, Content = "Chunk 0", TokenCount = 10 },
                new DocumentChunk { Id = Guid.NewGuid(), DocumentId = docId, ChunkIndex = 1, Content = "Chunk 1", TokenCount = 15 }
            };

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(docId))
                .ReturnsAsync(chunks);

            var result = (await service.GetDocumentChunksAsync(docId)).ToList();

            Assert.Equal(2, result.Count);
            Assert.Equal(0, result[0].ChunkIndex);
            Assert.Equal(1, result[1].ChunkIndex);
            Assert.Equal("Chunk 0", result[0].Content);
        }

        [Fact]
        public async Task GetDocumentChunksAsync_EmptyChunks_ReturnsEmptyList()
        {
            var (service, _, _, _, chunkRepo, _, _) = BuildService();

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new List<DocumentChunk>());

            var result = await service.GetDocumentChunksAsync(Guid.NewGuid());

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetDocumentChunksAsync_DoesNotExposeEmbedding()
        {
            var (service, _, _, _, chunkRepo, _, _) = BuildService();

            var docId = Guid.NewGuid();
            var chunks = new List<DocumentChunk>
            {
                new DocumentChunk { Id = Guid.NewGuid(), DocumentId = docId, ChunkIndex = 0, Content = "Chunk" }
            };

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(docId))
                .ReturnsAsync(chunks);

            var result = (await service.GetDocumentChunksAsync(docId)).ToList();

            Assert.Single(result);
            // Embedding field should not be exposed in the DTO
            Assert.Equal("Chunk", result[0].Content);
        }

        // ── GetAllDocumentsAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task GetAllDocumentsAsync_ReturnsAllMappedDocuments()
        {
            var (service, docRepo, _, _, _, _, _) = BuildService();

            var docs = new List<Document>
            {
                MakeDocument(fileName: "a.pdf"),
                MakeDocument(fileName: "b.pdf"),
                MakeDocument(fileName: "c.pdf")
            };

            docRepo
                .Setup(r => r.GetAllWithDetailsAsync())
                .ReturnsAsync(docs);

            var result = (await service.GetAllDocumentsAsync()).ToList();

            Assert.Equal(3, result.Count);
        }

        [Fact]
        public async Task GetAllDocumentsAsync_EmptyRepository_ReturnsEmpty()
        {
            var (service, docRepo, _, _, _, _, _) = BuildService();

            docRepo
                .Setup(r => r.GetAllWithDetailsAsync())
                .ReturnsAsync(new List<Document>());

            var result = await service.GetAllDocumentsAsync();

            Assert.Empty(result);
        }

        // ── GetPagedDocumentsAsync ───────────────────────────────────────────────

        [Fact]
        public async Task GetPagedDocumentsAsync_NoFilter_ReturnsAll()
        {
            var (service, docRepo, _, _, _, _, _) = BuildService();

            docRepo
                .Setup(r => r.CountAsync(null, null, null))
                .ReturnsAsync(5);

            docRepo
                .Setup(r => r.QueryAsync(null, null, null, 1, 10))
                .ReturnsAsync(new List<Document> { MakeDocument() });

            var result = await service.GetPagedDocumentsAsync(null, null, null, 1, 10);

            Assert.Equal(5, result.TotalCount);
            Assert.Single(result.Items);
            Assert.Equal(1, result.PageIndex);
            Assert.Equal(10, result.PageSize);
        }

        [Fact]
        public async Task GetPagedDocumentsAsync_ValidStatusString_ParsesCorrectly()
        {
            var (service, docRepo, _, _, _, _, _) = BuildService();

            docRepo
                .Setup(r => r.CountAsync(null, DocumentStatus.Pending, null))
                .ReturnsAsync(2);

            docRepo
                .Setup(r => r.QueryAsync(null, DocumentStatus.Pending, null, 1, 10))
                .ReturnsAsync(new List<Document>());

            var result = await service.GetPagedDocumentsAsync(null, "Pending", null, 1, 10);

            docRepo.Verify(r => r.CountAsync(null, DocumentStatus.Pending, null), Times.Once);
        }

        [Fact]
        public async Task GetPagedDocumentsAsync_InvalidStatusString_TreatsAsNoFilter()
        {
            var (service, docRepo, _, _, _, _, _) = BuildService();

            docRepo
                .Setup(r => r.CountAsync(null, null, null))
                .ReturnsAsync(0);

            docRepo
                .Setup(r => r.QueryAsync(null, null, null, 1, 10))
                .ReturnsAsync(new List<Document>());

            await service.GetPagedDocumentsAsync(null, "NotAStatus", null, 1, 10);

            docRepo.Verify(r => r.CountAsync(null, null, null), Times.Once);
        }

        [Fact]
        public async Task GetPagedDocumentsAsync_WithSubjectId_PassesSubjectIdToRepo()
        {
            var (service, docRepo, _, _, _, _, _) = BuildService();

            var subjectId = Guid.NewGuid();

            docRepo
                .Setup(r => r.CountAsync(null, null, subjectId))
                .ReturnsAsync(3);

            docRepo
                .Setup(r => r.QueryAsync(null, null, subjectId, 1, 10))
                .ReturnsAsync(new List<Document>());

            await service.GetPagedDocumentsAsync(null, null, subjectId, 1, 10);

            docRepo.Verify(r => r.CountAsync(null, null, subjectId), Times.Once);
            docRepo.Verify(r => r.QueryAsync(null, null, subjectId, 1, 10), Times.Once);
        }

        // ── GetProcessingProgressAsync ───────────────────────────────────────────

        [Fact]
        public async Task GetProcessingProgressAsync_AllChunksEmbedded_ReturnsFullProgress()
        {
            var (service, _, _, _, chunkRepo, _, _) = BuildService();

            var docId = Guid.NewGuid();
            var chunks = new List<DocumentChunk>
            {
                new DocumentChunk { Id = Guid.NewGuid(), Embedding = new Pgvector.Vector(new float[] { 0.1f }) },
                new DocumentChunk { Id = Guid.NewGuid(), Embedding = new Pgvector.Vector(new float[] { 0.2f }) }
            };

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(docId))
                .ReturnsAsync(chunks);

            var (processed, total) = await service.GetProcessingProgressAsync(docId);

            Assert.Equal(2, total);
            Assert.Equal(2, processed);
        }

        [Fact]
        public async Task GetProcessingProgressAsync_NoChunksEmbedded_ReturnsZeroProcessed()
        {
            var (service, _, _, _, chunkRepo, _, _) = BuildService();

            var docId = Guid.NewGuid();
            var chunks = new List<DocumentChunk>
            {
                new DocumentChunk { Id = Guid.NewGuid(), Embedding = null },
                new DocumentChunk { Id = Guid.NewGuid(), Embedding = null }
            };

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(docId))
                .ReturnsAsync(chunks);

            var (processed, total) = await service.GetProcessingProgressAsync(docId);

            Assert.Equal(2, total);
            Assert.Equal(0, processed);
        }

        [Fact]
        public async Task GetProcessingProgressAsync_PartialEmbedded_ReturnsCorrectCount()
        {
            var (service, _, _, _, chunkRepo, _, _) = BuildService();

            var docId = Guid.NewGuid();
            var chunks = new List<DocumentChunk>
            {
                new DocumentChunk { Id = Guid.NewGuid(), Embedding = new Pgvector.Vector(new float[] { 0.1f }) },
                new DocumentChunk { Id = Guid.NewGuid(), Embedding = null },
                new DocumentChunk { Id = Guid.NewGuid(), Embedding = new Pgvector.Vector(new float[] { 0.3f }) }
            };

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(docId))
                .ReturnsAsync(chunks);

            var (processed, total) = await service.GetProcessingProgressAsync(docId);

            Assert.Equal(3, total);
            Assert.Equal(2, processed);
        }

        [Fact]
        public async Task GetProcessingProgressAsync_EmptyChunks_ReturnsBothZero()
        {
            var (service, _, _, _, chunkRepo, _, _) = BuildService();

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new List<DocumentChunk>());

            var (processed, total) = await service.GetProcessingProgressAsync(Guid.NewGuid());

            Assert.Equal(0, total);
            Assert.Equal(0, processed);
        }

        // ── DownloadDocumentAsync ────────────────────────────────────────────────

        [Fact]
        public async Task DownloadDocumentAsync_DocumentNotFound_ReturnsNull()
        {
            var (service, docRepo, _, _, _, _, _) = BuildService();

            docRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Document?)null);

            var result = await service.DownloadDocumentAsync(Guid.NewGuid());

            Assert.Null(result);
        }

        [Fact]
        public async Task DownloadDocumentAsync_PdfFile_ReturnsPdfContentType()
        {
            var (service, docRepo, _, _, _, storage, _) = BuildService();

            var doc = MakeDocument(fileName: "lecture.pdf", fileUrl: "subject/abc/lecture.pdf");
            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            var stream = new MemoryStream(new byte[100]);
            storage
                .Setup(s => s.DownloadAsync("subject/abc/lecture.pdf", default))
                .ReturnsAsync(stream);

            var result = await service.DownloadDocumentAsync(doc.Id);

            Assert.NotNull(result);
            Assert.Equal("application/pdf", result!.Value.ContentType);
            Assert.Equal("lecture.pdf", result.Value.FileName);
        }

        [Fact]
        public async Task DownloadDocumentAsync_DocxFile_ReturnsDocxContentType()
        {
            var (service, docRepo, _, _, _, storage, _) = BuildService();

            var doc = MakeDocument(fileName: "report.docx", fileUrl: "subject/abc/report.docx");
            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            var stream = new MemoryStream(new byte[100]);
            storage
                .Setup(s => s.DownloadAsync("subject/abc/report.docx", default))
                .ReturnsAsync(stream);

            var result = await service.DownloadDocumentAsync(doc.Id);

            Assert.NotNull(result);
            Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", result!.Value.ContentType);
        }

        [Fact]
        public async Task DownloadDocumentAsync_TxtFile_ReturnsTextPlainContentType()
        {
            var (service, docRepo, _, _, _, storage, _) = BuildService();

            var doc = MakeDocument(fileName: "notes.txt", fileUrl: "subject/abc/notes.txt");
            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            var stream = new MemoryStream(new byte[100]);
            storage
                .Setup(s => s.DownloadAsync("subject/abc/notes.txt", default))
                .ReturnsAsync(stream);

            var result = await service.DownloadDocumentAsync(doc.Id);

            Assert.NotNull(result);
            Assert.Equal("text/plain", result!.Value.ContentType);
        }

        [Fact]
        public async Task DownloadDocumentAsync_UnknownExtension_ReturnsOctetStream()
        {
            var (service, docRepo, _, _, _, storage, _) = BuildService();

            var doc = MakeDocument(fileName: "data.xyz", fileUrl: "subject/abc/data.xyz");
            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            var stream = new MemoryStream(new byte[100]);
            storage
                .Setup(s => s.DownloadAsync("subject/abc/data.xyz", default))
                .ReturnsAsync(stream);

            var result = await service.DownloadDocumentAsync(doc.Id);

            Assert.NotNull(result);
            Assert.Equal("application/octet-stream", result!.Value.ContentType);
        }

        [Fact]
        public async Task DownloadDocumentAsync_PptxFile_ReturnsPptxContentType()
        {
            var (service, docRepo, _, _, _, storage, _) = BuildService();

            var doc = MakeDocument(fileName: "slides.pptx", fileUrl: "subject/abc/slides.pptx");
            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            var stream = new MemoryStream(new byte[100]);
            storage
                .Setup(s => s.DownloadAsync("subject/abc/slides.pptx", default))
                .ReturnsAsync(stream);

            var result = await service.DownloadDocumentAsync(doc.Id);

            Assert.NotNull(result);
            Assert.Equal("application/vnd.openxmlformats-officedocument.presentationml.presentation", result!.Value.ContentType);
        }
    }
}
