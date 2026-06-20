using System.Security.Claims;
using System.Text.RegularExpressions;
using BLL.Interfaces;
using Core.DTOs.Admin;
using Core.DTOs.Common;
using Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PL.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class UsersModel : PageModel
    {
        // Lecturer codes are typically GV + 4-6 digits (e.g. GV12345).
        private static readonly Regex LecturerCodeRegex = new(@"^GV\d{4,6}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly IAdminService _adminService;
        private readonly IUserService _userService;
        private readonly IAuthService _authService;

        public UsersModel(IAdminService adminService, IUserService userService, IAuthService authService)
        {
            _adminService = adminService;
            _userService = userService;
            _authService = authService;
        }

        public PagedResult<MockUser> Page { get; set; } = PagedResult<MockUser>.Empty();

        public class MockUser
        {
            public Guid Id { get; set; }
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Role { get; set; } = "Student";
            public string StudentCode { get; set; } = string.Empty;
            public string Status { get; set; } = "Active";
            public DateTime CreatedAt { get; set; }
        }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Role { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageIndex { get; set; } = 1;

        public string? CurrentAdminFullName { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            CurrentAdminFullName = User.FindFirstValue(ClaimTypes.Name);
            if (PageIndex < 1) PageIndex = 1;
            const int pageSize = 15;

            var paged = await _adminService.GetPagedUsersAsync(Search, Role, PageIndex, pageSize);
            var users = new List<MockUser>();
            foreach (var u in paged.Items)
            {
                string email = u.EmailHash ?? "N/A";
                if (!string.IsNullOrEmpty(u.EmailEncrypt))
                {
                    try { email = _authService.DecryptEmail(u.EmailEncrypt); }
                    catch { /* keep emailHash fallback */ }
                }
                users.Add(new MockUser
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = email,
                    Role = u.Role.ToString(),
                    StudentCode = u.UserCode ?? string.Empty,
                    Status = u.Status.ToString(),
                    CreatedAt = u.CreatedAt
                });
            }
            Page = new PagedResult<MockUser>
            {
                Items = users,
                TotalCount = paged.TotalCount,
                PageIndex = paged.PageIndex,
                PageSize = paged.PageSize
            };
            return Page();
        }

        public async Task<IActionResult> OnPostCreateUserAsync(string fullName, string email, string role, string? studentCode, bool mustChangePassword = false)
        {
            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Há» tÃªn vÃ  Email lÃ  báº¯t buá»™c.";
                return RedirectToPage(new { Search, Role, PageIndex });
            }
            var userRole = Enum.TryParse<UserRole>(role, true, out var parsed) ? parsed : UserRole.Student;

            if (userRole == UserRole.Lecturer)
            {
                if (string.IsNullOrWhiteSpace(studentCode) || !LecturerCodeRegex.IsMatch(studentCode.Trim()))
                {
                    TempData["ErrorMessage"] = "MÃ£ giáº£ng viÃªn lÃ  báº¯t buá»™c vÃ  pháº£i cÃ³ dáº¡ng GV + 4-6 chá»¯ sá»‘ (VD: GV12345).";
                    return RedirectToPage(new { Search, Role, PageIndex });
                }
            }
            else if (string.IsNullOrWhiteSpace(studentCode))
            {
                studentCode = "STU" + new Random().Next(100000, 999999).ToString();
            }

            var dto = new UserCreateDto
            {
                Email = email,
                FullName = fullName,
                Role = userRole,
                UserCode = studentCode!,
                MustChangePassword = mustChangePassword
            };
            var result = await _userService.CreateUserAsync(dto);
            if (!result.IsSuccess) TempData["ErrorMessage"] = result.ErrorMessage;
            else TempData["SuccessMessage"] = $"ÄÃ£ táº¡o tÃ i khoáº£n {fullName}. Email thÃ´ng bÃ¡o Ä‘Ã£ Ä‘Æ°á»£c gá»­i Ä‘áº¿n {email}.";
            return RedirectToPage(new { Search, Role, PageIndex });
        }

        public async Task<IActionResult> OnPostEditUserAsync(Guid id, string fullName, string email, string role, string status)
        {
            var userRole = Enum.TryParse<UserRole>(role, true, out var parsedRole) ? parsedRole : UserRole.Student;
            var userStatus = Enum.TryParse<UserStatus>(status, true, out var parsedStatus) ? parsedStatus : UserStatus.Active;
            var dto = new UserEditDto { Id = id, FullName = fullName, Role = userRole, Status = userStatus };
            var result = await _userService.UpdateUserAsync(dto);
            if (!result.IsSuccess) TempData["ErrorMessage"] = result.ErrorMessage;
            else TempData["SuccessMessage"] = $"ÄÃ£ cáº­p nháº­t tÃ i khoáº£n {fullName}.";
            return RedirectToPage(new { Search, Role, PageIndex });
        }

        public async Task<IActionResult> OnPostResetPasswordAsync(Guid id, string newPassword)
        {
            var result = await _userService.ResetPasswordAsync(id, newPassword);
            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
            }
            else
            {
                TempData["SuccessMessage"] = "ÄÃ£ Ä‘áº·t láº¡i máº­t kháº©u vÃ  kÃ­ch hoáº¡t tÃ i khoáº£n. Email thÃ´ng bÃ¡o Ä‘Ã£ Ä‘Æ°á»£c gá»­i cho ngÆ°á»i dÃ¹ng.";
            }
            return RedirectToPage(new { Search, Role, PageIndex });
        }

        public async Task<IActionResult> OnPostDeleteUserAsync(Guid id)
        {
            var result = await _userService.DeleteUserAsync(id);
            if (result.IsSuccess) TempData["SuccessMessage"] = "ÄÃ£ xÃ³a tÃ i khoáº£n.";
            else TempData["ErrorMessage"] = result.ErrorMessage;
            return RedirectToPage(new { Search, Role, PageIndex });
        }

        public class ImportRow
        {
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string StudentCode { get; set; } = string.Empty;
        }

        public async Task<JsonResult> OnPostBulkImportAsync([FromBody] List<ImportRow> rows)
        {
            if (rows == null || !rows.Any())
                return new JsonResult(new { success = false, message = "KhÃ´ng nháº­n Ä‘Æ°á»£c dá»¯ liá»‡u." });

            int successCount = 0, failCount = 0;
            var results = new List<object>();
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Email) || string.IsNullOrWhiteSpace(row.FullName))
                {
                    failCount++;
                    results.Add(new { name = row.FullName, email = row.Email, status = "Failed", reason = "Thiáº¿u há» tÃªn hoáº·c email" });
                    continue;
                }
                if (!row.Email.Contains("@"))
                {
                    failCount++;
                    results.Add(new { name = row.FullName, email = row.Email, status = "Failed", reason = "Email khÃ´ng Ä‘Ãºng Ä‘á»‹nh dáº¡ng" });
                    continue;
                }
                var code = string.IsNullOrWhiteSpace(row.StudentCode) ? "STU" + new Random().Next(100000, 999999).ToString() : row.StudentCode;
                var dto = new UserCreateDto { Email = row.Email, FullName = row.FullName, Role = UserRole.Student, UserCode = code, MustChangePassword = true };
                var result = await _userService.CreateUserAsync(dto);
                if (result.IsSuccess)
                {
                    successCount++;
                    results.Add(new { name = row.FullName, email = row.Email, status = "Success", studentCode = code });
                }
                else
                {
                    failCount++;
                    results.Add(new { name = row.FullName, email = row.Email, status = "Failed", reason = result.ErrorMessage });
                }
            }
            return new JsonResult(new { success = true, successCount, failCount, results });
        }
    }
}
