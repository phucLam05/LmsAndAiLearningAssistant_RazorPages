using System.Security.Claims;
using BLL.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PL.Pages.Auth
{
    [Authorize]
    public class MyProfileModel : PageModel
    {
        private readonly IUserService _userService;

        public MyProfileModel(IUserService userService)
        {
            _userService = userService;
        }

        public string UserCode { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = GetUserId();
            if (userId == Guid.Empty) return RedirectToPage("/Auth/Login");

            var result = await _userService.GetUserCodeAsync(userId);
            UserCode = result.IsSuccess ? result.Data ?? "N/A" : "N/A";
            return Page();
        }

        public async Task<IActionResult> OnPostChangePasswordAsync(
            string currentPassword, string newPassword, string confirmPassword)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty) return RedirectToPage("/Auth/Login");

            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError(string.Empty, "Mật khẩu mới và xác nhận không khớp.");
                return await ReloadPage(userId);
            }

            var result = await _userService.ChangePasswordAsync(userId, currentPassword, newPassword);
            if (!result.IsSuccess)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Đổi mật khẩu thất bại.");
                return await ReloadPage(userId);
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            TempData["SuccessMessage"] = "Đổi mật khẩu thành công! Vui lòng đăng nhập lại với mật khẩu mới.";
            return RedirectToPage("/Auth/Login");
        }

        private async Task<IActionResult> ReloadPage(Guid userId)
        {
            var result = await _userService.GetUserCodeAsync(userId);
            UserCode = result.IsSuccess ? result.Data ?? "N/A" : "N/A";
            return Page();
        }

        private Guid GetUserId()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(idStr, out var g) ? g : Guid.Empty;
        }
    }
}
