using BLL.Interfaces;
using Core.DTOs.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Threading.Tasks;

namespace PL.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class ChunkingConfigModel : PageModel
    {
        private readonly IChunkingConfigService _configService;

        public ChunkingConfigModel(IChunkingConfigService configService)
        {
            _configService = configService;
        }

        [BindProperty]
        public ChunkingConfigDto Config { get; set; } = new();

        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Config = await _configService.GetConfigAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                ErrorMessage = "Dữ liệu cấu hình không hợp lệ.";
                return Page();
            }

            if (Config.OverlapSize >= Config.ChunkSize)
            {
                ErrorMessage = "Độ chồng lặp (overlap) phải nhỏ hơn kích thước chunk.";
                return Page();
            }

            try
            {
                await _configService.SaveConfigAsync(Config);
                SuccessMessage = "Lưu cấu hình phân mảnh tài liệu thành công!";
            }
            catch (System.Exception ex)
            {
                ErrorMessage = $"Lỗi khi lưu cấu hình: {ex.Message}";
            }

            return Page();
        }
    }
}
