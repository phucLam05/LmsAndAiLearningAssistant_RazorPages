using System.Security.Claims;
using BLL.Interfaces;
using Core.DTOs.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PL.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly IAdminService _adminService;

        public IndexModel(IAdminService adminService)
        {
            _adminService = adminService;
        }

        public DashboardStatsDto Stats { get; set; } = new();

        public string CurrentAdminFullName { get; set; } = string.Empty;
        public string CurrentAdminRole { get; set; } = string.Empty;
        public string? CurrentAdminEmail { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            CurrentAdminFullName = User.FindFirstValue(ClaimTypes.Name) ?? "Quáº£n trá»‹ viÃªn";
            CurrentAdminRole = User.FindFirstValue(ClaimTypes.Role) ?? "Admin";
            CurrentAdminEmail = User.FindFirstValue(ClaimTypes.Email);
            Stats = await _adminService.GetDashboardStatsAsync();
            return Page();
        }
    }
}
