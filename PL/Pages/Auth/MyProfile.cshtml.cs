using System.Security.Claims;
using DAL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PL.Pages.Auth
{
    [Authorize]
    public class MyProfileModel : PageModel
    {
        private readonly IUserRepository _userRepository;

        public MyProfileModel(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public string UserCode { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = GetUserId();
            if (userId == Guid.Empty) return RedirectToPage("/Auth/Login");

            var user = await _userRepository.GetByIdAsync(userId);
            UserCode = user?.UserCode ?? "N/A";
            return Page();
        }

        public async Task<IActionResult> OnPostChangePasswordAsync(
            string currentPassword, string newPassword, string confirmPassword)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty) return RedirectToPage("/Auth/Login");

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            {
                ModelState.AddModelError(string.Empty, "Mật khẩu mới phải có ít nhất 6 ký tự.");
                return await ReloadPage(userId);
            }

            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError(string.Empty, "Mật khẩu mới và xác nhận không khớp.");
                return await ReloadPage(userId);
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return RedirectToPage("/Auth/Login");

            if (string.IsNullOrEmpty(currentPassword) || !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Mật khẩu hiện tại không chính xác.");
                return await ReloadPage(userId);
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);

            TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
            return RedirectToPage();
        }

        private async Task<IActionResult> ReloadPage(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            UserCode = user?.UserCode ?? "N/A";
            return Page();
        }

        private Guid GetUserId()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(idStr, out var g) ? g : Guid.Empty;
        }
    }
}
