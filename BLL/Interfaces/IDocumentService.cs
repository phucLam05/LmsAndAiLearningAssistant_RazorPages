using Core.DTOs.Common;
using Core.DTOs.Documents;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    /// <summary>
    /// Defines document upload, listing, and deletion use cases.
    /// </summary>
    public interface IDocumentService
    {
        Task<IReadOnlyList<DocumentDto>> GetDocumentsBySubjectIdAsync(Guid subjectId);
        
        Task<DocumentDto?> GetDocumentByIdAsync(Guid documentId);

        Task<string?> GetSignedDocumentUrlAsync(Guid documentId);
        
        Task<(Stream Stream, string ContentType, string FileName)?> DownloadDocumentAsync(Guid documentId);

        Task<IReadOnlyList<DocumentChunkDto>> GetDocumentChunksAsync(Guid documentId);

        Task<Result<DocumentDto>> UploadAsync(DocumentUploadDto uploadDto);

        Task<Result> DeleteAsync(Guid documentId, Guid userId);

        Task<Result> RetryProcessingAsync(Guid documentId, Guid userId);

        Task<IReadOnlyList<DocumentDto>> GetAllDocumentsAsync();

        Task<PagedResult<DocumentDto>> GetPagedDocumentsAsync(string? search, string? status, Guid? subjectId, int pageIndex, int pageSize);

        Task<(int Processed, int Total)> GetProcessingProgressAsync(Guid documentId);
    }
}
