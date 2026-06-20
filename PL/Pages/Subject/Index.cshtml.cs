using System.Security.Claims;
using BLL.Interfaces;
using Core.DTOs.Common;
using Core.DTOs.Subject;
using Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PL.Pages.Subject
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ISubjectService _subjectService;
        private readonly IAdminService _adminService;

        public IndexModel(ISubjectService subjectService, IAdminService adminService)
        {
            _subjectService = subjectService;
            _adminService = adminService;
        }

        public PagedResult<SubjectDto> Page { get; set; } = PagedResult<SubjectDto>.Empty();
        public List<LecturerOption> Lecturers { get; set; } = new();
        public UserRole UserRole { get; set; }
        public bool IsAdminMode { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageIndex { get; set; } = 1;

        public async Task<IActionResult> OnGetAsync()
        {
            UserRole = GetUserRole();
            if (PageIndex < 1) PageIndex = 1;

            switch (UserRole)
            {
                case UserRole.Admin:
                    Page = await _subjectService.GetPagedAllSubjectsAsync(Search, null, PageIndex, pageSize: 12);
                    var users = await _adminService.GetAllUsersAsync();
                    Lecturers = users.Where(u => u.Role == UserRole.Lecturer)
                        .Select(u => new LecturerOption { Id = u.Id, FullName = u.FullName }).ToList();
                    IsAdminMode = true;
                    break;
                case UserRole.Lecturer:
                    var lectId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                    Page = await _subjectService.GetPagedSubjectsByLecturerAsync(lectId, Search, PageIndex, pageSize: 12);
                    IsAdminMode = false;
                    break;
                case UserRole.Student:
                    Page = await _subjectService.GetPagedActiveSubjectsAsync(Search, PageIndex, pageSize: 12);
                    IsAdminMode = false;
                    break;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostCreateAsync(string SubjectCode, string Name, string? Description, string? LecturerId)
        {
            if (GetUserRole() != UserRole.Admin) return Forbid();
            var dto = new CreateSubjectDto
            {
                SubjectCode = SubjectCode,
                Name = Name,
                Description = Description,
                LecturerId = string.IsNullOrEmpty(LecturerId) ? null : Guid.Parse(LecturerId)
            };
            var (success, error) = await _subjectService.CreateSubjectAsync(dto);
            if (success) TempData["SuccessMessage"] = "MÃ´n há»c Ä‘Ã£ Ä‘Æ°á»£c táº¡o thÃ nh cÃ´ng.";
            else TempData["ErrorMessage"] = error ?? "Failed to create subject.";
            return RedirectToPage(new { Search, PageIndex });
        }

        public async Task<IActionResult> OnPostEditAsync(Guid Id, string Name, string? Description, string? LecturerId, string Status)
        {
            if (GetUserRole() != UserRole.Admin) return Forbid();
            var status = Enum.TryParse<SubjectStatus>(Status, true, out var s) ? s : SubjectStatus.Active;
            var dto = new UpdateSubjectDto
            {
                Id = Id,
                Name = Name,
                Description = Description,
                LecturerId = string.IsNullOrEmpty(LecturerId) ? null : Guid.Parse(LecturerId),
                Status = status
            };
            var (success, error) = await _subjectService.UpdateSubjectAsync(dto);
            if (success) TempData["SuccessMessage"] = "Subject updated successfully.";
            else TempData["ErrorMessage"] = error ?? "Failed to update subject.";
            return RedirectToPage(new { Search, PageIndex });
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            if (GetUserRole() != UserRole.Admin) return Forbid();
            var (success, error) = await _subjectService.DeleteSubjectAsync(id);
            if (success) TempData["SuccessMessage"] = "Subject deleted successfully.";
            else TempData["ErrorMessage"] = error ?? "Failed to delete subject.";
            return RedirectToPage(new { Search, PageIndex });
        }

        /// <summary>
        /// Admin: gÃ¡n hoáº·c bá» gÃ¡n 1 giáº£ng viÃªn cho mÃ´n há»c mÃ  khÃ´ng cáº§n má»Ÿ Edit modal.
        /// LecturerId rá»—ng => bá» gÃ¡n. TÆ°Æ¡ng Ä‘Æ°Æ¡ng POST /Subject/AssignLecturer trong PL.
        /// </summary>
        public async Task<IActionResult> OnPostAssignLecturerAsync(Guid subjectId, string? lecturerId)
        {
            if (GetUserRole() != UserRole.Admin) return Forbid();

            Guid? parsedLecturerId = null;
            if (!string.IsNullOrWhiteSpace(lecturerId) && Guid.TryParse(lecturerId, out var lid))
                parsedLecturerId = lid;

            var dto = new AssignLecturerDto
            {
                SubjectId = subjectId,
                LecturerId = parsedLecturerId
            };

            var (success, error) = await _subjectService.AssignLecturerAsync(dto);
            if (success)
            {
                TempData["SuccessMessage"] = parsedLecturerId.HasValue
                    ? "Lecturer assigned successfully."
                    : "Lecturer removed from subject.";
            }
            else
            {
                TempData["ErrorMessage"] = error ?? "Failed to assign lecturer.";
            }
            return RedirectToPage(new { Search, PageIndex });
        }

        private UserRole GetUserRole()
        {
            var roleString = User.FindFirstValue(ClaimTypes.Role);
            return Enum.TryParse<UserRole>(roleString, out var role) ? role : UserRole.Student;
        }

        public class LecturerOption
        {
            public Guid Id { get; set; }
            public string FullName { get; set; } = string.Empty;
        }
    }
}