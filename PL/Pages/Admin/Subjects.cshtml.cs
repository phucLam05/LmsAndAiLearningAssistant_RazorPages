using BLL.Interfaces;
using Core.DTOs.Common;
using Core.DTOs.Subject;
using Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PL.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class SubjectsModel : PageModel
    {
        private readonly ISubjectService _subjectService;
        private readonly IUserService _userService;
        public SubjectsModel(ISubjectService subjectService, IUserService userService)
        {
            _subjectService = subjectService;
            _userService = userService;
        }

        public PagedResult<SubjectDto> Paged { get; set; } = new();
        public IReadOnlyList<User> Lecturers { get; set; } = Array.Empty<User>();

        [BindProperty(SupportsGet = true)] public string? Search { get; set; }
        [BindProperty(SupportsGet = true)] public SubjectStatus? StatusFilter { get; set; }
        [BindProperty(SupportsGet = true)] public int PageIndex { get; set; } = 1;
        [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;

        [BindProperty] public CreateSubjectDto NewSubject { get; set; } = new();
        [BindProperty] public UpdateSubjectDto EditSubject { get; set; } = new();
        [BindProperty] public AssignLecturerDto AssignData { get; set; } = new();

        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostCreateAsync()
        {
            if (!ModelState.IsValid) { await LoadAsync(); return Page(); }
            var (ok, err) = await _subjectService.CreateSubjectAsync(NewSubject);
            if (!ok) ErrorMessage = err;
            else SuccessMessage = "ÄÃ£ táº¡o mÃ´n há»c.";
            NewSubject = new();
            await LoadAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostEditAsync()
        {
            if (!ModelState.IsValid) { await LoadAsync(); return Page(); }
            var (ok, err) = await _subjectService.UpdateSubjectAsync(EditSubject);
            if (!ok) ErrorMessage = err; else SuccessMessage = "ÄÃ£ cáº­p nháº­t mÃ´n há»c.";
            await LoadAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            var (ok, err) = await _subjectService.DeleteSubjectAsync(id);
            if (!ok) ErrorMessage = err; else SuccessMessage = "ÄÃ£ xÃ³a mÃ´n há»c.";
            await LoadAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAssignAsync()
        {
            var (ok, err) = await _subjectService.AssignLecturerAsync(AssignData);
            if (!ok) ErrorMessage = err; else SuccessMessage = "ÄÃ£ phÃ¢n cÃ´ng giáº£ng viÃªn.";
            await LoadAsync();
            return Page();
        }

        private async Task LoadAsync()
        {
            if (PageIndex < 1) PageIndex = 1;
            var all = (await _subjectService.GetAllSubjectsAsync()).ToList();
            var q = all.AsQueryable();
            if (!string.IsNullOrWhiteSpace(Search))
            {
                var s = Search.Trim();
                q = q.Where(x => x.SubjectCode.Contains(s, StringComparison.OrdinalIgnoreCase) || x.Name.Contains(s, StringComparison.OrdinalIgnoreCase));
            }
            if (StatusFilter.HasValue) q = q.Where(x => x.Status == StatusFilter.Value);
            var filtered = q.OrderBy(x => x.SubjectCode).ToList();
            Paged = new PagedResult<SubjectDto>
            {
                Items = filtered.Skip((PageIndex - 1) * PageSize).Take(PageSize),
                TotalCount = filtered.Count,
                PageIndex = PageIndex,
                PageSize = PageSize,
            };

            // Lecturers for the assignment dropdown
            Lecturers = await _userService.GetLecturersAsync();
        }
    }
}
