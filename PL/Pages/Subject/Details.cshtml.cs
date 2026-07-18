using System.Security.Claims;
using BLL.Interfaces;
using Core.DTOs.Documents;
using Core.DTOs.Subject;
using Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using PL.ViewModels.Documents;

namespace PL.Pages.Subject
{
    [Authorize]
    public class DetailsModel : PageModel
    {
        private readonly ISubjectService _subjectService;
        private readonly IDocumentService _documentService;
        private readonly Microsoft.AspNetCore.SignalR.IHubContext<PL.Hubs.LmsHub> _hubContext;
        private readonly IDocumentConflictService _documentConflictService;

        public DetailsModel(ISubjectService subjectService, IDocumentService documentService, Microsoft.AspNetCore.SignalR.IHubContext<PL.Hubs.LmsHub> hubContext, IDocumentConflictService documentConflictService)
        {
            _subjectService = subjectService;
            _documentService = documentService;
            _hubContext = hubContext;
            _documentConflictService = documentConflictService;
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

            Documents = await _documentService.GetVisibleDocumentsBySubjectIdAsync(id, UserRole);
            return Page();
        }

        public async Task<JsonResult> OnPostUploadFileAsync(IFormFile file, Guid subjectId)
        {
            var userId = GetUserId();
            if (userId == null) return new JsonResult(new { success = false, message = "Không có quyền truy cập." });
            if (file == null || file.Length == 0) return new JsonResult(new { success = false, message = "Vui lòng chọn tệp tin." });

            // Backend validation: File extension
            var allowedExtensions = new[] { ".pdf", ".docx", ".md", ".txt" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return new JsonResult(new { success = false, message = "Định dạng tệp không được hỗ trợ. Chỉ chấp nhận các tệp .pdf, .docx, .md, .txt." });
            }

            // Backend validation: File size (max 15MB)
            const long maxFileSize = 15 * 1024 * 1024; // 15MB
            if (file.Length > maxFileSize)
            {
                return new JsonResult(new { success = false, message = "Dung lượng tệp vượt quá giới hạn cho phép (tối đa 15MB)." });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var dto = new DocumentUploadDto
                {
                    UploadedBy = userId.Value,
                    SubjectId = subjectId,
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    FileSize = file.Length,
                    Content = stream
                };
                var r = await _documentService.UploadAsync(dto);
                if (r.IsSuccess)
                {
                    await _hubContext.Clients.All.SendAsync("ReceiveDocumentUpdate", "Upload", subjectId, Guid.Empty, file.FileName);
                    return new JsonResult(new { success = true });
                }
                return new JsonResult(new { success = false, message = r.ErrorMessage });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id, Guid subjectId)
        {
            var userId = GetUserId();
            if (userId == null) return Challenge();
            var r = await _documentService.DeleteAsync(id, userId.Value);
            if (r.IsSuccess)
            {
                TempData["SuccessMessage"] = "Document deleted successfully.";
                await _hubContext.Clients.All.SendAsync("ReceiveDocumentUpdate", "Delete", subjectId, id, "");
            }
            else TempData["ErrorMessage"] = r.ErrorMessage;
            return RedirectToPage(new { id = subjectId });
        }

        public async Task<IActionResult> OnPostRetryAsync(Guid id, Guid subjectId)
        {
            var userId = GetUserId();
            if (userId == null) return Challenge();
            var r = await _documentService.RetryProcessingAsync(id, userId.Value);
            if (r.IsSuccess)
            {
                TempData["SuccessMessage"] = "AI processing restarted successfully.";
                await _hubContext.Clients.All.SendAsync("ReceiveDocumentUpdate", "Retry", subjectId, id, "");
            }
            else TempData["ErrorMessage"] = r.ErrorMessage;
            return RedirectToPage(new { id = subjectId });
        }

        public async Task<JsonResult> OnGetGetStatusAsync(Guid docId)
        {
            var d = await _documentService.GetDocumentByIdAsync(docId);
            if (d == null) return new JsonResult(new { });
            
            var progress = await _documentService.GetProcessingProgressAsync(docId);
            var displayStatus = d.Status switch
            {
                DocumentStatus.Success => "Thành công",
                DocumentStatus.Failed => "Thất bại",
                DocumentStatus.Processing => "Đang xử lý",
                DocumentStatus.Pending => "Đang chờ",
                DocumentStatus.Conflict => "Trùng nội dung – cần kiểm tra",
                _ => d.Status.ToString()
            };
            return new JsonResult(new 
            { 
                id = d.Id, 
                status = d.Status.ToString(), 
                displayStatus,
                processed = progress.Processed, 
                total = progress.Total 
            });
        }

        public async Task<JsonResult> OnGetCompareConflictAsync(Guid docId, CancellationToken cancellationToken)
        {
            var result = await _documentConflictService.CompareAsync(docId, cancellationToken);
            if (!result.IsSuccess || result.Data == null)
                return new JsonResult(new { success = false, message = result.ErrorMessage });
            return new JsonResult(new
            {
                success = true,
                newFileName = result.Data.NewFileName,
                oldFileName = result.Data.OldFileName,
                analysis = result.Data.Analysis,
                cached = result.Data.Cached
            });
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
