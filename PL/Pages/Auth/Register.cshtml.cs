using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PL.ViewModels.Auth;

namespace PL.Pages.Auth
{
    public class RegisterModel : PageModel
    {
        [BindProperty]
        public RegisterViewModel Input { get; set; } = new();

        // Registration is disabled: admins create accounts through /Admin/Users.
        public IActionResult OnGet() => Page();

        public IActionResult OnPost() => NotFound();
    }
}
