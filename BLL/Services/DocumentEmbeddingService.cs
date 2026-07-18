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
        private readonly ISupabaseStorageProvider _storageProvider;
        private readonly IDocumentProgressNotifier _progressNotifier;
        private readonly ILogger<DocumentEmbeddingService> _logger;

        public DocumentEmbeddingService(
            IDocumentRepository documentRepository,
            IDocumentChunkRepository documentChunkRepository,
            IGeminiEmbeddingProvider geminiProvider,
            ISupabaseStorageProvider storageProvider,
            IDocumentProgressNotifier progressNotifier,
            ILogger<DocumentEmbeddingService> logger)
        {
            _documentRepository = documentRepository;
            _documentChunkRepository = documentChunkRepository;
            _geminiProvider = geminiProvider;
            _storageProvider = storageProvider;
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

                // Compare the newly indexed chunks with older documents in the same subject.
                // A very close match on every chunk means this is a duplicate; otherwise flag
                // the document for lecturer review when substantial knowledge overlaps.
                var nearest = new List<DocumentChunk>();
                foreach (var chunk in chunks.Where(c => c.Embedding != null))
                {
                    var matches = await _documentChunkRepository.FindSimilarChunksFromOtherDocumentsAsync(
                        document.SubjectId ?? Guid.Empty, documentId, chunk.Embedding!, 1, cancellationToken);
                    if (matches.Count > 0) nearest.Add(matches[0]);
                }

                var overlapCount = nearest.Count(m => m.Embedding != null);
                // Semantic similarity alone must never classify a document as a duplicate:
                // a short document may contain only one chunk, and that chunk can be merely
                // related to an older one. Duplicate requires every chunk to have identical
                // normalized text (the semantic search is only used to find candidates).
                var isDuplicate = chunks.Count > 0
                    && overlapCount == chunks.Count
                    && chunks.Zip(nearest, (newChunk, oldChunk) =>
                        NormalizeForDuplicateCheck(newChunk.Content) == NormalizeForDuplicateCheck(oldChunk.Content))
                        .All(x => x);
                // Any semantic overlap is enough to require lecturer review. There is no
                // minimum percentage: one duplicated knowledge chunk must be visible.
                var isConflict = !isDuplicate && overlapCount > 0;
                var finalStatus = isDuplicate || isConflict ? DocumentStatus.Conflict : DocumentStatus.Success;
                if (isDuplicate)
                {
                    // The upload is discarded only after semantic verification has completed.
                    // The older indexed document remains the canonical copy.
                    await _storageProvider.DeleteAsync(document.FileUrl, cancellationToken);
                    await _documentRepository.DeleteAsync(document);
                    await _progressNotifier.NotifyProgressAsync(documentId, "Duplicate", chunks.Count, chunks.Count);
                    _logger.LogInformation("Discarded duplicate document {DocumentId} after comparing embeddings.", documentId);
                    return Result.Failure("Tài liệu này đã tồn tại trong môn học, nên không cần tải lên lại.");
                }
                await _documentRepository.UpdateStatusAsync(documentId, finalStatus);
                await _progressNotifier.NotifyProgressAsync(documentId, finalStatus == DocumentStatus.Conflict ? "Conflict" : "Success", chunks.Count, chunks.Count);
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

        private static string NormalizeForDuplicateCheck(string? content)
        {
            if (string.IsNullOrWhiteSpace(content)) return string.Empty;
            return System.Text.RegularExpressions.Regex.Replace(content.Trim(), @"\s+", " ")
                .ToLowerInvariant();
        }
    }
}
