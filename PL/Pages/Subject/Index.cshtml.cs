using System.Security.Claims;
using BLL.Interfaces;
using Core.DTOs.Common;
using Core.DTOs.Subject;
using Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;

namespace PL.Pages.Subject
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ISubjectService _subjectService;
        private readonly IAdminService _adminService;
        private readonly Microsoft.AspNetCore.SignalR.IHubContext<PL.Hubs.LmsHub> _hubContext;

        public IndexModel(ISubjectService subjectService, IAdminService adminService, Microsoft.AspNetCore.SignalR.IHubContext<PL.Hubs.LmsHub> hubContext)
        {
            _subjectService = subjectService;
            _adminService = adminService;
            _hubContext = hubContext;
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
            if (UserRole == UserRole.Student)
            {
                return RedirectToPage("/Subject/Browse");
            }
            if (UserRole == UserRole.Lecturer)
            {
                return RedirectToPage("/Subject/MySubjects");
            }

            if (PageIndex < 1) PageIndex = 1;

            if (UserRole == UserRole.Admin)
            {
                Page = await _subjectService.GetPagedAllSubjectsAsync(Search, null, PageIndex, pageSize: 12);
                var users = await _adminService.GetAllUsersAsync();
                Lecturers = users.Where(u => u.Role == UserRole.Lecturer)
                    .Select(u => new LecturerOption { Id = u.Id, FullName = u.FullName }).ToList();
                IsAdminMode = true;
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
            if (success)
            {
                TempData["SuccessMessage"] = "Môn học đã được tạo thành công.";
                await _hubContext.Clients.All.SendAsync("ReceiveSubjectUpdate", "Create", SubjectCode, Name);
            }
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
            if (success)
            {
                TempData["SuccessMessage"] = "Subject updated successfully.";
                await _hubContext.Clients.All.SendAsync("ReceiveSubjectUpdate", "Edit", dto.Name, dto.Name);
            }
            else TempData["ErrorMessage"] = error ?? "Failed to update subject.";
            return RedirectToPage(new { Search, PageIndex });
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            if (GetUserRole() != UserRole.Admin) return Forbid();
            var (success, error) = await _subjectService.DeleteSubjectAsync(id);
            if (success)
            {
                TempData["SuccessMessage"] = "Subject deleted successfully.";
                await _hubContext.Clients.All.SendAsync("ReceiveSubjectUpdate", "Delete", "", "");
            }
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
                await _hubContext.Clients.All.SendAsync("ReceiveSubjectUpdate", "Assign", "", "");
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