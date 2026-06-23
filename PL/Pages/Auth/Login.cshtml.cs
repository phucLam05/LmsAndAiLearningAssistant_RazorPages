using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BLL.Interfaces;
using Core.DTOs.Auth;
using Core.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PL.ViewModels.Auth;

namespace PL.Pages.Auth
{
    public class LoginModel : PageModel
    {
        private readonly IAuthService _authService;

        public LoginModel(IAuthService authService)
        {
            _authService = authService;
        }

        [BindProperty]
        public LoginViewModel Input { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public IActionResult OnGet()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectByRole();
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectByRole(User.FindFirstValue(ClaimTypes.Role));
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var loginDto = new LoginDto { Email = Input.Email, Password = Input.Password };
            var user = await _authService.LoginAsync(loginDto);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Sai thông tin đăng nhập. Vui lòng kiểm tra lại.");
                return Page();
            }

            if (user.Status == UserStatus.Inactive)
            {
                // Keep the user signed in (so /Auth/ForceChangePassword can read User.Identity) but
                // route them to the mandatory password change flow.
                var tempClaims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new(ClaimTypes.Name, user.FullName),
                    new(ClaimTypes.Email, Input.Email),
                    new(ClaimTypes.Role, user.Role.ToString())
                };
                var tempIdentity = new ClaimsIdentity(tempClaims, CookieAuthenticationDefaults.AuthenticationScheme);
                var tempProps = new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2) };
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(tempIdentity), tempProps);
                return RedirectToPage("/Auth/ForceChangePassword");
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.FullName),
                new(ClaimTypes.Email, Input.Email),
                new(ClaimTypes.Role, user.Role.ToString())
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var props = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            };
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), props);

            if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            {
                return Redirect(ReturnUrl);
            }

            return RedirectByRole(user.Role.ToString());
        }

        private IActionResult RedirectByRole(string? role = null)
        {
            role ??= User.FindFirstValue(ClaimTypes.Role);
            return role switch
            {
                "Admin" => RedirectToPage("/Admin/Index"),
                "Lecturer" => RedirectToPage("/Subject/MySubjects"),
                "Student" => RedirectToPage("/Subject/Browse"),
                _ => RedirectToPage("/Subject/Index")
            };
        }
    }
}
