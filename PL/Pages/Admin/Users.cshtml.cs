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
        // Lecturer codes: LEC + 3 digits (e.g. LEC001).
        private static readonly Regex LecturerCodeRegex = new(@"^LEC\d{3}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
                TempData["ErrorMessage"] = "Họ tên và Email là bắt buộc.";
                return RedirectToPage(new { Search, Role, PageIndex });
            }
            var userRole = Enum.TryParse<UserRole>(role, true, out var parsed) ? parsed : UserRole.Student;

            if (userRole == UserRole.Lecturer)
            {
                if (string.IsNullOrWhiteSpace(studentCode) || !LecturerCodeRegex.IsMatch(studentCode.Trim()))
                {
                    TempData["ErrorMessage"] = "Mã giảng viên là bắt buộc và phải có dạng LEC + 3 chữ số (VD: LEC001).";
                    return RedirectToPage(new { Search, Role, PageIndex });
                }
            }
            else if (string.IsNullOrWhiteSpace(studentCode))
            {
                // Auto-generate: HE + 6 digits
                studentCode = "HE" + new Random().Next(100000, 999999).ToString();
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
            else TempData["SuccessMessage"] = $"Đã tạo tài khoản {fullName}. Email thông báo đã được gửi đến {email}.";
            return RedirectToPage(new { Search, Role, PageIndex });
        }

        public async Task<IActionResult> OnPostEditUserAsync(Guid id, string fullName, string email, string role, string status, string? userCode = null)
        {
            var userRole = Enum.TryParse<UserRole>(role, true, out var parsedRole) ? parsedRole : UserRole.Student;
            var userStatus = Enum.TryParse<UserStatus>(status, true, out var parsedStatus) ? parsedStatus : UserStatus.Active;
            var dto = new UserEditDto { Id = id, FullName = fullName, Role = userRole, Status = userStatus, UserCode = userCode };
            var result = await _userService.UpdateUserAsync(dto);
            if (!result.IsSuccess) TempData["ErrorMessage"] = result.ErrorMessage;
            else TempData["SuccessMessage"] = $"Đã cập nhật tài khoản {fullName}.";
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
                TempData["SuccessMessage"] = "Đã đặt lại mật khẩu và kích hoạt tài khoản. Email thông báo đã được gửi cho người dùng.";
            }
            return RedirectToPage(new { Search, Role, PageIndex });
        }

        public async Task<IActionResult> OnPostDeleteUserAsync(Guid id)
        {
            var result = await _userService.DeleteUserAsync(id);
            if (result.IsSuccess) TempData["SuccessMessage"] = "Đã xóa tài khoản.";
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
                return new JsonResult(new { success = false, message = "Không nhận được dữ liệu." });

            int successCount = 0, failCount = 0;
            var results = new List<object>();
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Email) || string.IsNullOrWhiteSpace(row.FullName))
                {
                    failCount++;
                    results.Add(new { name = row.FullName, email = row.Email, status = "Failed", reason = "Thiếu họ tên hoặc email" });
                    continue;
                }
                if (!row.Email.Contains("@"))
                {
                    failCount++;
                    results.Add(new { name = row.FullName, email = row.Email, status = "Failed", reason = "Email không đúng định dạng" });
                    continue;
                }
                // Auto-generate: HE + 6 digits
                var code = string.IsNullOrWhiteSpace(row.StudentCode) ? "HE" + new Random().Next(100000, 999999).ToString() : row.StudentCode;
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
