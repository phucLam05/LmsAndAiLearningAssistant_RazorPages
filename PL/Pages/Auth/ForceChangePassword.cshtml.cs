using System.Security.Claims;
using BLL.Interfaces;
using Core.Entities;
using DAL.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PL.Pages.Auth
{
    public class ForceChangePasswordModel : PageModel
    {
        private readonly IUserRepository _userRepository;

        public ForceChangePasswordModel(IUserRepository userRepository)
        {
            _userRepository = userRepository;
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

            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
            {
                ModelState.AddModelError(string.Empty, "Mật khẩu mới phải có ít nhất 6 ký tự.");
                CurrentEmail = User.FindFirstValue(ClaimTypes.Email);
                return Page();
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

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Không tìm thấy tài khoản.");
                return RedirectToPage("/Auth/Login");
            }

            if (!string.IsNullOrEmpty(currentPassword) && !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Mật khẩu tạm thời không chính xác.");
                CurrentEmail = User.FindFirstValue(ClaimTypes.Email);
                return Page();
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.Status = UserStatus.Active;
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);

            // Sign out the temporary session and redirect to Login
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            TempData["SuccessMessage"] = "Mật khẩu đã được cập nhật thành công. Vui lòng đăng nhập lại với mật khẩu mới.";
            return RedirectToPage("/Auth/Login");
        }
    }
}
