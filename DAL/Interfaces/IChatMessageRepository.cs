using Core.DTOs.Admin;
using Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IChatMessageRepository
    {
        Task<ChatMessage> AddAsync(ChatMessage message);
        Task<IReadOnlyList<ChatMessage>> GetBySessionIdAsync(Guid sessionId);
        Task<int> CountAllAsync();
        Task<long> GetTotalTokensAsync();
        Task<(long PromptTokens, long CompletionTokens)> GetTokenBreakdownAsync();
        Task<List<SubjectMessageStatsDto>> GetStatsGroupedBySubjectAsync();
    }
}
