using System;
using System.Collections.Generic;
using Core.DTOs.Subject;

namespace Core.DTOs.Chat
{
    public class ChatSessionDetailDto
    {
        public Guid Id { get; set; }
        public Guid? SubjectId { get; set; }
        public string? SubjectName { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<ChatMessageDto> Messages { get; set; } = new();
    }
}
