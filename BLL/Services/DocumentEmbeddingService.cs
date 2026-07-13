using BLL.Interfaces;
using Core.DTOs.Common;
using Core.Entities;
using DAL.Interfaces;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace BLL.Services
{
    /// <summary>
    /// Service that generates vector embeddings for chunks of text and saves them to the DB.
    /// </summary>
    public class DocumentEmbeddingService : IEmbeddingService
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly IDocumentChunkRepository _documentChunkRepository;
        private readonly IGeminiEmbeddingProvider _geminiProvider;
        private readonly IDocumentProgressNotifier _progressNotifier;
        private readonly ILogger<DocumentEmbeddingService> _logger;

        public DocumentEmbeddingService(
            IDocumentRepository documentRepository,
            IDocumentChunkRepository documentChunkRepository,
            IGeminiEmbeddingProvider geminiProvider,
            IDocumentProgressNotifier progressNotifier,
            ILogger<DocumentEmbeddingService> logger)
        {
            _documentRepository = documentRepository;
            _documentChunkRepository = documentChunkRepository;
            _geminiProvider = geminiProvider;
            _progressNotifier = progressNotifier;
            _logger = logger;
        }

        public async Task<Result> ProcessEmbeddingsAsync(Guid documentId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting embedding process for DocumentId: {DocumentId}", documentId);

                var document = await _documentRepository.GetByIdAsync(documentId);
                if (document == null)
                {
                    return Result.Failure("Document not found.");
                }

                // If chunking failed, or it's already indexed, we should skip embedding.
                if (document.Status == DocumentStatus.Failed || 
                    document.Status == DocumentStatus.Success)
                {
                    _logger.LogWarning("Document {DocumentId} is at status {Status}. Skipping embedding.", documentId, document.Status);
                    return Result.Success();
                }

                // Status should be Processing now.
                // We don't need to change it, it's already Processing from ChunkingService.

                // Fetch chunks
                var chunks = await _documentChunkRepository.GetChunksByDocumentIdAsync(documentId);
                if (chunks == null || !chunks.Any())
                {
                    _logger.LogWarning("No chunks found for DocumentId: {DocumentId}. Marking as Success anyway.", documentId);
                    await _documentRepository.UpdateStatusAsync(documentId, DocumentStatus.Success);
                    return Result.Success();
                }

                var chunksToProcess = chunks.Where(c => c.Embedding == null).ToList();
                _logger.LogInformation("Found {TotalChunks} chunks, {PendingChunks} need embeddings.", chunks.Count, chunksToProcess.Count);

                int initialProcessed = chunks.Count - chunksToProcess.Count;
                await _progressNotifier.NotifyProgressAsync(documentId, "Processing", initialProcessed, chunks.Count);

                const int batchSize = 10;
                var batch = new List<DocumentChunk>();

                foreach (var chunk in chunksToProcess)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var vectorArray = await _geminiProvider.GetEmbeddingAsync(chunk.Content, cancellationToken);
                    chunk.Embedding = new Vector(vectorArray);
                    
                    batch.Add(chunk);

                    if (batch.Count >= batchSize)
                    {
                        await _documentChunkRepository.UpdateChunksAsync(batch);
                        batch.Clear();

                        int processedCount = chunks.Count(c => c.Embedding != null);
                        await _progressNotifier.NotifyProgressAsync(documentId, "Processing", processedCount, chunks.Count);
                    }
                }

                if (batch.Any())
                {
                    await _documentChunkRepository.UpdateChunksAsync(batch);

                    int processedCount = chunks.Count(c => c.Embedding != null);
                    await _progressNotifier.NotifyProgressAsync(documentId, "Processing", processedCount, chunks.Count);
                }

                // 4. Update status to Success
                await _documentRepository.UpdateStatusAsync(documentId, DocumentStatus.Success);
                await _progressNotifier.NotifyProgressAsync(documentId, "Success", chunks.Count, chunks.Count);
                _logger.LogInformation("Successfully completed embedding process for DocumentId: {DocumentId}", documentId);
                
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process embeddings for DocumentId: {DocumentId}", documentId);
                
                _documentRepository.ClearTracker();
                // If it fails, update status to Failed so the user can see it
                await _documentRepository.UpdateStatusAsync(documentId, DocumentStatus.Failed);
                await _progressNotifier.NotifyProgressAsync(documentId, "Failed", 0, 0);
                return Result.Failure($"Embedding error: {ex.Message}");
            }
        }
    }
}
