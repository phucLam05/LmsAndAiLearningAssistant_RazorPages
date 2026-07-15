using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using BLL.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PL.Pages.Auth
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly IAuthService _authService;

        public ForgotPasswordModel(IAuthService authService)
        {
            _authService = authService;
        }

        [BindProperty]
        [Required(ErrorMessage = "Email là bắt buộc.")]
        [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ.")]
        [Display(Name = "Địa chỉ Email")]
        public string Email { get; set; } = string.Empty;

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public IActionResult OnGet()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToPage("/Index");
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToPage("/Index");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var result = await _authService.ForgotPasswordAsync(Email);
            if (result.IsSuccess)
            {
                SuccessMessage = "Nếu email này tồn tại trên hệ thống, mật khẩu tạm thời mới đã được gửi tới hòm thư của bạn. Vui lòng kiểm tra hộp thư.";
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Đã xảy ra lỗi khi yêu cầu khôi phục mật khẩu.";
            }

            return Page();
        }
    }
}
