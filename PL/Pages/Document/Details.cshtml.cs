using BLL.Interfaces;
using Core.DTOs.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PL.Pages.Document
{
    [Authorize]
    public class DetailsModel : PageModel
    {
        private readonly IDocumentService _documentService;

        public DetailsModel(IDocumentService documentService)
        {
            _documentService = documentService;
        }

        public DocumentDto Document { get; set; } = new();
        public IReadOnlyList<DocumentChunkDto> Chunks { get; set; } = Array.Empty<DocumentChunkDto>();

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var doc = await _documentService.GetDocumentByIdAsync(id);
            if (doc == null) return NotFound();
            Document = doc;
            Chunks = await _documentService.GetDocumentChunksAsync(id);
            return Page();
        }
    }
}
