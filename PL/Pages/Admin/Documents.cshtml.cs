using System.Security.Claims;
using BLL.Interfaces;
using Core.DTOs.Common;
using Core.DTOs.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PL.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class DocumentsModel : PageModel
    {
        private readonly IDocumentService _documentService;

        public DocumentsModel(IDocumentService documentService)
        {
            _documentService = documentService;
        }

        public PagedResult<DocumentDto> Page { get; set; } = PagedResult<DocumentDto>.Empty();
        public long TotalSizeBytes { get; set; }
        public int SuccessCount { get; set; }
        public int ProcessingCount { get; set; }
        public int FailedCount { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Status { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageIndex { get; set; } = 1;

        public async Task<IActionResult> OnGetAsync()
        {
            if (PageIndex < 1) PageIndex = 1;
            const int pageSize = 20;

            Page = await _documentService.GetPagedDocumentsAsync(Search, Status, null, PageIndex, pageSize);

            // Stats from the full set (used in header cards)
            var all = await _documentService.GetAllDocumentsAsync();
            TotalSizeBytes = all.Sum(d => d.FileSize);
            SuccessCount = all.Count(d => d.Status == Core.Entities.DocumentStatus.Success);
            ProcessingCount = all.Count(d => d.Status == Core.Entities.DocumentStatus.Processing);
            FailedCount = all.Count(d => d.Status == Core.Entities.DocumentStatus.Failed);

            return Page();
        }

        public async Task<IActionResult> OnPostRetryAsync(Guid id, Guid subjectId, string? redirectUrl = null)
        {
            var userId = GetUserId();
            if (userId == null) return Challenge();

            var result = await _documentService.RetryProcessingAsync(id, userId.Value);
            if (result.IsSuccess) TempData["SuccessMessage"] = "AI processing restarted successfully.";
            else TempData["ErrorMessage"] = result.ErrorMessage;

            if (!string.IsNullOrEmpty(redirectUrl)) return Redirect(redirectUrl);
            return RedirectToPage("/Admin/Documents", new { Search, Status, PageIndex });
        }

        private Guid? GetUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }
}
