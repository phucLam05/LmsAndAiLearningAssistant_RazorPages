using System.Security.Claims;
using BLL.Interfaces;
using Core.DTOs.Documents;
using Core.DTOs.Subject;
using Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PL.Models.Documents;

namespace PL.Pages.Subject
{
    [Authorize]
    public class DetailsModel : PageModel
    {
        private readonly ISubjectService _subjectService;
        private readonly IDocumentService _documentService;

        public DetailsModel(ISubjectService subjectService, IDocumentService documentService)
        {
            _subjectService = subjectService;
            _documentService = documentService;
        }

        public SubjectDto Subject { get; set; } = new();
        public IReadOnlyList<DocumentDto> Documents { get; set; } = Array.Empty<DocumentDto>();
        public UserRole UserRole { get; set; }
        public Guid CurrentUserId { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            UserRole = GetUserRole();
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            CurrentUserId = Guid.TryParse(userIdString, out var u) ? u : Guid.Empty;

            var subject = await _subjectService.GetSubjectByIdAsync(id);
            if (subject == null) return NotFound();
            Subject = subject;

            // Student -> redirect to Chat
            if (UserRole == UserRole.Student) return RedirectToPage("/Subject/Chat", new { subjectId = id });

            Documents = await _documentService.GetDocumentsBySubjectIdAsync(id);
            return Page();
        }

        public async Task<IActionResult> OnPostUploadAsync(Guid SubjectId, List<IFormFile> Files)
        {
            var userId = GetUserId();
            if (userId == null) return Challenge();
            if (Files == null || Files.Count == 0)
            {
                TempData["ErrorMessage"] = "Vui lÃ²ng chá»n Ã­t nháº¥t má»™t tá»‡p tin há»£p lá»‡.";
                return RedirectToPage(new { id = SubjectId });
            }
            int success = 0, error = 0;
            string lastError = "";
            foreach (var file in Files)
            {
                try
                {
                    await using var stream = file.OpenReadStream();
                    var dto = new DocumentUploadDto
                    {
                        UploadedBy = userId.Value,
                        SubjectId = SubjectId,
                        FileName = file.FileName,
                        ContentType = file.ContentType,
                        FileSize = file.Length,
                        Content = stream
                    };
                    var r = await _documentService.UploadAsync(dto);
                    if (r.IsSuccess) success++;
                    else { error++; lastError = r.ErrorMessage; }
                }
                catch (Exception ex) { error++; lastError = ex.Message; }
            }
            if (error > 0)
            {
                if (success > 0) TempData["SuccessMessage"] = $"Táº£i lÃªn thÃ nh cÃ´ng {success} tÃ i liá»‡u. CÃ³ {error} tÃ i liá»‡u gáº·p lá»—i: {lastError}";
                else TempData["ErrorMessage"] = $"Lá»—i táº£i lÃªn tÃ i liá»‡u: {lastError}";
            }
            else
            {
                TempData["SuccessMessage"] = success > 1 ? $"ÄÃ£ táº£i lÃªn thÃ nh cÃ´ng {success} tÃ i liá»‡u vÃ  báº¯t Ä‘áº§u phÃ¢n tÃ­ch AI." : "TÃ i liá»‡u Ä‘Ã£ Ä‘Æ°á»£c táº£i lÃªn thÃ nh cÃ´ng vÃ  báº¯t Ä‘áº§u phÃ¢n tÃ­ch AI.";
            }
            return RedirectToPage(new { id = SubjectId });
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id, Guid subjectId)
        {
            var userId = GetUserId();
            if (userId == null) return Challenge();
            var r = await _documentService.DeleteAsync(id, userId.Value);
            if (r.IsSuccess) TempData["SuccessMessage"] = "Document deleted successfully.";
            else TempData["ErrorMessage"] = r.ErrorMessage;
            return RedirectToPage(new { id = subjectId });
        }

        public async Task<IActionResult> OnPostRetryAsync(Guid id, Guid subjectId)
        {
            var userId = GetUserId();
            if (userId == null) return Challenge();
            var r = await _documentService.RetryProcessingAsync(id, userId.Value);
            if (r.IsSuccess) TempData["SuccessMessage"] = "AI processing restarted successfully.";
            else TempData["ErrorMessage"] = r.ErrorMessage;
            return RedirectToPage(new { id = subjectId });
        }

        public async Task<JsonResult> OnGetGetStatusAsync(Guid docId)
        {
            var d = await _documentService.GetDocumentByIdAsync(docId);
            if (d == null) return new JsonResult(new { });
            return new JsonResult(new { id = d.Id, status = d.Status.ToString() });
        }

        private UserRole GetUserRole()
        {
            var roleString = User.FindFirstValue(ClaimTypes.Role);
            return Enum.TryParse<UserRole>(roleString, out var role) ? role : UserRole.Student;
        }
        private Guid? GetUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }
}
