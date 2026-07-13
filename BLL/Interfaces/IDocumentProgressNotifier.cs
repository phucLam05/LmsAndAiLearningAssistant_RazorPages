using System;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IDocumentProgressNotifier
    {
        Task NotifyProgressAsync(Guid documentId, string status, int processedChunks, int totalChunks);
    }
}
