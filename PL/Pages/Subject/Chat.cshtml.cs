using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BLL.Interfaces;
using Core.DTOs.Chat;
using Core.DTOs.Subject;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PL.Pages.Subject
{
    [Authorize]
    public class ChatModel : PageModel
    {
        private readonly ISubjectService _subjectService;
        private readonly IChatService _chatService;

        public ChatModel(ISubjectService subjectService, IChatService chatService)
        {
            _subjectService = subjectService;
            _chatService = chatService;
        }

        public SubjectDto Subject { get; set; } = new();

        public IReadOnlyList<ChatSessionDto> Sessions { get; set; } = Array.Empty<ChatSessionDto>();
        public IReadOnlyList<ChatMessageDto> InitialMessages { get; set; } = Array.Empty<ChatMessageDto>();

        [BindProperty(SupportsGet = true)]
        public Guid? SessionId { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid subjectId)
        {
            var s = await _subjectService.GetSubjectByIdAsync(subjectId);
            if (s == null) return NotFound();
            Subject = s;

            var userId = GetCurrentUserId();
            if (userId != Guid.Empty)
            {
                Sessions = await _chatService.GetUserSessionsAsync(userId, subjectId, limit: 50);
                if (SessionId.HasValue && SessionId.Value != Guid.Empty)
                {
                    InitialMessages = await _chatService.GetSessionMessagesAsync(SessionId.Value, userId);
                }
            }
            return Page();
        }

        public async Task<JsonResult> OnPostSendMessageAsync(Guid subjectId, string message, string? model = null, string? documentIds = null, Guid? sessionId = null)
        {
            if (string.IsNullOrWhiteSpace(message))
                return new JsonResult(new { success = false, message = "Message cannot be empty." });

            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return new JsonResult(new { success = false, message = "Vui lÃ²ng Ä‘Äƒng nháº­p." });

            List<Guid>? selectedDocIds = null;
            if (!string.IsNullOrWhiteSpace(documentIds))
            {
                selectedDocIds = documentIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
                    .Where(g => g.HasValue)
                    .Select(g => g!.Value)
                    .ToList();
            }

            var result = await _chatService.ChatWithSubjectAsync(
                userId,
                subjectId,
                message,
                sessionId,
                model,
                selectedDocIds);
            return new JsonResult(new
            {
                success = true,
                answer = result.Response?.Answer,
                sources = result.Response?.Sources,
                sessionId = result.SessionId
            });
        }

        public async Task<JsonResult> OnPostDeleteSessionAsync(Guid sessionId)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return new JsonResult(new { success = false, message = "Vui lÃ²ng Ä‘Äƒng nháº­p." });
            await _chatService.DeleteSessionAsync(sessionId, userId);
            return new JsonResult(new { success = true });
        }

        private Guid GetCurrentUserId()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(idStr, out var g) ? g : Guid.Empty;
        }
    }
}
