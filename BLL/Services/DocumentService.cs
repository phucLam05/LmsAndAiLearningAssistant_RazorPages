using BLL.Interfaces;
using Core.DTOs.Common;
using Core.DTOs.Documents;
using Core.Entities;
using DAL.Interfaces;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Configurations;

namespace BLL.Services
{
    /// <summary>
    /// Coordinates document upload, storage in Supabase, and Hangfire background processing.
    /// </summary>
    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly ISubjectRepository _subjectRepository;
        private readonly IUserRepository _userRepository;
        private readonly IDocumentChunkRepository _documentChunkRepository;
        private readonly ISupabaseStorageProvider _storageService;
        private readonly UploadOptions _uploadOptions;
        private readonly ILogger<DocumentService> _logger;
        private readonly IBackgroundJobClient _backgroundJobs;
        private readonly string _supabaseUrl;
        private readonly string _bucket;

        public DocumentService(
            IDocumentRepository documentRepository,
            ISubjectRepository subjectRepository,
            IUserRepository userRepository,
            IDocumentChunkRepository documentChunkRepository,
            ISupabaseStorageProvider storageService,
            IOptions<UploadOptions> uploadOptions,
            ILogger<DocumentService> logger,
            IBackgroundJobClient backgroundJobs,
            IConfiguration configuration)
        {
            _documentRepository = documentRepository;
            _subjectRepository = subjectRepository;
            _userRepository = userRepository;
            _documentChunkRepository = documentChunkRepository;
            _storageService = storageService;
            _uploadOptions = uploadOptions.Value;
            _logger = logger;
            _backgroundJobs = backgroundJobs;

            var supabaseUrl = configuration["Supabase:Url"] ?? "";
            if (Uri.TryCreate(supabaseUrl, UriKind.Absolute, out var uri))
            {
                _supabaseUrl = $"{uri.Scheme}://{uri.Host}";
                if (uri.Port != 80 && uri.Port != 443)
                {
                    _supabaseUrl += $":{uri.Port}";
                }
            }
            else
            {
                _supabaseUrl = supabaseUrl.TrimEnd('/');
            }
            _bucket = configuration["Supabase:Bucket"] ?? "Document";
        }

        public async Task<IReadOnlyList<DocumentDto>> GetDocumentsBySubjectIdAsync(Guid subjectId)
        {
            var documents = await _documentRepository.GetBySubjectIdAsync(subjectId);
            return documents.Select(MapDocument).ToList();
        }

        public async Task<IReadOnlyList<DocumentDto>> GetVisibleDocumentsBySubjectIdAsync(Guid subjectId, UserRole role)
        {
            var documents = await _documentRepository.GetBySubjectIdAsync(subjectId);
            if (role == UserRole.Student)
                documents = documents.Where(d => d.Status == DocumentStatus.Success).ToList();
            return documents.Select(MapDocument).ToList();
        }

        public async Task<DocumentDto?> GetDocumentByIdAsync(Guid documentId)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);
            return document != null ? MapDocument(document) : null;
        }

        public async Task<string?> GetSignedDocumentUrlAsync(Guid documentId)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document == null) return null;
            return await _storageService.GetSignedUrlAsync(document.FileUrl);
        }

        public async Task<(Stream Stream, string ContentType, string FileName)?> DownloadDocumentAsync(Guid documentId)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document == null) return null;

            var stream = await _storageService.DownloadAsync(document.FileUrl, CancellationToken.None);
            
            // Basic content type resolution
            var ext = Path.GetExtension(document.FileName).ToLowerInvariant();
            var contentType = ext switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".doc" => "application/msword",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".xls" => "application/vnd.ms-excel",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
            };

            return (stream, contentType, document.FileName);
        }

        public async Task<IReadOnlyList<DocumentChunkDto>> GetDocumentChunksAsync(Guid documentId)
        {
            var chunks = await _documentChunkRepository.GetChunksByDocumentIdAsync(documentId);
            return chunks.Select(c => new DocumentChunkDto
            {
                Id = c.Id,
                DocumentId = c.DocumentId ?? Guid.Empty,
                ChunkIndex = c.ChunkIndex,
                Content = c.Content,
                TokenCount = c.TokenCount,
                PageNumber = c.PageNumber,
                // Don't expose large embedding vectors to the UI unless absolutely needed.
                CreatedAt = c.CreatedAt
            }).ToList();
        }

        public async Task<Result<DocumentDto>> UploadAsync(DocumentUploadDto uploadDto)
        {
            var validationError = ValidateUpload(uploadDto);
            if (!string.IsNullOrEmpty(validationError))
            {
                return Result<DocumentDto>.Failure(validationError);
            }

            var subject = await _subjectRepository.GetByIdAsync(uploadDto.SubjectId);
            if (subject == null)
            {
                return Result<DocumentDto>.Failure("Subject does not exist.");
            }

            var extension = Path.GetExtension(uploadDto.FileName).ToLowerInvariant();
            var storedFileName = $"{Guid.NewGuid():N}{extension}";
            var storagePath = BuildStoragePath(uploadDto.SubjectId, storedFileName);
            var now = DateTime.UtcNow;

            try
            {
                await _storageService.UploadAsync(storagePath, uploadDto.Content, NormalizeContentType(uploadDto.ContentType));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload file to storage for subject {SubjectId}", uploadDto.SubjectId);
                return Result<DocumentDto>.Failure($"Supabase upload error: {ex.Message}");
            }

            try
            {
                var uploaderUser = await _userRepository.GetByIdAsync(uploadDto.UploadedBy);
                if (uploaderUser == null)
                {
                    return Result<DocumentDto>.Failure("Người dùng không tồn tại.");
                }

                if (uploaderUser.Role != UserRole.Admin && subject.LecturerId != uploadDto.UploadedBy)
                {
                    return Result<DocumentDto>.Failure("Bạn không có quyền upload tài liệu cho môn học này.");
                }

                var document = new Document
                {
                    Id = Guid.NewGuid(),
                    SubjectId = subject.Id,
                    UploadedBy = uploadDto.UploadedBy,
                    FileName = Path.GetFileName(uploadDto.FileName),
                    FileUrl = storagePath,
                    FileSize = uploadDto.FileSize,
                    Status = DocumentStatus.Pending,
                    CreatedAt = now,
                    UpdatedAt = now,
                    UpdatedBy = uploadDto.UploadedBy
                };

                await _documentRepository.AddAsync(document);

                // Enqueue the chunking job, followed by the embedding job
                var chunkingJobId = _backgroundJobs.Enqueue<IChunkingService>(x => x.ProcessFileChunkingAsync(document.Id, CancellationToken.None));
                var embeddingJobId = _backgroundJobs.ContinueJobWith<IEmbeddingService>(chunkingJobId, x => x.ProcessEmbeddingsAsync(document.Id, CancellationToken.None));

                // Normally we would save the JobIds if the entity supported it, but we can just fire-and-forget for now.
                
                return Result<DocumentDto>.Success(MapDocument(document));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save document metadata for subject {SubjectId}", uploadDto.SubjectId);
                try
                {
                    await _storageService.DeleteAsync(storagePath);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogError(cleanupEx, "Failed to cleanup storage after DB save error.");
                }
                return Result<DocumentDto>.Failure($"Database save error: {ex.Message}");
            }
        }

        public async Task<Result> DeleteAsync(Guid documentId, Guid userId)
        {
            try
            {
                var document = await _documentRepository.GetByIdAsync(documentId);
                if (document == null)
                {
                    return Result.Failure("Document not found.");
                }

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    return Result.Failure("User not found.");
                }

                // Admins can delete any document; Lecturers can only delete their own
                if (user.Role != UserRole.Admin && document.UploadedBy != userId)
                {
                    return Result.Failure("Access denied.");
                }

                try
                {
                    await _storageService.DeleteAsync(document.FileUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete file from Supabase storage. Path: {StoragePath}", document.FileUrl);
                }

                await _documentRepository.DeleteAsync(document);
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete document {DocumentId}", documentId);
                return Result.Failure($"Delete error: {ex.Message}");
            }
        }

        public async Task<Result> RetryProcessingAsync(Guid documentId, Guid userId)
        {
            try
            {
                var document = await _documentRepository.GetByIdAsync(documentId);
                if (document == null)
                {
                    return Result.Failure("Document not found.");
                }

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    return Result.Failure("User not found.");
                }

                if (user.Role != UserRole.Admin && document.UploadedBy != userId)
                {
                    return Result.Failure("Access denied.");
                }

                bool hasChunks = await _documentChunkRepository.HasChunksAsync(documentId);
                if (hasChunks)
                {
                    document.Status = DocumentStatus.Processing;
                    var embeddingJobId = _backgroundJobs.Enqueue<IEmbeddingService>(x => x.ProcessEmbeddingsAsync(documentId, CancellationToken.None));
                }
                else
                {
                    document.Status = DocumentStatus.Pending;
                    var chunkingJobId = _backgroundJobs.Enqueue<IChunkingService>(x => x.ProcessFileChunkingAsync(documentId, CancellationToken.None));
                    var embeddingJobId = _backgroundJobs.ContinueJobWith<IEmbeddingService>(chunkingJobId, x => x.ProcessEmbeddingsAsync(documentId, CancellationToken.None));
                }

                document.UpdatedBy = userId;
                await _documentRepository.UpdateAsync(document);
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retry processing for document {DocumentId}", documentId);
                return Result.Failure($"Retry processing error: {ex.Message}");
            }
        }

        public async Task<IReadOnlyList<DocumentDto>> GetAllDocumentsAsync()
        {
            var documents = await _documentRepository.GetAllWithDetailsAsync();
            return documents.Select(MapDocument).ToList();
        }

        public async Task<PagedResult<DocumentDto>> GetPagedDocumentsAsync(
            string? search, string? status, Guid? subjectId, int pageIndex, int pageSize)
        {
            var statusEnum = !string.IsNullOrWhiteSpace(status) && Enum.TryParse<DocumentStatus>(status, true, out var s)
                ? s : (DocumentStatus?)null;

            var total = await _documentRepository.CountAsync(search, statusEnum, subjectId);
            var docs = await _documentRepository.QueryAsync(search, statusEnum, subjectId, pageIndex, pageSize);

            return new PagedResult<DocumentDto>
            {
                Items = docs.Select(MapDocument).ToList(),
                TotalCount = total,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }

        private string ValidateUpload(DocumentUploadDto uploadDto)
        {
            if (uploadDto.UploadedBy == Guid.Empty) return "User is not authenticated.";
            if (uploadDto.Content == Stream.Null) return "Please choose a file.";
            if (uploadDto.FileSize <= 0) return "File is empty.";
            if (uploadDto.FileSize > _uploadOptions.MaxFileSize) return $"File exceeds the limit of {_uploadOptions.MaxFileSize / (1024 * 1024)}MB.";

            var extension = Path.GetExtension(uploadDto.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !_uploadOptions.AllowedMimeTypes.TryGetValue(extension, out var expectedMimeTypes))
            {
                return "This file type is not allowed for upload.";
            }

            var contentType = NormalizeContentType(uploadDto.ContentType);
            if (contentType != "application/octet-stream" && !expectedMimeTypes.Any(expected => string.Equals(expected, contentType, StringComparison.OrdinalIgnoreCase)))
            {
                return "File MIME type does not match the selected file extension.";
            }

            return string.Empty;
        }

        private static string BuildStoragePath(Guid subjectId, string storedFileName)
        {
            return $"subject/{subjectId}/{storedFileName}";
        }

        private static string NormalizeContentType(string contentType)
        {
            return string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
        }

        private string GetAbsoluteStorageUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return string.Empty;
            if (url.StartsWith("http://") || url.StartsWith("https://"))
                return url;

            return $"{_supabaseUrl}/storage/v1/object/public/{_bucket}/{url.TrimStart('/')}";
        }

        private DocumentDto MapDocument(Document document)
        {
            return new DocumentDto
            {
                Id = document.Id,
                SubjectId = document.SubjectId,
                UploadedBy = document.UploadedBy,
                FileName = document.FileName,
                FileUrl = GetAbsoluteStorageUrl(document.FileUrl),
                FileSize = document.FileSize,
                Status = document.Status,
                CreatedAt = document.CreatedAt,
                UpdatedAt = document.UpdatedAt,
                UpdatedBy = document.UpdatedBy,
                SubjectCode = document.Subject?.SubjectCode,
                SubjectName = document.Subject?.Name,
                UploaderName = document.Uploader?.FullName
            };
        }

        public async Task<(int Processed, int Total)> GetProcessingProgressAsync(Guid documentId)
        {
            var chunks = await _documentChunkRepository.GetChunksByDocumentIdAsync(documentId);
            int total = chunks.Count;
            int processed = chunks.Count(c => c.Embedding != null);
            return (processed, total);
        }
    }
}
