using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BLL.Interfaces;
using Core.DTOs.Chat;
using Core.DTOs.Subject;
using Core.DTOs.Documents;
using Core.Entities;
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
        private readonly IDocumentService _documentService;

        public ChatModel(ISubjectService subjectService, IChatService chatService, IDocumentService documentService)
        {
            _subjectService = subjectService;
            _chatService = chatService;
            _documentService = documentService;
        }

        public SubjectDto Subject { get; set; } = new();

        public IReadOnlyList<ChatSessionDto> Sessions { get; set; } = Array.Empty<ChatSessionDto>();
        public IReadOnlyList<ChatMessageDto> InitialMessages { get; set; } = Array.Empty<ChatMessageDto>();
        public IReadOnlyList<DocumentDto> Documents { get; set; } = Array.Empty<DocumentDto>();

        [BindProperty(SupportsGet = true)]
        public Guid? SessionId { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid subjectId, Guid? sessionId)
        {
            SessionId = sessionId;
            var s = await _subjectService.GetSubjectByIdAsync(subjectId);
            if (s == null) return NotFound();
            Subject = s;
            Documents = await _documentService.GetVisibleDocumentsBySubjectIdAsync(subjectId, GetCurrentUserRole());

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

        public async Task<JsonResult> OnPostSendMessageAsync(Guid subjectId, string message, string? model = null, Guid? sessionId = null)
        {
            if (string.IsNullOrWhiteSpace(message))
                return new JsonResult(new { success = false, message = "Message cannot be empty." });

            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return new JsonResult(new { success = false, message = "Vui lÃ²ng Ä‘Äƒng nháº­p." });

            var result = await _chatService.ChatWithSubjectAsync(
                userId,
                subjectId,
                message,
                sessionId,
                model);
            return new JsonResult(new
            {
                success = true,
                reply = result.Response?.Answer,   // JS reads data.reply
                sources = result.Response?.Sources,
                sessionId = result.SessionId
            });
        }

        public async Task<JsonResult> OnPostDeleteSessionAsync(Guid sessionId)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return new JsonResult(new { success = false, message = "Vui lòng đăng nhập." });
            await _chatService.DeleteSessionAsync(sessionId, userId);
            return new JsonResult(new { success = true });
        }

        public async Task<JsonResult> OnPostRenameSessionAsync(Guid sessionId, string title)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return new JsonResult(new { success = false, message = "Vui lòng đăng nhập." });
            if (string.IsNullOrWhiteSpace(title))
                return new JsonResult(new { success = false, message = "Tên không được để trống." });

            await _chatService.RenameSessionAsync(sessionId, userId, title);
            return new JsonResult(new { success = true });
        }

        private Guid GetCurrentUserId()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(idStr, out var g) ? g : Guid.Empty;
        }

        private UserRole GetCurrentUserRole()
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            return Enum.TryParse<UserRole>(role, out var parsed) ? parsed : UserRole.Student;
        }
    }
}
