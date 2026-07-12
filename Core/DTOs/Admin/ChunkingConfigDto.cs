using System.ComponentModel.DataAnnotations;

namespace Core.DTOs.Admin
{
    public class ChunkingConfigDto
    {
        [Required(ErrorMessage = "Phương pháp phân mảnh là bắt buộc.")]
        public string Method { get; set; } = "Paragraph"; // "Paragraph", "Word", "Character"

        [Range(1, 10000, ErrorMessage = "Kích thước chunk phải lớn hơn 0.")]
        public int ChunkSize { get; set; } = 500;

        [Range(0, 5000, ErrorMessage = "Độ chồng lặp không được âm.")]
        public int OverlapSize { get; set; } = 50;
    }
}
