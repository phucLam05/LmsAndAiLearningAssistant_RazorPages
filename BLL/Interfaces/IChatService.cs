using System;
using System.Threading;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    /// <summary>
    /// Service interface for RAG Chatbot operations.
    /// </summary>
    public interface IChatService
    {
        Task<string> ChatWithSubjectAsync(Guid subjectId, string query, string? model = null, List<Guid>? documentIds = null, CancellationToken cancellationToken = default);
    }
}
