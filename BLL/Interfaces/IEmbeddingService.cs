using System;
using System.Threading;
using System.Threading.Tasks;
using Core.DTOs.Common;

namespace BLL.Interfaces
{
    /// <summary>
    /// Coordinates the embedding process for a document's chunks.
    /// </summary>
    public interface IEmbeddingService
    {
        /// <summary>
        /// Retrieves chunks for the document, generates embeddings via the provider,
        /// and updates the database.
        /// </summary>
        /// <param name="documentId">The document identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<Result> ProcessEmbeddingsAsync(Guid documentId, CancellationToken cancellationToken = default);
    }
}
