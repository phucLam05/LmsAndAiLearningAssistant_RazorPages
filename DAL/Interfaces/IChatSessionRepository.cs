using Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IChatSessionRepository
    {
        Task<ChatSession> CreateAsync(ChatSession session);
        Task<ChatSession?> GetByIdAsync(Guid id);
        Task<IReadOnlyList<ChatSession>> GetByUserAndSubjectAsync(Guid userId, Guid? subjectId);
        Task UpdateAsync(ChatSession session);
        Task DeleteAsync(Guid id);
        Task<int> CountAllAsync();
        Task<IReadOnlyList<ChatSession>> GetAllAsync();
    }
}
