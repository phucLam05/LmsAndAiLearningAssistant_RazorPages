using BLL.Interfaces;
using Core.DTOs.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace PL.Pages.Auth
{
    public class ChangePasswordModel : PageModel
    {
        private readonly IAuthService _authService;

        public ChangePasswordModel(IAuthService authService)
        {
            _authService = authService;
        }

        [BindProperty(SupportsGet = true)]
        public Guid UserId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Email { get; set; }

        [BindProperty]
        public FirstTimeChangePasswordDto Input { get; set; } = new();

        public IActionResult OnGet()
        {
            if (UserId == Guid.Empty || string.IsNullOrEmpty(Email))
            {
                return RedirectToPage("/Auth/Login");
            }
            Input.UserId = UserId;
            return Page();
        }

        [EnableRateLimiting("change-password")]
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var result = await _authService.ActivateAccountAsync(Input.UserId, Input.TemporaryPassword, Input.NewPassword);
            if (!result.IsSuccess)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage);
                return Page();
            }

            TempData["SuccessMessage"] = "Account activated and password changed successfully. Please log in with your new password.";
            return RedirectToPage("/Auth/Login");
        }
    }
}
