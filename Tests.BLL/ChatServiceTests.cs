using BLL.Services;
using Core.DTOs.Chat;
using Core.Entities;
using DAL.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Pgvector;

namespace Tests.BLL
{
    /// <summary>
    /// Unit tests for <see cref="ChatService"/>.
    /// Covers session management, RAG query handling, session CRUD, rename, delete,
    /// and message retrieval logic.
    /// </summary>
    public class ChatServiceTests
    {
        // ── Helpers ─────────────────────────────────────────────────────────────

        private static (
            ChatService service,
            Mock<IDocumentChunkRepository> chunkRepoMock,
            Mock<IGeminiEmbeddingProvider> embeddingMock,
            Mock<IChatSessionRepository> sessionRepoMock,
            Mock<IChatMessageRepository> msgRepoMock,
            Mock<IGeminiChatProvider> chatProviderMock
        ) BuildService()
        {
            var chunkRepo   = new Mock<IDocumentChunkRepository>();
            var embedding   = new Mock<IGeminiEmbeddingProvider>();
            var sessionRepo = new Mock<IChatSessionRepository>();
            var msgRepo     = new Mock<IChatMessageRepository>();
            var chatProvider = new Mock<IGeminiChatProvider>();
            var logger      = Mock.Of<ILogger<ChatService>>();

            var service = new ChatService(
                chunkRepo.Object,
                embedding.Object,
                sessionRepo.Object,
                msgRepo.Object,
                chatProvider.Object,
                logger);

            return (service, chunkRepo, embedding, sessionRepo, msgRepo, chatProvider);
        }

        private static ChatSession MakeSession(Guid? userId = null, Guid? sessionId = null, Guid? subjectId = null)
            => new ChatSession
            {
                Id = sessionId ?? Guid.NewGuid(),
                UserId = userId ?? Guid.NewGuid(),
                SubjectId = subjectId ?? Guid.NewGuid(),
                Title = "Test Session",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Messages = new List<ChatMessage>()
            };

        private static ChatMessage MakeMessage(Guid sessionId, string role = "user", string content = "Hello")
            => new ChatMessage
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                Role = role,
                Content = content,
                CreatedAt = DateTime.UtcNow
            };

        private static DocumentChunk MakeChunk(Guid? documentId = null, string content = "Chunk content", int chunkIndex = 0)
            => new DocumentChunk
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId ?? Guid.NewGuid(),
                ChunkIndex = chunkIndex,
                Content = content,
                Document = new Document { FileName = "lecture.pdf" }
            };

        // ── ChatWithSubjectAsync — empty query ───────────────────────────────────

        [Fact]
        public async Task ChatWithSubjectAsync_EmptyQuery_ReturnsPromptMessage()
        {
            var (service, _, _, _, _, _) = BuildService();

            var result = await service.ChatWithSubjectAsync(
                userId: Guid.NewGuid(),
                subjectId: Guid.NewGuid(),
                query: "");

            Assert.NotNull(result);
            Assert.Contains("nhập câu hỏi", result.Response.Answer, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ChatWithSubjectAsync_WhitespaceQuery_ReturnsPromptMessage()
        {
            var (service, _, _, _, _, _) = BuildService();

            var result = await service.ChatWithSubjectAsync(
                userId: Guid.NewGuid(),
                subjectId: Guid.NewGuid(),
                query: "   ");

            Assert.NotNull(result);
            Assert.Contains("nhập câu hỏi", result.Response.Answer, StringComparison.OrdinalIgnoreCase);
        }

        // ── ChatWithSubjectAsync — session resolution ─────────────────────────────

        [Fact]
        public async Task ChatWithSubjectAsync_NoSessionId_CreatesNewSession()
        {
            var (service, chunkRepo, embedding, sessionRepo, msgRepo, chatProvider) = BuildService();

            var userId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var newSession = MakeSession(userId, subjectId: subjectId);

            sessionRepo
                .Setup(r => r.CreateAsync(It.IsAny<ChatSession>()))
                .ReturnsAsync((ChatSession s) => s);

            msgRepo
                .Setup(r => r.AddAsync(It.IsAny<ChatMessage>()))
                .ReturnsAsync((ChatMessage m) => m);

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), default))
                .ReturnsAsync(new float[768]);

            chunkRepo
                .Setup(r => r.SearchSimilarChunksAsync(subjectId, It.IsAny<Vector>(), 5, null, default))
                .ReturnsAsync(new List<DocumentChunk>());

            sessionRepo
                .Setup(r => r.UpdateAsync(It.IsAny<ChatSession>()))
                .Returns(Task.CompletedTask);

            var result = await service.ChatWithSubjectAsync(userId, subjectId, "What is SOLID?");

            sessionRepo.Verify(r => r.CreateAsync(It.IsAny<ChatSession>()), Times.Once);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task ChatWithSubjectAsync_ValidSessionId_ReusesExistingSession()
        {
            var (service, chunkRepo, embedding, sessionRepo, msgRepo, chatProvider) = BuildService();

            var userId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var existingSession = MakeSession(userId, subjectId: subjectId);

            sessionRepo
                .Setup(r => r.GetByIdAsync(existingSession.Id))
                .ReturnsAsync(existingSession);

            msgRepo
                .Setup(r => r.AddAsync(It.IsAny<ChatMessage>()))
                .ReturnsAsync((ChatMessage m) => m);

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), default))
                .ReturnsAsync(new float[768]);

            chunkRepo
                .Setup(r => r.SearchSimilarChunksAsync(subjectId, It.IsAny<Vector>(), 5, null, default))
                .ReturnsAsync(new List<DocumentChunk>());

            sessionRepo
                .Setup(r => r.UpdateAsync(It.IsAny<ChatSession>()))
                .Returns(Task.CompletedTask);

            await service.ChatWithSubjectAsync(userId, subjectId, "Question?", sessionId: existingSession.Id);

            // Should NOT create a new session since we have a valid one
            sessionRepo.Verify(r => r.CreateAsync(It.IsAny<ChatSession>()), Times.Never);
        }

        [Fact]
        public async Task ChatWithSubjectAsync_SessionBelongsToDifferentUser_CreatesNewSession()
        {
            var (service, chunkRepo, embedding, sessionRepo, msgRepo, chatProvider) = BuildService();

            var userId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();

            // Session belongs to a DIFFERENT user
            var existingSession = MakeSession(otherUserId, subjectId: subjectId);

            sessionRepo
                .Setup(r => r.GetByIdAsync(existingSession.Id))
                .ReturnsAsync(existingSession);

            sessionRepo
                .Setup(r => r.CreateAsync(It.IsAny<ChatSession>()))
                .ReturnsAsync((ChatSession s) => s);

            msgRepo
                .Setup(r => r.AddAsync(It.IsAny<ChatMessage>()))
                .ReturnsAsync((ChatMessage m) => m);

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), default))
                .ReturnsAsync(new float[768]);

            chunkRepo
                .Setup(r => r.SearchSimilarChunksAsync(subjectId, It.IsAny<Vector>(), 5, null, default))
                .ReturnsAsync(new List<DocumentChunk>());

            sessionRepo
                .Setup(r => r.UpdateAsync(It.IsAny<ChatSession>()))
                .Returns(Task.CompletedTask);

            await service.ChatWithSubjectAsync(userId, subjectId, "My question", sessionId: existingSession.Id);

            // Should create a new session since the existing one doesn't belong to this user
            sessionRepo.Verify(r => r.CreateAsync(It.IsAny<ChatSession>()), Times.Once);
        }

        [Fact]
        public async Task ChatWithSubjectAsync_InvalidSessionId_CreatesNewSession()
        {
            var (service, chunkRepo, embedding, sessionRepo, msgRepo, chatProvider) = BuildService();

            var userId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();

            sessionRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((ChatSession?)null);

            sessionRepo
                .Setup(r => r.CreateAsync(It.IsAny<ChatSession>()))
                .ReturnsAsync((ChatSession s) => s);

            msgRepo
                .Setup(r => r.AddAsync(It.IsAny<ChatMessage>()))
                .ReturnsAsync((ChatMessage m) => m);

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), default))
                .ReturnsAsync(new float[768]);

            chunkRepo
                .Setup(r => r.SearchSimilarChunksAsync(subjectId, It.IsAny<Vector>(), 5, null, default))
                .ReturnsAsync(new List<DocumentChunk>());

            sessionRepo
                .Setup(r => r.UpdateAsync(It.IsAny<ChatSession>()))
                .Returns(Task.CompletedTask);

            await service.ChatWithSubjectAsync(userId, subjectId, "Question?", sessionId: Guid.NewGuid());

            sessionRepo.Verify(r => r.CreateAsync(It.IsAny<ChatSession>()), Times.Once);
        }

        // ── ChatWithSubjectAsync — RAG behavior ──────────────────────────────────

        [Fact]
        public async Task ChatWithSubjectAsync_NoMatchingChunks_ReturnsNoDocumentMessage()
        {
            var (service, chunkRepo, embedding, sessionRepo, msgRepo, chatProvider) = BuildService();

            var userId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();

            sessionRepo
                .Setup(r => r.CreateAsync(It.IsAny<ChatSession>()))
                .ReturnsAsync((ChatSession s) => s);

            msgRepo
                .Setup(r => r.AddAsync(It.IsAny<ChatMessage>()))
                .ReturnsAsync((ChatMessage m) => m);

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), default))
                .ReturnsAsync(new float[768]);

            chunkRepo
                .Setup(r => r.SearchSimilarChunksAsync(subjectId, It.IsAny<Vector>(), 5, null, default))
                .ReturnsAsync(new List<DocumentChunk>());

            sessionRepo
                .Setup(r => r.UpdateAsync(It.IsAny<ChatSession>()))
                .Returns(Task.CompletedTask);

            var result = await service.ChatWithSubjectAsync(userId, subjectId, "What is polymorphism?");

            Assert.NotNull(result);
            Assert.Contains("Chưa có tài liệu", result.Response.Answer, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ChatWithSubjectAsync_NoChunksWithDocumentFilter_ReturnsFilterMessage()
        {
            var (service, chunkRepo, embedding, sessionRepo, msgRepo, chatProvider) = BuildService();

            var userId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var docIds = new List<Guid> { Guid.NewGuid() };

            sessionRepo
                .Setup(r => r.CreateAsync(It.IsAny<ChatSession>()))
                .ReturnsAsync((ChatSession s) => s);

            msgRepo
                .Setup(r => r.AddAsync(It.IsAny<ChatMessage>()))
                .ReturnsAsync((ChatMessage m) => m);

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), default))
                .ReturnsAsync(new float[768]);

            chunkRepo
                .Setup(r => r.SearchSimilarChunksAsync(subjectId, It.IsAny<Vector>(), 5, docIds, default))
                .ReturnsAsync(new List<DocumentChunk>());

            sessionRepo
                .Setup(r => r.UpdateAsync(It.IsAny<ChatSession>()))
                .Returns(Task.CompletedTask);

            var result = await service.ChatWithSubjectAsync(userId, subjectId, "What is polymorphism?", documentIds: docIds);

            Assert.NotNull(result);
            Assert.Contains("chưa được xử lý", result.Response.Answer, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ChatWithSubjectAsync_ChunksFound_CallsGeminiAndReturnsAnswer()
        {
            var (service, chunkRepo, embedding, sessionRepo, msgRepo, chatProvider) = BuildService();

            var userId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();

            sessionRepo
                .Setup(r => r.CreateAsync(It.IsAny<ChatSession>()))
                .ReturnsAsync((ChatSession s) => s);

            msgRepo
                .Setup(r => r.AddAsync(It.IsAny<ChatMessage>()))
                .ReturnsAsync((ChatMessage m) => m);

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), default))
                .ReturnsAsync(new float[768]);

            var chunks = new List<DocumentChunk> { MakeChunk(content: "SOLID stands for...") };
            chunkRepo
                .Setup(r => r.SearchSimilarChunksAsync(subjectId, It.IsAny<Vector>(), 5, null, default))
                .ReturnsAsync(chunks);

            chatProvider
                .Setup(c => c.GenerateTextAsync(It.IsAny<string>(), It.IsAny<string>(), default))
                .ReturnsAsync(("SOLID is a set of design principles [1].", 100, 50));

            sessionRepo
                .Setup(r => r.UpdateAsync(It.IsAny<ChatSession>()))
                .Returns(Task.CompletedTask);

            var result = await service.ChatWithSubjectAsync(userId, subjectId, "What is SOLID?");

            Assert.NotNull(result);
            Assert.NotNull(result.Response.Answer);
            chatProvider.Verify(c => c.GenerateTextAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Once);
        }

        [Fact]
        public async Task ChatWithSubjectAsync_EmbeddingThrows_ReturnsErrorMessage()
        {
            var (service, chunkRepo, embedding, sessionRepo, msgRepo, chatProvider) = BuildService();

            var userId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();

            sessionRepo
                .Setup(r => r.CreateAsync(It.IsAny<ChatSession>()))
                .ReturnsAsync((ChatSession s) => s);

            msgRepo
                .Setup(r => r.AddAsync(It.IsAny<ChatMessage>()))
                .ReturnsAsync((ChatMessage m) => m);

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), default))
                .ThrowsAsync(new Exception("Embedding API failure"));

            sessionRepo
                .Setup(r => r.UpdateAsync(It.IsAny<ChatSession>()))
                .Returns(Task.CompletedTask);

            var result = await service.ChatWithSubjectAsync(userId, subjectId, "Some question");

            Assert.NotNull(result);
            Assert.Contains("lỗi", result.Response.Answer, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ChatWithSubjectAsync_GeminiThrows_ReturnsErrorMessage()
        {
            var (service, chunkRepo, embedding, sessionRepo, msgRepo, chatProvider) = BuildService();

            var userId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();

            sessionRepo
                .Setup(r => r.CreateAsync(It.IsAny<ChatSession>()))
                .ReturnsAsync((ChatSession s) => s);

            msgRepo
                .Setup(r => r.AddAsync(It.IsAny<ChatMessage>()))
                .ReturnsAsync((ChatMessage m) => m);

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), default))
                .ReturnsAsync(new float[768]);

            var chunks = new List<DocumentChunk> { MakeChunk() };
            chunkRepo
                .Setup(r => r.SearchSimilarChunksAsync(subjectId, It.IsAny<Vector>(), 5, null, default))
                .ReturnsAsync(chunks);

            chatProvider
                .Setup(c => c.GenerateTextAsync(It.IsAny<string>(), It.IsAny<string>(), default))
                .ThrowsAsync(new Exception("Gemini quota exceeded"));

            sessionRepo
                .Setup(r => r.UpdateAsync(It.IsAny<ChatSession>()))
                .Returns(Task.CompletedTask);

            var result = await service.ChatWithSubjectAsync(userId, subjectId, "What is SOLID?");

            Assert.NotNull(result);
            Assert.Contains("lỗi", result.Response.Answer, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ChatWithSubjectAsync_PersistsUserAndAssistantMessages()
        {
            var (service, chunkRepo, embedding, sessionRepo, msgRepo, chatProvider) = BuildService();

            var userId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();

            sessionRepo
                .Setup(r => r.CreateAsync(It.IsAny<ChatSession>()))
                .ReturnsAsync((ChatSession s) => s);

            var addedMessages = new List<ChatMessage>();
            msgRepo
                .Setup(r => r.AddAsync(It.IsAny<ChatMessage>()))
                .Callback<ChatMessage>(m => addedMessages.Add(m))
                .ReturnsAsync((ChatMessage m) => m);

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), default))
                .ReturnsAsync(new float[768]);

            chunkRepo
                .Setup(r => r.SearchSimilarChunksAsync(subjectId, It.IsAny<Vector>(), 5, null, default))
                .ReturnsAsync(new List<DocumentChunk>());

            sessionRepo
                .Setup(r => r.UpdateAsync(It.IsAny<ChatSession>()))
                .Returns(Task.CompletedTask);

            await service.ChatWithSubjectAsync(userId, subjectId, "My question");

            // Both user and assistant messages should be persisted
            Assert.Equal(2, addedMessages.Count);
            Assert.Contains(addedMessages, m => m.Role == "user");
            Assert.Contains(addedMessages, m => m.Role == "assistant");
        }

        [Fact]
        public async Task ChatWithSubjectAsync_UpdatesSessionTimestamp()
        {
            var (service, chunkRepo, embedding, sessionRepo, msgRepo, chatProvider) = BuildService();

            var userId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();

            sessionRepo
                .Setup(r => r.CreateAsync(It.IsAny<ChatSession>()))
                .ReturnsAsync((ChatSession s) => s);

            msgRepo
                .Setup(r => r.AddAsync(It.IsAny<ChatMessage>()))
                .ReturnsAsync((ChatMessage m) => m);

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), default))
                .ReturnsAsync(new float[768]);

            chunkRepo
                .Setup(r => r.SearchSimilarChunksAsync(subjectId, It.IsAny<Vector>(), 5, null, default))
                .ReturnsAsync(new List<DocumentChunk>());

            sessionRepo
                .Setup(r => r.UpdateAsync(It.IsAny<ChatSession>()))
                .Returns(Task.CompletedTask);

            await service.ChatWithSubjectAsync(userId, subjectId, "Question?");

            // Should update session timestamp
            sessionRepo.Verify(r => r.UpdateAsync(It.IsAny<ChatSession>()), Times.Once);
        }

        [Fact]
        public async Task ChatWithSubjectAsync_UserMessagePersistsBeforeRAG()
        {
            var (service, chunkRepo, embedding, sessionRepo, msgRepo, chatProvider) = BuildService();

            var userId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            const string query = "Tell me about inheritance";

            sessionRepo
                .Setup(r => r.CreateAsync(It.IsAny<ChatSession>()))
                .ReturnsAsync((ChatSession s) => s);

            ChatMessage? firstAddedMessage = null;
            msgRepo
                .Setup(r => r.AddAsync(It.IsAny<ChatMessage>()))
                .Callback<ChatMessage>(m => firstAddedMessage ??= m)
                .ReturnsAsync((ChatMessage m) => m);

            embedding
                .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), default))
                .ReturnsAsync(new float[768]);

            chunkRepo
                .Setup(r => r.SearchSimilarChunksAsync(subjectId, It.IsAny<Vector>(), 5, null, default))
                .ReturnsAsync(new List<DocumentChunk>());

            sessionRepo
                .Setup(r => r.UpdateAsync(It.IsAny<ChatSession>()))
                .Returns(Task.CompletedTask);

            await service.ChatWithSubjectAsync(userId, subjectId, query);

            Assert.NotNull(firstAddedMessage);
            Assert.Equal("user", firstAddedMessage!.Role);
            Assert.Equal(query, firstAddedMessage.Content);
        }

        // ── GetUserSessionsAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task GetUserSessionsAsync_ReturnsMappedSessionDtos()
        {
            var (service, _, _, sessionRepo, _, _) = BuildService();

            var userId = Guid.NewGuid();
            var sessions = new List<ChatSession>
            {
                MakeSession(userId),
                MakeSession(userId)
            };

            sessionRepo
                .Setup(r => r.GetByUserAndSubjectAsync(userId, null))
                .ReturnsAsync(sessions);

            var result = (await service.GetUserSessionsAsync(userId, null)).ToList();

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetUserSessionsAsync_LimitsResultsToDefault50()
        {
            var (service, _, _, sessionRepo, _, _) = BuildService();

            var userId = Guid.NewGuid();
            var sessions = Enumerable.Range(0, 80)
                .Select(i =>
                {
                    var s = MakeSession(userId);
                    s.UpdatedAt = DateTime.UtcNow.AddSeconds(-i);
                    return s;
                })
                .ToList();

            sessionRepo
                .Setup(r => r.GetByUserAndSubjectAsync(userId, null))
                .ReturnsAsync(sessions);

            var result = (await service.GetUserSessionsAsync(userId, null)).ToList();

            Assert.Equal(50, result.Count);
        }

        [Fact]
        public async Task GetUserSessionsAsync_ReturnsSessionsOrderedByUpdatedAtDesc()
        {
            var (service, _, _, sessionRepo, _, _) = BuildService();

            var userId = Guid.NewGuid();
            var old = MakeSession(userId);
            old.UpdatedAt = DateTime.UtcNow.AddDays(-3);
            var recent = MakeSession(userId);
            recent.UpdatedAt = DateTime.UtcNow;

            var sessions = new List<ChatSession> { old, recent };

            sessionRepo
                .Setup(r => r.GetByUserAndSubjectAsync(userId, null))
                .ReturnsAsync(sessions);

            var result = (await service.GetUserSessionsAsync(userId, null)).ToList();

            Assert.Equal(recent.Id, result[0].Id);
        }

        [Fact]
        public async Task GetUserSessionsAsync_EmptyRepository_ReturnsEmpty()
        {
            var (service, _, _, sessionRepo, _, _) = BuildService();

            var userId = Guid.NewGuid();
            sessionRepo
                .Setup(r => r.GetByUserAndSubjectAsync(userId, null))
                .ReturnsAsync(new List<ChatSession>());

            var result = await service.GetUserSessionsAsync(userId, null);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetUserSessionsAsync_WithSubjectFilter_PassesSubjectIdToRepo()
        {
            var (service, _, _, sessionRepo, _, _) = BuildService();

            var userId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();

            sessionRepo
                .Setup(r => r.GetByUserAndSubjectAsync(userId, subjectId))
                .ReturnsAsync(new List<ChatSession>());

            await service.GetUserSessionsAsync(userId, subjectId);

            sessionRepo.Verify(r => r.GetByUserAndSubjectAsync(userId, subjectId), Times.Once);
        }

        [Fact]
        public async Task GetUserSessionsAsync_CustomLimit_RespectsLimit()
        {
            var (service, _, _, sessionRepo, _, _) = BuildService();

            var userId = Guid.NewGuid();
            var sessions = Enumerable.Range(0, 20)
                .Select(_ => MakeSession(userId))
                .ToList();

            for (int i = 0; i < sessions.Count; i++)
            {
                sessions[i].UpdatedAt = DateTime.UtcNow.AddSeconds(-i);
            }

            sessionRepo
                .Setup(r => r.GetByUserAndSubjectAsync(userId, null))
                .ReturnsAsync(sessions);

            var result = (await service.GetUserSessionsAsync(userId, null, limit: 5)).ToList();

            Assert.Equal(5, result.Count);
        }

        // ── GetSessionWithMessagesAsync ──────────────────────────────────────────

        [Fact]
        public async Task GetSessionWithMessagesAsync_SessionNotFound_ReturnsNull()
        {
            var (service, _, _, sessionRepo, _, _) = BuildService();

            sessionRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((ChatSession?)null);

            var result = await service.GetSessionWithMessagesAsync(Guid.NewGuid(), Guid.NewGuid());

            Assert.Null(result);
        }

        [Fact]
        public async Task GetSessionWithMessagesAsync_SessionBelongsToDifferentUser_ReturnsNull()
        {
            var (service, _, _, sessionRepo, _, _) = BuildService();

            var session = MakeSession(userId: Guid.NewGuid());
            sessionRepo
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(session);

            var result = await service.GetSessionWithMessagesAsync(session.Id, Guid.NewGuid());

            Assert.Null(result);
        }

        [Fact]
        public async Task GetSessionWithMessagesAsync_ValidSession_ReturnsMappedDetail()
        {
            var (service, _, _, sessionRepo, _, _) = BuildService();

            var userId = Guid.NewGuid();
            var session = MakeSession(userId);
            session.Messages = new List<ChatMessage>
            {
                MakeMessage(session.Id, "user", "Hello"),
                MakeMessage(session.Id, "assistant", "Hi there!")
            };

            sessionRepo
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(session);

            var result = await service.GetSessionWithMessagesAsync(session.Id, userId);

            Assert.NotNull(result);
            Assert.Equal(session.Id, result!.Id);
            Assert.Equal(2, result.Messages.Count);
        }

        [Fact]
        public async Task GetSessionWithMessagesAsync_MessagesOrderedByCreatedAt()
        {
            var (service, _, _, sessionRepo, _, _) = BuildService();

            var userId = Guid.NewGuid();
            var session = MakeSession(userId);
            var msg1 = MakeMessage(session.Id, "user", "First");
            msg1.CreatedAt = DateTime.UtcNow.AddMinutes(-2);
            var msg2 = MakeMessage(session.Id, "assistant", "Second");
            msg2.CreatedAt = DateTime.UtcNow.AddMinutes(-1);

            session.Messages = new List<ChatMessage> { msg2, msg1 }; // reversed order

            sessionRepo
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(session);

            var result = await service.GetSessionWithMessagesAsync(session.Id, userId);

            Assert.NotNull(result);
            Assert.Equal("First", result!.Messages[0].Content);
            Assert.Equal("Second", result.Messages[1].Content);
        }

        // ── GetSessionMessagesAsync ──────────────────────────────────────────────

        [Fact]
        public async Task GetSessionMessagesAsync_SessionNotFound_ReturnsEmpty()
        {
            var (service, _, _, sessionRepo, _, _) = BuildService();

            sessionRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((ChatSession?)null);

            var result = await service.GetSessionMessagesAsync(Guid.NewGuid(), Guid.NewGuid());

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetSessionMessagesAsync_NotOwner_ReturnsEmpty()
        {
            var (service, _, _, sessionRepo, _, _) = BuildService();

            var session = MakeSession(userId: Guid.NewGuid());
            sessionRepo
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(session);

            var result = await service.GetSessionMessagesAsync(session.Id, Guid.NewGuid());

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetSessionMessagesAsync_ValidSession_ReturnsAllMessages()
        {
            var (service, _, _, sessionRepo, _, _) = BuildService();

            var userId = Guid.NewGuid();
            var session = MakeSession(userId);
            session.Messages = new List<ChatMessage>
            {
                MakeMessage(session.Id, "user", "Q1"),
                MakeMessage(session.Id, "assistant", "A1"),
                MakeMessage(session.Id, "user", "Q2")
            };

            sessionRepo
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(session);

            var result = (await service.GetSessionMessagesAsync(session.Id, userId)).ToList();

            Assert.Equal(3, result.Count);
        }

        [Fact]
        public async Task GetSessionMessagesAsync_MessagesOrderedByCreatedAt()
        {
            var (service, _, _, sessionRepo, _, _) = BuildService();

            var userId = Guid.NewGuid();
            var session = MakeSession(userId);

            var msg1 = MakeMessage(session.Id, "user", "First");
            msg1.CreatedAt = DateTime.UtcNow.AddMinutes(-5);

            var msg2 = MakeMessage(session.Id, "assistant", "Second");
            msg2.CreatedAt = DateTime.UtcNow;

            session.Messages = new List<ChatMessage> { msg2, msg1 }; // reversed

            sessionRepo
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(session);

            var result = (await service.GetSessionMessagesAsync(session.Id, userId)).ToList();

            Assert.Equal("First", result[0].Content);
        }

        // ── DeleteSessionAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task DeleteSessionAsync_SessionNotFound_DoesNotThrow()
        {
            var (service, _, _, sessionRepo, _, _) = BuildService();

            sessionRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((ChatSession?)null);

            // Should not throw
            var exception = await Record.ExceptionAsync(() =>
                service.DeleteSessionAsync(Guid.NewGuid(), Guid.NewGuid()));

            Assert.Null(exception);
        }

        [Fact]
        public async Task DeleteSessionAsync_NotOwner_DoesNotDelete()
        {
            var (service, _, _, sessionRepo, _, _) = BuildService();

            var session = MakeSession(userId: Guid.NewGuid());
            sessionRepo
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(session);

            await service.DeleteSessionAsync(session.Id, Guid.NewGuid());

            sessionRepo.Verify(r => r.DeleteAsync(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task DeleteSessionAsync_ValidOwner_DeletesSession()
        {
            var (service, _, _, sessionRepo, _, _) = BuildService();

            var userId = Guid.NewGuid();
            var session = MakeSession(userId);

            sessionRepo
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(session);

            sessionRepo
                .Setup(r => r.DeleteAsync(session.Id))
                .Returns(Task.CompletedTask);

            await service.DeleteSessionAsync(session.Id, userId);

            sessionRepo.Verify(r => r.DeleteAsync(session.Id), Times.Once);
        }

        // ── RenameSessionAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task RenameSessionAsync_SessionNotFound_DoesNotThrow()
        {
            var (service, _, _, sessionRepo, _, _) = BuildService();

            sessionRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((ChatSession?)null);

            var exception = await Record.ExceptionAsync(() =>
                service.RenameSessionAsync(Guid.NewGuid(), Guid.NewGuid(), "New Title"));

            Assert.Null(exception);
        }

        [Fact]
        public async Task RenameSessionAsync_NotOwner_DoesNotRename()
        {
            var (service, _, _, sessionRepo, _, _) = BuildService();

            var session = MakeSession(userId: Guid.NewGuid());
            sessionRepo
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(session);

            await service.RenameSessionAsync(session.Id, Guid.NewGuid(), "New Title");

            sessionRepo.Verify(r => r.UpdateAsync(It.IsAny<ChatSession>()), Times.Never);
        }

        [Fact]
        public async Task RenameSessionAsync_ValidOwner_RenamesAndUpdates()
        {
            var (service, _, _, sessionRepo, _, _) = BuildService();

            var userId = Guid.NewGuid();
            var session = MakeSession(userId);

            sessionRepo
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(session);

            sessionRepo
                .Setup(r => r.UpdateAsync(It.IsAny<ChatSession>()))
                .Returns(Task.CompletedTask);

            await service.RenameSessionAsync(session.Id, userId, "  New Title  ");

            Assert.Equal("New Title", session.Title); // Should be trimmed
            sessionRepo.Verify(r => r.UpdateAsync(session), Times.Once);
        }

        [Fact]
        public async Task RenameSessionAsync_TrimsWhitespace()
        {
            var (service, _, _, sessionRepo, _, _) = BuildService();

            var userId = Guid.NewGuid();
            var session = MakeSession(userId);

            sessionRepo
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(session);

            sessionRepo
                .Setup(r => r.UpdateAsync(It.IsAny<ChatSession>()))
                .Returns(Task.CompletedTask);

            await service.RenameSessionAsync(session.Id, userId, "  Padded Title   ");

            Assert.Equal("Padded Title", session.Title);
        }

        [Fact]
        public async Task RenameSessionAsync_EmptyTitle_SetsEmptyString()
        {
            var (service, _, _, sessionRepo, _, _) = BuildService();

            var userId = Guid.NewGuid();
            var session = MakeSession(userId);

            sessionRepo
                .Setup(r => r.GetByIdAsync(session.Id))
                .ReturnsAsync(session);

            sessionRepo
                .Setup(r => r.UpdateAsync(It.IsAny<ChatSession>()))
                .Returns(Task.CompletedTask);

            await service.RenameSessionAsync(session.Id, userId, "");

            Assert.Equal("", session.Title);
        }
    }

    /// <summary>
    /// Extension helper for MakeSession
    /// </summary>
    internal static class ChatSessionExtensions
    {
        public static ChatSession MakeSession(Guid? userId = null, Guid? sessionId = null, Guid? subjectId = null, DateTime? createdAt = null)
            => new ChatSession
            {
                Id = sessionId ?? Guid.NewGuid(),
                UserId = userId ?? Guid.NewGuid(),
                SubjectId = subjectId ?? Guid.NewGuid(),
                Title = "Test Session",
                CreatedAt = createdAt ?? DateTime.UtcNow,
                UpdatedAt = createdAt ?? DateTime.UtcNow,
                Messages = new List<ChatMessage>()
            };
    }
}
