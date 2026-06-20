using System;
using System.Collections.Generic;

namespace Core.DTOs.Subject
{
    public class ChatResponseDto
    {
        public string Answer { get; set; } = string.Empty;
        public List<ChatSourceDto> Sources { get; set; } = new();
    }

    public class ChatSourceDto
    {
        public int Index { get; set; }
        public Guid DocumentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public int? PageNumber { get; set; }
    }
}
