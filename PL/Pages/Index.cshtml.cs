using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PL.Pages
{
    public class IndexModel : PageModel
    {
        public IActionResult OnGet()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var role = User.FindFirstValue(ClaimTypes.Role);
                return role switch
                {
                    "Admin" => RedirectToPage("/Admin/Index"),
                    "Lecturer" => RedirectToPage("/Subject/MySubjects"),
                    "Student" => RedirectToPage("/Subject/Browse"),
                    _ => RedirectToPage("/Subject/Index")
                };
            }
            return RedirectToPage("/Auth/Login");
        }
    }
}
