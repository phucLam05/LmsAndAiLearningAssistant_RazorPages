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

            // Re-issue cookie with a normal 7-day expiry now that the account is activated.
            var role = User.FindFirstValue(ClaimTypes.Role) ?? user.Role.ToString();
            var email = User.FindFirstValue(ClaimTypes.Email);
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.FullName),
                new(ClaimTypes.Email, email ?? string.Empty),
                new(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var props = new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7) };
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), props);

            TempData["SuccessMessage"] = "Mật khẩu đã được cập nhật. Chào mừng bạn đến với LMS AI!";
            return role switch
            {
                "Admin" => RedirectToPage("/Admin/Index"),
                "Lecturer" => RedirectToPage("/Subject/MySubjects"),
                "Student" => RedirectToPage("/Subject/Browse"),
                _ => RedirectToPage("/Index")
            };
        }
    }
}
