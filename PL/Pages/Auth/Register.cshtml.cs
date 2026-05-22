using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BLL.Interfaces;
using Core.DTOs.Auth;
using System.Threading.Tasks;

namespace PL.Pages.Auth
{
    public class RegisterModel : PageModel
    {
        private readonly IAuthService _authService;

        public RegisterModel(IAuthService authService)
        {
            _authService = authService;
        }

        [BindProperty]
        public RegisterDTO Input { get; set; } = new();

        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var (success, error) = await _authService.RegisterAsync(Input);

            if (!success)
            {
                ErrorMessage = error;
                return Page();
            }

            SuccessMessage = "Registration successful. You can now login.";
            return RedirectToPage("/Auth/Login");
        }
    }
}
