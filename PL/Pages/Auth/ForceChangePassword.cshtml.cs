using System.Security.Claims;
using BLL.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PL.Pages.Auth
{
    public class ForceChangePasswordModel : PageModel
    {
        private readonly IUserService _userService;

        public ForceChangePasswordModel(IUserService userService)
        {
            _userService = userService;
        }

        public string? CurrentEmail { get; set; }

        public IActionResult OnGet()
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return RedirectToPage("/Auth/Login");
            }
            CurrentEmail = User.FindFirstValue(ClaimTypes.Email);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string currentPassword, string newPassword, string confirmPassword)
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return RedirectToPage("/Auth/Login");
            }

            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError(string.Empty, "Mật khẩu mới và xác nhận không trùng khớp.");
                CurrentEmail = User.FindFirstValue(ClaimTypes.Email);
                return Page();
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                ModelState.AddModelError(string.Empty, "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.");
                return RedirectToPage("/Auth/Login");
            }

            var result = await _userService.ActivateAndChangePasswordAsync(userId, currentPassword, newPassword);
            if (!result.IsSuccess)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Cập nhật mật khẩu thất bại.");
                CurrentEmail = User.FindFirstValue(ClaimTypes.Email);
                return Page();
            }

            // Sign out the temporary session and redirect to Login
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            TempData["SuccessMessage"] = "Mật khẩu đã được cập nhật thành công. Vui lòng đăng nhập lại với mật khẩu mới.";
            return RedirectToPage("/Auth/Login");
        }
    }
}
