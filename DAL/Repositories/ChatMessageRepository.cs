using Core.DTOs.Admin;
using Core.Entities;
using DAL.Data;
using DAL.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DAL.Repositories
{
    public class ChatMessageRepository : IChatMessageRepository
    {
        private readonly ApplicationDbContext _context;
        public ChatMessageRepository(ApplicationDbContext context) { _context = context; }

        public async Task<ChatMessage> AddAsync(ChatMessage message)
        {
            await _context.ChatMessages.AddAsync(message);
            await _context.SaveChangesAsync();
            return message;
        }

        public async Task<IReadOnlyList<ChatMessage>> GetBySessionIdAsync(Guid sessionId)
        {
            return await _context.ChatMessages
                .Where(m => m.SessionId == sessionId)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();
        }

        public async Task<int> CountAllAsync()
            => await _context.ChatMessages.CountAsync();

        public async Task<long> GetTotalTokensAsync()
        {
            return await _context.ChatMessages.SumAsync(m => (long)(m.PromptTokens ?? 0) + (long)(m.CompletionTokens ?? 0));
        }

        public async Task<(long PromptTokens, long CompletionTokens)> GetTokenBreakdownAsync()
        {
            var prompt = await _context.ChatMessages.SumAsync(m => (long)(m.PromptTokens ?? 0));
            var completion = await _context.ChatMessages.SumAsync(m => (long)(m.CompletionTokens ?? 0));
            return (prompt, completion);
        }

        public async Task<List<SubjectMessageStatsDto>> GetStatsGroupedBySubjectAsync()
        {
            return await _context.ChatMessages
                .Join(_context.ChatSessions,
                    m => m.SessionId,
                    s => s.Id,
                    (m, s) => new { m, s })
                .Where(x => x.s.SubjectId.HasValue)
                .GroupBy(x => x.s.SubjectId!.Value)
                .Select(g => new SubjectMessageStatsDto
                {
                    SubjectId = g.Key,
                    PromptTokens = g.Sum(x => (long)(x.m.PromptTokens ?? 0)),
                    CompletionTokens = g.Sum(x => (long)(x.m.CompletionTokens ?? 0)),
                    MessageCount = g.Count()
                })
                .ToListAsync();
        }
    }
}
