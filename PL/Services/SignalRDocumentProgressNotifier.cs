using BLL.Interfaces;
using Microsoft.AspNetCore.SignalR;
using PL.Hubs;
using System;
using System.Threading.Tasks;

namespace PL.Services
{
    public class SignalRDocumentProgressNotifier : IDocumentProgressNotifier
    {
        private readonly IHubContext<LmsHub> _hubContext;

        public SignalRDocumentProgressNotifier(IHubContext<LmsHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task NotifyProgressAsync(Guid documentId, string status, int processedChunks, int totalChunks)
        {
            await _hubContext.Clients.All.SendAsync("ReceiveDocumentProgress", documentId, status, processedChunks, totalChunks);
        }
    }
}
