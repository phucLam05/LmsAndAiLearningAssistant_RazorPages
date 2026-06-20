using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.DTOs.Subject;

namespace BLL.Interfaces
{
    public interface IChatService
    {
        /// <summary>
        /// Run RAG + Gemini. If <paramref name="sessionId"/> is null, a new ChatSession is created
        /// for the user; otherwise the existing session is appended to. User and assistant messages
        /// are persisted, and the assistant message's sources JSON is stored as well.
        /// </summary>
        Task<ChatWithSessionDto> ChatWithSubjectAsync(
            Guid userId,
            Guid subjectId,
            string query,
            Guid? sessionId = null,
            string? model = null,
            List<Guid>? documentIds = null,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Core.DTOs.Chat.ChatSessionDto>> GetUserSessionsAsync(Guid userId, Guid? subjectId, int limit = 50);
        Task<Core.DTOs.Chat.ChatSessionDetailDto?> GetSessionWithMessagesAsync(Guid sessionId, Guid userId);
        Task<IReadOnlyList<Core.DTOs.Chat.ChatMessageDto>> GetSessionMessagesAsync(Guid sessionId, Guid userId);
        Task DeleteSessionAsync(Guid sessionId, Guid userId);
    }
}
