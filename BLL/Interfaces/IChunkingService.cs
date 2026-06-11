using System;
using System.Threading;
using System.Threading.Tasks;
using Core.DTOs.Common;

namespace BLL.Interfaces
{
    /// <summary>
    /// Service interface for processing document text into chunks.
    /// </summary>
    public interface IChunkingService
    {
        /// <summary>
        /// Processes a file associated with the given document ID, chunking it into smaller text pieces.
        /// </summary>
        /// <param name="documentId">The unique identifier of the document.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task<Result> ProcessFileChunkingAsync(Guid documentId, CancellationToken cancellationToken);
    }
}
