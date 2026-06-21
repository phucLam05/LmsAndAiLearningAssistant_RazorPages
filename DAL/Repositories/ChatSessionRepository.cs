using Core.Entities;
using DAL.Data;
using DAL.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DAL.Repositories
{
    public class ChatSessionRepository : IChatSessionRepository
    {
        private readonly ApplicationDbContext _context;
        public ChatSessionRepository(ApplicationDbContext context) { _context = context; }

        public async Task<ChatSession> CreateAsync(ChatSession session)
        {
            await _context.ChatSessions.AddAsync(session);
            await _context.SaveChangesAsync();
            return session;
        }

        public async Task<ChatSession?> GetByIdAsync(Guid id)
        {
            return await _context.ChatSessions
                .Include(s => s.Subject)
                .Include(s => s.Messages.OrderBy(m => m.CreatedAt))
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<IReadOnlyList<ChatSession>> GetByUserAndSubjectAsync(Guid userId, Guid? subjectId)
        {
            var q = _context.ChatSessions
                .AsNoTracking()
                .Include(s => s.Subject)
                .Where(s => s.UserId == userId);

            if (subjectId.HasValue) q = q.Where(s => s.SubjectId == subjectId.Value);

            return await q.OrderByDescending(s => s.UpdatedAt).ToListAsync();
        }

        public async Task UpdateAsync(ChatSession session)
        {
            session.UpdatedAt = DateTime.UtcNow;
            _context.ChatSessions.Update(session);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var s = await _context.ChatSessions.FindAsync(id);
            if (s == null) return;
            _context.ChatSessions.Remove(s);
            await _context.SaveChangesAsync();
        }

        public async Task<int> CountAllAsync()
            => await _context.ChatSessions.CountAsync();

        public async Task<IReadOnlyList<ChatSession>> GetAllAsync()
            => await _context.ChatSessions.AsNoTracking().ToListAsync();
    }
}
