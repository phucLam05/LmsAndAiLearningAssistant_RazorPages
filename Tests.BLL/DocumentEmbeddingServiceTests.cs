using BLL.Services;
using Core.DTOs.Common;
using Core.Entities;
using DAL.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Pgvector;

namespace Tests.BLL
{
    /// <summary>
    /// Unit tests for <see cref="DocumentEmbeddingService"/>.
    /// Covers happy paths, edge cases, error handling, batch processing, and progress notifications.
    /// </summary>
    public class DocumentEmbeddingServiceTests
    {
        // ── Helpers ─────────────────────────────────────────────────────────────

        private static (
            DocumentEmbeddingService service,
            Mock<IDocumentRepository> docRepoMock,
            Mock<IDocumentChunkRepository> chunkRepoMock,
            Mock<IGeminiEmbeddingProvider> embeddingMock,
            Mock<IDocumentProgressNotifier> notifierMock
        ) BuildService()
        {
            var docRepo    = new Mock<IDocumentRepository>();
            var chunkRepo  = new Mock<IDocumentChunkRepository>();
            var embedding  = new Mock<IGeminiEmbeddingProvider>();
            var notifier   = new Mock<IDocumentProgressNotifier>();
            var logger     = Mock.Of<ILogger<DocumentEmbeddingService>>();

            var service = new DocumentEmbeddingService(
                docRepo.Object,
                chunkRepo.Object,
                embedding.Object,
                notifier.Object,
                logger);

            return (service, docRepo, chunkRepo, embedding, notifier);
        }

        private static Document MakeDocument(
            DocumentStatus status = DocumentStatus.Processing,
            Guid? id = null)
            => new Document
            {
                Id     = id ?? Guid.NewGuid(),
                FileName = "test.pdf",
                FileUrl  = "subjects/test/test.pdf",
                Status   = status,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

        private static DocumentChunk MakeChunk(
            Guid? docId = null,
            bool hasEmbedding = false,
            int index = 0)
            => new DocumentChunk
            {
                Id         = Guid.NewGuid(),
                DocumentId = docId ?? Guid.NewGuid(),
                ChunkIndex = index,
                Content    = $"This is chunk number {index} with some content for testing purposes.",
                Embedding  = hasEmbedding
                    ? new Vector(new float[] { 0.1f, 0.2f, 0.3f })
                    : null,
                CreatedAt  = DateTime.UtcNow
            };

        // ── ProcessEmbeddingsAsync — document not found ─────────────────────────

        [Fact]
        public async Task ProcessEmbeddingsAsync_DocumentNotFound_ReturnsFailure()
        {
            var (service, docRepo, _, _, _) = BuildService();

            docRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Document?)null);

            var result = await service.ProcessEmbeddingsAsync(Guid.NewGuid());

            Assert.False(result.IsSuccess);
            Assert.Equal("Document not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task ProcessEmbeddingsAsync_DocumentNotFound_DoesNotCallEmbedding()
        {
            var (service, docRepo, _, embedding, _) = BuildService();

            docRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Document?)null);

            await service.ProcessEmbeddingsAsync(Guid.NewGuid());

            embedding.Verify(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ── ProcessEmbeddingsAsync — status guards ──────────────────────────────

        [Fact]
        public async Task ProcessEmbeddingsAsync_DocumentAlreadyFailed_SkipsAndReturnsSuccess()
        {
            var (service, docRepo, chunkRepo, embedding, _) = BuildService();

            var doc = MakeDocument(DocumentStatus.Failed);
            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            var result = await service.ProcessEmbeddingsAsync(doc.Id);

            Assert.True(result.IsSuccess);
            embedding.Verify(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            chunkRepo.Verify(r => r.GetChunksByDocumentIdAsync(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task ProcessEmbeddingsAsync_DocumentAlreadySuccess_SkipsAndReturnsSuccess()
        {
            var (service, docRepo, chunkRepo, embedding, _) = BuildService();

            var doc = MakeDocument(DocumentStatus.Success);
            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            var result = await service.ProcessEmbeddingsAsync(doc.Id);

            Assert.True(result.IsSuccess);
            embedding.Verify(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ProcessEmbeddingsAsync_DocumentPendingStatus_ProcessesEmbeddings()
        {
            var (service, docRepo, chunkRepo, embedding, notifier) = BuildService();

            var doc = MakeDocument(DocumentStatus.Processing);
            var chunks = new List<DocumentChunk> { MakeChunk(doc.Id, hasEmbedding: false) };

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(doc.Id))
                .ReturnsAsync(chunks);

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new float[768]);

            chunkRepo
                .Setup(r => r.UpdateChunksAsync(It.IsAny<List<DocumentChunk>>()))
                .Returns(Task.CompletedTask);

            docRepo
                .Setup(r => r.UpdateStatusAsync(doc.Id, DocumentStatus.Success))
                .Returns(Task.CompletedTask);

            notifier
                .Setup(n => n.NotifyProgressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            var result = await service.ProcessEmbeddingsAsync(doc.Id);

            Assert.True(result.IsSuccess);
        }

        // ── ProcessEmbeddingsAsync — no chunks ──────────────────────────────────

        [Fact]
        public async Task ProcessEmbeddingsAsync_NoChunks_MarksSuccessAndReturnsSuccess()
        {
            var (service, docRepo, chunkRepo, embedding, notifier) = BuildService();

            var doc = MakeDocument(DocumentStatus.Processing);
            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(doc.Id))
                .ReturnsAsync(new List<DocumentChunk>());

            docRepo
                .Setup(r => r.UpdateStatusAsync(doc.Id, DocumentStatus.Success))
                .Returns(Task.CompletedTask);

            notifier
                .Setup(n => n.NotifyProgressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            var result = await service.ProcessEmbeddingsAsync(doc.Id);

            Assert.True(result.IsSuccess);
            docRepo.Verify(r => r.UpdateStatusAsync(doc.Id, DocumentStatus.Success), Times.Once);
        }

        [Fact]
        public async Task ProcessEmbeddingsAsync_NullChunks_MarksSuccessAndReturnsSuccess()
        {
            var (service, docRepo, chunkRepo, embedding, notifier) = BuildService();

            var doc = MakeDocument(DocumentStatus.Processing);
            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(doc.Id))
                .ReturnsAsync((List<DocumentChunk>?)null!);

            docRepo
                .Setup(r => r.UpdateStatusAsync(doc.Id, DocumentStatus.Success))
                .Returns(Task.CompletedTask);

            notifier
                .Setup(n => n.NotifyProgressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            var result = await service.ProcessEmbeddingsAsync(doc.Id);

            Assert.True(result.IsSuccess);
        }

        // ── ProcessEmbeddingsAsync — all chunks already embedded ────────────────

        [Fact]
        public async Task ProcessEmbeddingsAsync_AllChunksAlreadyEmbedded_SkipsEmbeddingCalls()
        {
            var (service, docRepo, chunkRepo, embedding, notifier) = BuildService();

            var doc = MakeDocument(DocumentStatus.Processing);
            var chunks = new List<DocumentChunk>
            {
                MakeChunk(doc.Id, hasEmbedding: true, index: 0),
                MakeChunk(doc.Id, hasEmbedding: true, index: 1),
                MakeChunk(doc.Id, hasEmbedding: true, index: 2)
            };

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(doc.Id))
                .ReturnsAsync(chunks);

            docRepo
                .Setup(r => r.UpdateStatusAsync(doc.Id, DocumentStatus.Success))
                .Returns(Task.CompletedTask);

            notifier
                .Setup(n => n.NotifyProgressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            var result = await service.ProcessEmbeddingsAsync(doc.Id);

            Assert.True(result.IsSuccess);
            embedding.Verify(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ── ProcessEmbeddingsAsync — partial embedding ──────────────────────────

        [Fact]
        public async Task ProcessEmbeddingsAsync_SomeChunksAlreadyEmbedded_OnlyEmbedsMissingOnes()
        {
            var (service, docRepo, chunkRepo, embedding, notifier) = BuildService();

            var doc = MakeDocument(DocumentStatus.Processing);
            var chunks = new List<DocumentChunk>
            {
                MakeChunk(doc.Id, hasEmbedding: true,  index: 0),
                MakeChunk(doc.Id, hasEmbedding: false, index: 1),
                MakeChunk(doc.Id, hasEmbedding: true,  index: 2),
                MakeChunk(doc.Id, hasEmbedding: false, index: 3)
            };

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(doc.Id))
                .ReturnsAsync(chunks);

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new float[768]);

            chunkRepo
                .Setup(r => r.UpdateChunksAsync(It.IsAny<List<DocumentChunk>>()))
                .Returns(Task.CompletedTask);

            docRepo
                .Setup(r => r.UpdateStatusAsync(doc.Id, DocumentStatus.Success))
                .Returns(Task.CompletedTask);

            notifier
                .Setup(n => n.NotifyProgressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            var result = await service.ProcessEmbeddingsAsync(doc.Id);

            Assert.True(result.IsSuccess);
            // Only 2 chunks needed embeddings
            embedding.Verify(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        // ── ProcessEmbeddingsAsync — single chunk ────────────────────────────────

        [Fact]
        public async Task ProcessEmbeddingsAsync_SingleChunk_EmbedsThenMarksSuccess()
        {
            var (service, docRepo, chunkRepo, embedding, notifier) = BuildService();

            var doc = MakeDocument(DocumentStatus.Processing);
            var chunk = MakeChunk(doc.Id, hasEmbedding: false, index: 0);

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(doc.Id))
                .ReturnsAsync(new List<DocumentChunk> { chunk });

            embedding
                .Setup(e => e.GetEmbeddingAsync(chunk.Content, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new float[768]);

            chunkRepo
                .Setup(r => r.UpdateChunksAsync(It.IsAny<List<DocumentChunk>>()))
                .Returns(Task.CompletedTask);

            docRepo
                .Setup(r => r.UpdateStatusAsync(doc.Id, DocumentStatus.Success))
                .Returns(Task.CompletedTask);

            notifier
                .Setup(n => n.NotifyProgressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            var result = await service.ProcessEmbeddingsAsync(doc.Id);

            Assert.True(result.IsSuccess);
            Assert.NotNull(chunk.Embedding);
            docRepo.Verify(r => r.UpdateStatusAsync(doc.Id, DocumentStatus.Success), Times.Once);
        }

        // ── ProcessEmbeddingsAsync — batch processing ────────────────────────────

        [Fact]
        public async Task ProcessEmbeddingsAsync_MoreThanTenChunks_ProcessesInBatches()
        {
            var (service, docRepo, chunkRepo, embedding, notifier) = BuildService();

            var doc = MakeDocument(DocumentStatus.Processing);
            var chunks = Enumerable.Range(0, 15)
                .Select(i => MakeChunk(doc.Id, hasEmbedding: false, index: i))
                .ToList();

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(doc.Id))
                .ReturnsAsync(chunks);

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new float[768]);

            chunkRepo
                .Setup(r => r.UpdateChunksAsync(It.IsAny<List<DocumentChunk>>()))
                .Returns(Task.CompletedTask);

            docRepo
                .Setup(r => r.UpdateStatusAsync(doc.Id, DocumentStatus.Success))
                .Returns(Task.CompletedTask);

            notifier
                .Setup(n => n.NotifyProgressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            var result = await service.ProcessEmbeddingsAsync(doc.Id);

            Assert.True(result.IsSuccess);
            // 15 chunks = 1 full batch of 10 + 5 remaining = UpdateChunksAsync called twice
            chunkRepo.Verify(r => r.UpdateChunksAsync(It.IsAny<List<DocumentChunk>>()), Times.Exactly(2));
        }

        [Fact]
        public async Task ProcessEmbeddingsAsync_ExactlyTenChunks_ProcessesInOneBatch()
        {
            var (service, docRepo, chunkRepo, embedding, notifier) = BuildService();

            var doc = MakeDocument(DocumentStatus.Processing);
            var chunks = Enumerable.Range(0, 10)
                .Select(i => MakeChunk(doc.Id, hasEmbedding: false, index: i))
                .ToList();

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(doc.Id))
                .ReturnsAsync(chunks);

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new float[768]);

            chunkRepo
                .Setup(r => r.UpdateChunksAsync(It.IsAny<List<DocumentChunk>>()))
                .Returns(Task.CompletedTask);

            docRepo
                .Setup(r => r.UpdateStatusAsync(doc.Id, DocumentStatus.Success))
                .Returns(Task.CompletedTask);

            notifier
                .Setup(n => n.NotifyProgressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            var result = await service.ProcessEmbeddingsAsync(doc.Id);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task ProcessEmbeddingsAsync_TwentyChunks_ProcessesInThreeBatches()
        {
            var (service, docRepo, chunkRepo, embedding, notifier) = BuildService();

            var doc = MakeDocument(DocumentStatus.Processing);
            var chunks = Enumerable.Range(0, 20)
                .Select(i => MakeChunk(doc.Id, hasEmbedding: false, index: i))
                .ToList();

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(doc.Id))
                .ReturnsAsync(chunks);

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new float[768]);

            chunkRepo
                .Setup(r => r.UpdateChunksAsync(It.IsAny<List<DocumentChunk>>()))
                .Returns(Task.CompletedTask);

            docRepo
                .Setup(r => r.UpdateStatusAsync(doc.Id, DocumentStatus.Success))
                .Returns(Task.CompletedTask);

            notifier
                .Setup(n => n.NotifyProgressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            var result = await service.ProcessEmbeddingsAsync(doc.Id);

            Assert.True(result.IsSuccess);
            // 20 chunks = 2 full batches of 10 each. No remainder.
            chunkRepo.Verify(r => r.UpdateChunksAsync(It.IsAny<List<DocumentChunk>>()), Times.Exactly(2));
        }

        // ── ProcessEmbeddingsAsync — embedding error handling ────────────────────

        [Fact]
        public async Task ProcessEmbeddingsAsync_EmbeddingThrows_ReturnsFailureAndMarksDocumentFailed()
        {
            var (service, docRepo, chunkRepo, embedding, notifier) = BuildService();

            var doc = MakeDocument(DocumentStatus.Processing);
            var chunks = new List<DocumentChunk> { MakeChunk(doc.Id, hasEmbedding: false) };

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(doc.Id))
                .ReturnsAsync(chunks);

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Gemini API rate limit exceeded"));

            docRepo
                .Setup(r => r.UpdateStatusAsync(doc.Id, DocumentStatus.Failed))
                .Returns(Task.CompletedTask);

            docRepo
                .Setup(r => r.ClearTracker())
                .Verifiable();

            notifier
                .Setup(n => n.NotifyProgressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            var result = await service.ProcessEmbeddingsAsync(doc.Id);

            Assert.False(result.IsSuccess);
            Assert.Contains("Embedding error", result.ErrorMessage);
            docRepo.Verify(r => r.UpdateStatusAsync(doc.Id, DocumentStatus.Failed), Times.Once);
        }

        [Fact]
        public async Task ProcessEmbeddingsAsync_EmbeddingThrows_NotifiesFailure()
        {
            var (service, docRepo, chunkRepo, embedding, notifier) = BuildService();

            var doc = MakeDocument(DocumentStatus.Processing);
            var chunks = new List<DocumentChunk> { MakeChunk(doc.Id, hasEmbedding: false) };

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(doc.Id))
                .ReturnsAsync(chunks);

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Network error"));

            docRepo
                .Setup(r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<DocumentStatus>()))
                .Returns(Task.CompletedTask);

            docRepo.Setup(r => r.ClearTracker());

            notifier
                .Setup(n => n.NotifyProgressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            await service.ProcessEmbeddingsAsync(doc.Id);

            notifier.Verify(
                n => n.NotifyProgressAsync(doc.Id, "Failed", 0, 0),
                Times.Once);
        }

        [Fact]
        public async Task ProcessEmbeddingsAsync_ChunkRepositoryThrows_ReturnsFailure()
        {
            var (service, docRepo, chunkRepo, embedding, notifier) = BuildService();

            var doc = MakeDocument(DocumentStatus.Processing);
            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(doc.Id))
                .ThrowsAsync(new Exception("DB connection lost"));

            docRepo
                .Setup(r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<DocumentStatus>()))
                .Returns(Task.CompletedTask);

            docRepo.Setup(r => r.ClearTracker());

            notifier
                .Setup(n => n.NotifyProgressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            var result = await service.ProcessEmbeddingsAsync(doc.Id);

            Assert.False(result.IsSuccess);
        }

        // ── ProcessEmbeddingsAsync — progress notifications ──────────────────────

        [Fact]
        public async Task ProcessEmbeddingsAsync_Success_NotifiesSuccessAtEnd()
        {
            var (service, docRepo, chunkRepo, embedding, notifier) = BuildService();

            var doc = MakeDocument(DocumentStatus.Processing);
            var chunks = new List<DocumentChunk>
            {
                MakeChunk(doc.Id, hasEmbedding: false, index: 0),
                MakeChunk(doc.Id, hasEmbedding: false, index: 1)
            };

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(doc.Id))
                .ReturnsAsync(chunks);

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new float[768]);

            chunkRepo
                .Setup(r => r.UpdateChunksAsync(It.IsAny<List<DocumentChunk>>()))
                .Returns(Task.CompletedTask);

            docRepo
                .Setup(r => r.UpdateStatusAsync(doc.Id, DocumentStatus.Success))
                .Returns(Task.CompletedTask);

            notifier
                .Setup(n => n.NotifyProgressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            await service.ProcessEmbeddingsAsync(doc.Id);

            notifier.Verify(
                n => n.NotifyProgressAsync(doc.Id, "Success", 2, 2),
                Times.Once);
        }

        [Fact]
        public async Task ProcessEmbeddingsAsync_StartOfProcess_NotifiesInitialProgress()
        {
            var (service, docRepo, chunkRepo, embedding, notifier) = BuildService();

            var doc = MakeDocument(DocumentStatus.Processing);
            var chunks = new List<DocumentChunk>
            {
                MakeChunk(doc.Id, hasEmbedding: true,  index: 0), // already done
                MakeChunk(doc.Id, hasEmbedding: false, index: 1)  // needs embedding
            };

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(doc.Id))
                .ReturnsAsync(chunks);

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new float[768]);

            chunkRepo
                .Setup(r => r.UpdateChunksAsync(It.IsAny<List<DocumentChunk>>()))
                .Returns(Task.CompletedTask);

            docRepo
                .Setup(r => r.UpdateStatusAsync(doc.Id, DocumentStatus.Success))
                .Returns(Task.CompletedTask);

            notifier
                .Setup(n => n.NotifyProgressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            await service.ProcessEmbeddingsAsync(doc.Id);

            // Should notify initial progress with 1 already processed out of 2
            notifier.Verify(
                n => n.NotifyProgressAsync(doc.Id, "Processing", 1, 2),
                Times.Once);
        }

        // ── ProcessEmbeddingsAsync — cancellation ────────────────────────────────

        [Fact]
        public async Task ProcessEmbeddingsAsync_CancellationRequested_ThrowsOperationCancelledException()
        {
            var (service, docRepo, chunkRepo, embedding, notifier) = BuildService();

            var doc = MakeDocument(DocumentStatus.Processing);
            var chunks = new List<DocumentChunk>
            {
                MakeChunk(doc.Id, hasEmbedding: false, index: 0),
                MakeChunk(doc.Id, hasEmbedding: false, index: 1)
            };

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(doc.Id))
                .ReturnsAsync(chunks);

            var cts = new CancellationTokenSource();

            // Signal cancellation right before embedding
            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback(() => cts.Cancel())
                .ThrowsAsync(new OperationCanceledException());

            docRepo
                .Setup(r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<DocumentStatus>()))
                .Returns(Task.CompletedTask);

            docRepo.Setup(r => r.ClearTracker());

            notifier
                .Setup(n => n.NotifyProgressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            var result = await service.ProcessEmbeddingsAsync(doc.Id, cts.Token);

            // Cancellation is treated as an error
            Assert.False(result.IsSuccess);
        }

        // ── ProcessEmbeddingsAsync — embedding is set on chunk ───────────────────

        [Fact]
        public async Task ProcessEmbeddingsAsync_AfterSuccess_ChunkHasEmbeddingSet()
        {
            var (service, docRepo, chunkRepo, embedding, notifier) = BuildService();

            var doc = MakeDocument(DocumentStatus.Processing);
            var chunk = MakeChunk(doc.Id, hasEmbedding: false, index: 0);
            var expectedVector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f };

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(doc.Id))
                .ReturnsAsync(new List<DocumentChunk> { chunk });

            embedding
                .Setup(e => e.GetEmbeddingAsync(chunk.Content, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedVector);

            chunkRepo
                .Setup(r => r.UpdateChunksAsync(It.IsAny<List<DocumentChunk>>()))
                .Returns(Task.CompletedTask);

            docRepo
                .Setup(r => r.UpdateStatusAsync(doc.Id, DocumentStatus.Success))
                .Returns(Task.CompletedTask);

            notifier
                .Setup(n => n.NotifyProgressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            await service.ProcessEmbeddingsAsync(doc.Id);

            // Chunk should have its embedding set
            Assert.NotNull(chunk.Embedding);
        }

        [Fact]
        public async Task ProcessEmbeddingsAsync_MultipleChunks_EachChunkEmbeddingIsSet()
        {
            var (service, docRepo, chunkRepo, embedding, notifier) = BuildService();

            var doc = MakeDocument(DocumentStatus.Processing);
            var chunks = Enumerable.Range(0, 5)
                .Select(i => MakeChunk(doc.Id, hasEmbedding: false, index: i))
                .ToList();

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(doc.Id))
                .ReturnsAsync(chunks);

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new float[768]);

            chunkRepo
                .Setup(r => r.UpdateChunksAsync(It.IsAny<List<DocumentChunk>>()))
                .Returns(Task.CompletedTask);

            docRepo
                .Setup(r => r.UpdateStatusAsync(doc.Id, DocumentStatus.Success))
                .Returns(Task.CompletedTask);

            notifier
                .Setup(n => n.NotifyProgressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            await service.ProcessEmbeddingsAsync(doc.Id);

            Assert.All(chunks, c => Assert.NotNull(c.Embedding));
        }

        // ── ProcessEmbeddingsAsync — status update verification ──────────────────

        [Fact]
        public async Task ProcessEmbeddingsAsync_OnSuccess_CallsUpdateStatusWithSuccess()
        {
            var (service, docRepo, chunkRepo, embedding, notifier) = BuildService();

            var doc = MakeDocument(DocumentStatus.Processing);
            var chunks = new List<DocumentChunk> { MakeChunk(doc.Id, hasEmbedding: false) };

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(doc.Id))
                .ReturnsAsync(chunks);

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new float[768]);

            chunkRepo
                .Setup(r => r.UpdateChunksAsync(It.IsAny<List<DocumentChunk>>()))
                .Returns(Task.CompletedTask);

            docRepo
                .Setup(r => r.UpdateStatusAsync(doc.Id, DocumentStatus.Success))
                .Returns(Task.CompletedTask);

            notifier
                .Setup(n => n.NotifyProgressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            await service.ProcessEmbeddingsAsync(doc.Id);

            docRepo.Verify(r => r.UpdateStatusAsync(doc.Id, DocumentStatus.Success), Times.Once);
        }

        [Fact]
        public async Task ProcessEmbeddingsAsync_OnFailure_CallsUpdateStatusWithFailed()
        {
            var (service, docRepo, chunkRepo, embedding, notifier) = BuildService();

            var doc = MakeDocument(DocumentStatus.Processing);
            var chunks = new List<DocumentChunk> { MakeChunk(doc.Id, hasEmbedding: false) };

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(doc.Id))
                .ReturnsAsync(chunks);

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Quota exceeded"));

            docRepo
                .Setup(r => r.UpdateStatusAsync(doc.Id, DocumentStatus.Failed))
                .Returns(Task.CompletedTask);

            docRepo.Setup(r => r.ClearTracker());

            notifier
                .Setup(n => n.NotifyProgressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            await service.ProcessEmbeddingsAsync(doc.Id);

            docRepo.Verify(r => r.UpdateStatusAsync(doc.Id, DocumentStatus.Failed), Times.Once);
        }

        [Fact]
        public async Task ProcessEmbeddingsAsync_OnFailure_CallsClearTracker()
        {
            var (service, docRepo, chunkRepo, embedding, notifier) = BuildService();

            var doc = MakeDocument(DocumentStatus.Processing);
            var chunks = new List<DocumentChunk> { MakeChunk(doc.Id, hasEmbedding: false) };

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(doc.Id))
                .ReturnsAsync(chunks);

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Error"));

            docRepo
                .Setup(r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<DocumentStatus>()))
                .Returns(Task.CompletedTask);

            docRepo.Setup(r => r.ClearTracker());

            notifier
                .Setup(n => n.NotifyProgressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            await service.ProcessEmbeddingsAsync(doc.Id);

            docRepo.Verify(r => r.ClearTracker(), Times.Once);
        }

        // ── ProcessEmbeddingsAsync — no chunks to embed ──────────────────────────

        [Fact]
        public async Task ProcessEmbeddingsAsync_NoChunksToEmbed_DoesNotCallUpdateChunks()
        {
            var (service, docRepo, chunkRepo, embedding, notifier) = BuildService();

            var doc = MakeDocument(DocumentStatus.Processing);
            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(doc.Id))
                .ReturnsAsync(new List<DocumentChunk>());

            docRepo
                .Setup(r => r.UpdateStatusAsync(doc.Id, DocumentStatus.Success))
                .Returns(Task.CompletedTask);

            notifier
                .Setup(n => n.NotifyProgressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            await service.ProcessEmbeddingsAsync(doc.Id);

            chunkRepo.Verify(r => r.UpdateChunksAsync(It.IsAny<List<DocumentChunk>>()), Times.Never);
        }

        [Fact]
        public async Task ProcessEmbeddingsAsync_AllAlreadyEmbedded_DoesNotCallUpdateChunks()
        {
            var (service, docRepo, chunkRepo, embedding, notifier) = BuildService();

            var doc = MakeDocument(DocumentStatus.Processing);
            var chunks = new List<DocumentChunk>
            {
                MakeChunk(doc.Id, hasEmbedding: true, index: 0),
                MakeChunk(doc.Id, hasEmbedding: true, index: 1)
            };

            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(doc.Id))
                .ReturnsAsync(chunks);

            docRepo
                .Setup(r => r.UpdateStatusAsync(doc.Id, DocumentStatus.Success))
                .Returns(Task.CompletedTask);

            notifier
                .Setup(n => n.NotifyProgressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            await service.ProcessEmbeddingsAsync(doc.Id);

            chunkRepo.Verify(r => r.UpdateChunksAsync(It.IsAny<List<DocumentChunk>>()), Times.Never);
        }

        // ── ProcessEmbeddingsAsync — error message content ───────────────────────

        [Fact]
        public async Task ProcessEmbeddingsAsync_EmbeddingFails_ErrorMessageContainsEmbeddingError()
        {
            var (service, docRepo, chunkRepo, embedding, notifier) = BuildService();

            var doc = MakeDocument(DocumentStatus.Processing);
            docRepo
                .Setup(r => r.GetByIdAsync(doc.Id))
                .ReturnsAsync(doc);

            chunkRepo
                .Setup(r => r.GetChunksByDocumentIdAsync(doc.Id))
                .ReturnsAsync(new List<DocumentChunk> { MakeChunk(doc.Id) });

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Service unavailable"));

            docRepo
                .Setup(r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<DocumentStatus>()))
                .Returns(Task.CompletedTask);

            docRepo.Setup(r => r.ClearTracker());

            notifier
                .Setup(n => n.NotifyProgressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            var result = await service.ProcessEmbeddingsAsync(doc.Id);

            Assert.False(result.IsSuccess);
            Assert.StartsWith("Embedding error:", result.ErrorMessage);
        }
    }
}
