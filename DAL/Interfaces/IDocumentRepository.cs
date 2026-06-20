using Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    /// <summary>
    /// Provides database and data access operations for managing documents.
    /// </summary>
    public interface IDocumentRepository
    {
        /// <summary>
        /// Retrieves all documents under a specific subject.
        /// </summary>
        Task<IReadOnlyList<Document>> GetBySubjectIdAsync(Guid subjectId);

        /// <summary>
        /// Retrieves all documents uploaded by a specific user (Lecturer).
        /// </summary>
        Task<IReadOnlyList<Document>> GetByUploadedByAsync(Guid userId);

        /// <summary>
        /// Retrieves a document by its unique identifier and verifies user permission.
        /// </summary>
        Task<Document?> GetByIdForUserAsync(Guid documentId, Guid userId);

        /// <summary>
        /// Retrieves a document by its unique identifier.
        /// </summary>
        Task<Document?> GetByIdAsync(Guid id);

        /// <summary>
        /// Adds a new document record to the database.
        /// </summary>
        Task<Document> AddAsync(Document document);

        /// <summary>
        /// Deletes a document record from the database.
        /// </summary>
        Task DeleteAsync(Document document);

        /// <summary>
        /// Updates the status of a specific document.
        /// </summary>
        /// <param name="id">The unique identifier of the document.</param>
        /// <param name="status">The new processing status.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task UpdateStatusAsync(Guid id, DocumentStatus status);

        /// <summary>
        /// Updates an existing document in the database.
        /// </summary>
        Task UpdateAsync(Document document);

        /// <summary>
        /// Clears the Entity Framework change tracker.
        /// </summary>
        void ClearTracker();

        /// <summary>
        /// Retrieves all documents across all subjects, including related Subject and Uploader data.
        /// </summary>
        Task<IReadOnlyList<Document>> GetAllWithDetailsAsync();

        Task<IReadOnlyList<Document>> QueryAsync(string? search, Core.Entities.DocumentStatus? status, Guid? subjectId, int pageIndex, int pageSize);

        Task<int> CountAsync(string? search, Core.Entities.DocumentStatus? status, Guid? subjectId);
    }
}
