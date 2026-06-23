using BLL.Interfaces;
using Core.DTOs.Chat;
using Core.DTOs.Subject;
using Core.Entities;
using DAL.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pgvector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BLL.Services
{
    public class ChatService : IChatService
    {
        private readonly IDocumentChunkRepository _documentChunkRepository;
        private readonly IGeminiEmbeddingProvider _embeddingProvider;
        private readonly IChatSessionRepository _chatSessionRepository;
        private readonly IChatMessageRepository _chatMessageRepository;
        private readonly IGeminiChatProvider _chatProvider;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            IDocumentChunkRepository documentChunkRepository,
            IGeminiEmbeddingProvider embeddingProvider,
            IChatSessionRepository chatSessionRepository,
            IChatMessageRepository chatMessageRepository,
            IGeminiChatProvider chatProvider,
            ILogger<ChatService> logger)
        {
            _documentChunkRepository = documentChunkRepository;
            _embeddingProvider = embeddingProvider;
            _chatSessionRepository = chatSessionRepository;
            _chatMessageRepository = chatMessageRepository;
            _chatProvider = chatProvider;
            _logger = logger;
        }

        private static readonly HashSet<string> AllowedModels = new(StringComparer.OrdinalIgnoreCase)
        {
            "gemini-2.5-flash", "gemini-2.5-flash-lite", "gemini-2.0-flash", "gemini-3.1-flash-lite", "gemini-3.5-flash",
        };
        private const string DefaultModel = "gemini-2.5-flash";

        public async Task<ChatWithSessionDto> ChatWithSubjectAsync(
            Guid userId,
            Guid subjectId,
            string query,
            Guid? sessionId = null,
            string? model = null,
            List<Guid>? documentIds = null,
            CancellationToken cancellationToken = default)
        {
            int? promptTokens = null;
            int? completionTokens = null;
            if (string.IsNullOrWhiteSpace(query))
            {
                return new ChatWithSessionDto
                {
                    Response = new ChatResponseDto { Answer = "Vui lòng nhập câu hỏi." }
                };
            }

            // ── Resolve session ──
            ChatSession session;
            if (sessionId.HasValue && sessionId.Value != Guid.Empty)
            {
                var existing = await _chatSessionRepository.GetByIdAsync(sessionId.Value);
                if (existing == null || existing.UserId != userId)
                {
                    // Invalid or not owned — create new
                    session = await CreateSessionAsync(userId, subjectId, query);
                }
                else
                {
                    session = existing;
                }
            }
            else
            {
                session = await CreateSessionAsync(userId, subjectId, query);
            }

            // ── Persist user message ──
            await _chatMessageRepository.AddAsync(new ChatMessage
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                Role = "user",
                Content = query,
                CreatedAt = DateTime.UtcNow,
            });

            // ── RAG search ──
            ChatResponseDto resp;
            try
            {
                var queryEmbedding = await _embeddingProvider.GetEmbeddingAsync(query, cancellationToken);
                var matchedChunks = await _documentChunkRepository.SearchSimilarChunksAsync(
                    subjectId, new Vector(queryEmbedding), limit: 5,
                    documentIds: documentIds, cancellationToken: cancellationToken);

                if (!matchedChunks.Any())
                {
                    bool hasDocumentFilter = documentIds != null && documentIds.Count > 0;
                    resp = new ChatResponseDto
                    {
                        Answer = hasDocumentFilter
                            ? "Xin lỗi, các tài liệu bạn đã chọn chưa được xử lý hoặc không có nội dung phù hợp."
                            : "Chưa có tài liệu cho môn học này. Vui lòng liên hệ giảng viên.",
                        Sources = new(),
                    };
                }
                else
                {
                    // Dedupe by (DocumentId, ChunkIndex)
                    var deduped = matchedChunks
                        .GroupBy(c => new { DocumentId = c.DocumentId ?? Guid.Empty, c.ChunkIndex })
                        .Select(g => g.First())
                        .ToList();

                    var contextBuilder = new StringBuilder();
                    var sources = new List<ChatSourceDto>();
                    int idx = 1;
                    foreach (var chunk in deduped)
                    {
                        var fileName = chunk.Document != null ? chunk.Document.FileName : "Tài liệu";
                        contextBuilder.AppendLine($"[Source {idx}]");
                        contextBuilder.AppendLine($"Document: {fileName}");
                        if (chunk.PageNumber.HasValue) contextBuilder.AppendLine($"Page: {chunk.PageNumber.Value}");
                        contextBuilder.AppendLine($"Content: {chunk.Content}");
                        contextBuilder.AppendLine("---");

                        sources.Add(new ChatSourceDto
                        {
                            Index = idx,
                            DocumentId = chunk.DocumentId ?? Guid.Empty,
                            FileName = fileName,
                            Content = chunk.Content,
                            PageNumber = chunk.PageNumber,
                        });
                        idx++;
                    }

                    var sourceCount = deduped.Count;
                    var systemPrompt = $@"Bạn là Trợ lý AI học tập đại học. Trả lời câu hỏi của sinh viên DỰA TRÊN các tài liệu được cung cấp.
Tài liệu tham khảo được đánh số từ [1] đến [{sourceCount}]. TUYỆT ĐỐI không dùng số trích dẫn nào ngoài phạm vi [1] đến [{sourceCount}].
Mỗi khẳng định lấy từ tài liệu PHẢI có trích dẫn ngay cuối câu, ví dụ: '...như vậy [1].' hoặc '...theo hai nguồn [1][2].'
KHÔNG thêm phần 'Tài liệu tham khảo', 'References' hay danh sách số ở cuối bài.
Nếu tài liệu không đủ thông tin, hãy nói rõ và gợi ý liên hệ giảng viên.
Trả lời ngắn gọn, dùng markdown khi phù hợp.";
                    var prompt = $"System context:\n{systemPrompt}\n\nCourse materials context:\n{contextBuilder}\n\nStudent's Question: {query}\n\nAnswer:";

                    var resolvedModel = (!string.IsNullOrWhiteSpace(model) && AllowedModels.Contains(model))
                        ? model : DefaultModel;

                    var generateResult = await _chatProvider.GenerateTextAsync(prompt, resolvedModel, cancellationToken);
                    var rawAnswer = generateResult.Answer;
                    promptTokens = generateResult.PromptTokens;
                    completionTokens = generateResult.CompletionTokens;

                    // ── Citations Re-mapping & Filtering ──
                    var usedIndices = new List<int>();
                    var matches = System.Text.RegularExpressions.Regex.Matches(rawAnswer, @"\[(\d+)\]");
                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        if (int.TryParse(m.Groups[1].Value, out var num) && !usedIndices.Contains(num))
                        {
                            usedIndices.Add(num);
                        }
                    }

                    var filteredSources = new List<ChatSourceDto>();
                    var indexMap = new Dictionary<int, int>();
                    int newIdx = 1;
                    foreach (var originalIndex in usedIndices)
                    {
                        var s = sources.FirstOrDefault(x => x.Index == originalIndex);
                        if (s != null)
                        {
                            indexMap[originalIndex] = newIdx;
                            filteredSources.Add(new ChatSourceDto
                            {
                                Index = newIdx,
                                DocumentId = s.DocumentId,
                                FileName = s.FileName,
                                Content = s.Content,
                                PageNumber = s.PageNumber
                            });
                            newIdx++;
                        }
                    }

                    var finalAnswer = System.Text.RegularExpressions.Regex.Replace(rawAnswer, @"\[(\d+)\]", m =>
                    {
                        var oldIdx = int.Parse(m.Groups[1].Value);
                        if (indexMap.TryGetValue(oldIdx, out var mapped))
                        {
                            return $"[{mapped}]";
                        }
                        return string.Empty; // Remove invalid/unused references
                    });

                    resp = new ChatResponseDto { Answer = finalAnswer, Sources = filteredSources };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during RAG chat for SubjectId: {SubjectId}", subjectId);
                resp = new ChatResponseDto
                {
                    Answer = $"Đã xảy ra lỗi khi giao tiếp với AI: {ex.Message}",
                    Sources = new(),
                };
            }

            // ── Persist assistant message ──
            await _chatMessageRepository.AddAsync(new ChatMessage
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                Role = "assistant",
                Content = resp.Answer,
                SourcesJson = JsonSerializer.Serialize(resp.Sources),
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                CreatedAt = DateTime.UtcNow,
            });

            // Update session timestamp
            session.UpdatedAt = DateTime.UtcNow;
            await _chatSessionRepository.UpdateAsync(session);

            return new ChatWithSessionDto { SessionId = session.Id, Response = resp };
        }

        public async Task<IReadOnlyList<ChatSessionDto>> GetUserSessionsAsync(Guid userId, Guid? subjectId, int limit = 50)
        {
            var sessions = await _chatSessionRepository.GetByUserAndSubjectAsync(userId, subjectId);
            return sessions
                .OrderByDescending(s => s.UpdatedAt)
                .Take(limit)
                .Select(s => new ChatSessionDto
                {
                    Id = s.Id,
                    UserId = s.UserId,
                    SubjectId = s.SubjectId,
                    SubjectName = s.Subject?.Name,
                    Title = s.Title,
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt,
                    MessageCount = s.Messages?.Count ?? 0,
                    Preview = s.Messages?.LastOrDefault()?.Content?[..Math.Min(80, s.Messages?.LastOrDefault()?.Content?.Length ?? 0)],
                }).ToList();
        }

        public async Task<ChatSessionDetailDto?> GetSessionWithMessagesAsync(Guid sessionId, Guid userId)
        {
            var s = await _chatSessionRepository.GetByIdAsync(sessionId);
            if (s == null || s.UserId != userId) return null;

            return new ChatSessionDetailDto
            {
                Id = s.Id,
                SubjectId = s.SubjectId,
                SubjectName = s.Subject?.Name,
                Title = s.Title,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                Messages = s.Messages
                    .OrderBy(m => m.CreatedAt)
                    .Select(m => new ChatMessageDto
                    {
                        Id = m.Id,
                        SessionId = m.SessionId,
                        Role = m.Role,
                        Content = m.Content,
                        Sources = string.IsNullOrEmpty(m.SourcesJson)
                            ? new()
                            : JsonSerializer.Deserialize<List<ChatSourceDto>>(m.SourcesJson) ?? new(),
                        CreatedAt = m.CreatedAt,
                    })
                    .ToList(),
            };
        }

        public async Task<IReadOnlyList<ChatMessageDto>> GetSessionMessagesAsync(Guid sessionId, Guid userId)
        {
            var s = await _chatSessionRepository.GetByIdAsync(sessionId);
            if (s == null || s.UserId != userId) return Array.Empty<ChatMessageDto>();
            return s.Messages
                .OrderBy(m => m.CreatedAt)
                .Select(m => new ChatMessageDto
                {
                    Id = m.Id,
                    SessionId = m.SessionId,
                    Role = m.Role,
                    Content = m.Content,
                    Sources = string.IsNullOrEmpty(m.SourcesJson)
                        ? new()
                        : JsonSerializer.Deserialize<List<ChatSourceDto>>(m.SourcesJson) ?? new(),
                    CreatedAt = m.CreatedAt,
                })
                .ToList();
        }

        public async Task DeleteSessionAsync(Guid sessionId, Guid userId)
        {
            var s = await _chatSessionRepository.GetByIdAsync(sessionId);
            if (s == null || s.UserId != userId) return;
            await _chatSessionRepository.DeleteAsync(sessionId);
        }

        public async Task RenameSessionAsync(Guid sessionId, Guid userId, string newTitle)
        {
            var s = await _chatSessionRepository.GetByIdAsync(sessionId);
            if (s == null || s.UserId != userId) return;
            s.Title = newTitle.Trim();
            await _chatSessionRepository.UpdateAsync(s);
        }

        // ── helpers ──
        private async Task<ChatSession> CreateSessionAsync(Guid userId, Guid subjectId, string titleSeed)
        {
            var title = titleSeed.Length > 60 ? titleSeed[..60] + "…" : titleSeed;
            var session = new ChatSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SubjectId = subjectId,
                Title = title,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            await _chatSessionRepository.CreateAsync(session);
            return session;
        }
    }
}
