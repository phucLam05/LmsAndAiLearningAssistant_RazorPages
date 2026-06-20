using System.Security.Claims;
using BLL.Interfaces;
using Core.DTOs.Documents;
using Core.DTOs.Subject;
using Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PL.Pages.Lecturer
{
    [Authorize(Roles = "Lecturer")]
    public class PortalModel : PageModel
    {
        private readonly ISubjectService _subjectService;
        private readonly IDocumentService _documentService;

        public PortalModel(ISubjectService subjectService, IDocumentService documentService)
        {
            _subjectService = subjectService;
            _documentService = documentService;
        }

        public class MockLecturerDoc
        {
            public Guid Id { get; set; }
            public string SubjectId { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
            public string FileSizeStr { get; set; } = string.Empty;
            public string Status { get; set; } = "Success";
            public string StoredBy { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
        }

        public List<SubjectDto> Subjects { get; set; } = new();
        public SubjectDto? SelectedSubject { get; set; }
        public Guid SelectedSubjectId { get; set; }
        public List<MockLecturerDoc> Documents { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(string? selectedSubjectId = null)
        {
            var userId = GetUserId();
            if (userId == null) return Challenge();

            Subjects = (await _subjectService.GetSubjectsByLecturerAsync(userId.Value)).ToList();
            if (string.IsNullOrEmpty(selectedSubjectId) && Subjects.Any())
            {
                selectedSubjectId = Subjects.First().Id.ToString();
            }
            if (Guid.TryParse(selectedSubjectId, out var sid))
            {
                SelectedSubjectId = sid;
                SelectedSubject = Subjects.FirstOrDefault(s => s.Id == sid);
                var dbDocs = await _documentService.GetDocumentsBySubjectIdAsync(sid);
                Documents = dbDocs.Select(d => new MockLecturerDoc
                {
                    Id = d.Id,
                    SubjectId = d.SubjectId?.ToString() ?? string.Empty,
                    FileName = d.FileName,
                    FileSizeStr = FormatFileSize(d.FileSize),
                    Status = d.Status.ToString(),
                    StoredBy = d.UploaderName ?? "System",
                    CreatedAt = d.CreatedAt
                }).ToList();
            }
            return Page();
        }

        public async Task<JsonResult> OnPostUploadLecturerFileAsync(IFormFile file, string subjectId)
        {
            var userId = GetUserId();
            if (userId == null) return new JsonResult(new { success = false, message = "User is not authenticated." });
            if (file == null || file.Length == 0) return new JsonResult(new { success = false, message = "Please select a file." });
            if (!Guid.TryParse(subjectId, out var sg)) return new JsonResult(new { success = false, message = "Invalid subject id." });

            var lecturerSubjects = await _subjectService.GetSubjectsByLecturerAsync(userId.Value);
            if (!lecturerSubjects.Any(s => s.Id == sg))
                return new JsonResult(new { success = false, message = "Unauthorized: You are not assigned to this subject." });

            await using var stream = file.OpenReadStream();
            var dto = new DocumentUploadDto
            {
                UploadedBy = userId.Value,
                SubjectId = sg,
                FileName = file.FileName,
                ContentType = file.ContentType,
                FileSize = file.Length,
                Content = stream
            };
            var result = await _documentService.UploadAsync(dto);
            if (!result.IsSuccess || result.Data == null)
                return new JsonResult(new { success = false, message = result.ErrorMessage });
            var docDto = result.Data;
            var mapped = new MockLecturerDoc
            {
                Id = docDto.Id,
                SubjectId = docDto.SubjectId?.ToString() ?? string.Empty,
                FileName = docDto.FileName,
                FileSizeStr = FormatFileSize(docDto.FileSize),
                Status = docDto.Status.ToString(),
                StoredBy = docDto.UploaderName ?? "Lecturer",
                CreatedAt = docDto.CreatedAt
            };
            return new JsonResult(new { success = true, document = mapped });
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id, Guid subjectId)
        {
            var userId = GetUserId();
            if (userId == null) return Challenge();
            var r = await _documentService.DeleteAsync(id, userId.Value);
            if (r.IsSuccess) TempData["SuccessMessage"] = "Document deleted successfully.";
            else TempData["ErrorMessage"] = r.ErrorMessage;
            return RedirectToPage(new { selectedSubjectId = subjectId.ToString() });
        }

        public async Task<IActionResult> OnPostRetryAsync(Guid id, Guid subjectId)
        {
            var userId = GetUserId();
            if (userId == null) return Challenge();
            var r = await _documentService.RetryProcessingAsync(id, userId.Value);
            if (r.IsSuccess) TempData["SuccessMessage"] = "AI processing restarted successfully.";
            else TempData["ErrorMessage"] = r.ErrorMessage;
            return RedirectToPage(new { selectedSubjectId = subjectId.ToString() });
        }

        public async Task<JsonResult> OnPostUpdateDocumentStatusAsync(Guid docId, string status)
        {
            // No-op preserved for API compatibility
            return new JsonResult(new { success = true });
        }

        public async Task<JsonResult> OnGetGetStatusAsync(Guid docId)
        {
            var d = await _documentService.GetDocumentByIdAsync(docId);
            if (d == null) return new JsonResult(new { });
            return new JsonResult(new { id = d.Id, status = d.Status.ToString() });
        }

        private Guid? GetUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var userId) ? userId : null;
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes <= 0) return "0 Bytes";
            string[] suffixes = { "Bytes", "KB", "MB", "GB" };
            int counter = 0;
            double number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
                if (counter >= suffixes.Length - 1) break;
            }
            return $"{number:F1} {suffixes[counter]}";
        }
    }
}
