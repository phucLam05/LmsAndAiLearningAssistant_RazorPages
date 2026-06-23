using BLL.Interfaces;
using Core.DTOs.Common;
using Core.DTOs.Subject;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PL.Pages.Subject
{
    [Authorize(Roles = "Student")]
    public class BrowseModel : PageModel
    {
        private readonly ISubjectService _subjectService;

        public BrowseModel(ISubjectService subjectService)
        {
            _subjectService = subjectService;
        }

        public PagedResult<SubjectDto> Page { get; set; } = PagedResult<SubjectDto>.Empty();

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageIndex { get; set; } = 1;

        public async Task<IActionResult> OnGetAsync()
        {
            if (PageIndex < 1) PageIndex = 1;
            Page = await _subjectService.GetPagedActiveSubjectsAsync(Search, PageIndex, pageSize: 12);
            return Page();
        }
    }
}
