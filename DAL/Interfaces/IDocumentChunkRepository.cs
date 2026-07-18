using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.Entities;
using Pgvector;

namespace DAL.Interfaces
{
    /// <summary>
    /// Defines repository operations for document chunks.
    /// </summary>
    public interface IDocumentChunkRepository
    {
        /// <summary>
        /// Performs a bulk insert of document chunks into the database.
        /// </summary>
        Task BulkInsertChunksAsync(IEnumerable<DocumentChunk> chunks);

        /// <summary>
        /// Retrieves all chunks for a specific document.
        /// </summary>
        Task<IReadOnlyList<DocumentChunk>> GetChunksByDocumentIdAsync(Guid documentId);

        /// <summary>
        /// Deletes all chunks for a specific document (useful for retries).
        /// </summary>
        Task DeleteChunksByDocumentIdAsync(Guid documentId);

        /// <summary>
        /// Updates multiple chunks, typically to save their generated embeddings.
        /// </summary>
        Task UpdateChunksAsync(IEnumerable<DocumentChunk> chunks);

        /// <summary>
        /// Checks if a document already has chunks.
        /// </summary>
        Task<bool> HasChunksAsync(Guid documentId);

        /// <summary>
        /// Performs a cosine similarity search for chunks within a subject, optionally filtered by document IDs.
        /// Returns the top <paramref name="limit"/> most similar chunks as full <see cref="DocumentChunk"/>
        /// entities (with the parent <see cref="Core.Entities.Document"/> included for source display).
        /// </summary>
        Task<IReadOnlyList<DocumentChunk>> SearchSimilarChunksAsync(
            Guid subjectId,
            Vector queryEmbedding,
            int limit,
            List<Guid>? documentIds = null,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<DocumentChunk>> FindSimilarChunksFromOtherDocumentsAsync(
            Guid subjectId, Guid documentId, Vector queryEmbedding, int limit,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the total number of document chunks across all documents.
        /// </summary>
        Task<int> CountAllAsync();
    }
}
