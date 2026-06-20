using BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PL.Pages.Document
{
    [Authorize]
    public class ViewOriginalModel : PageModel
    {
        private readonly IDocumentService _documentService;

        public ViewOriginalModel(IDocumentService documentService)
        {
            _documentService = documentService;
        }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var result = await _documentService.DownloadDocumentAsync(id);
            if (result == null) return NotFound("Document not found or could not be downloaded.");
            var (stream, contentType, fileName) = result.Value;
            return File(stream, contentType);
        }
    }
}
