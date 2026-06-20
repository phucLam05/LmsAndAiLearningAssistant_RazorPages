using System;
using System.Collections.Generic;
using Core.DTOs.Subject;

namespace Core.DTOs.Chat
{
    public class ChatSessionDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid? SubjectId { get; set; }
        public string? SubjectName { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int MessageCount { get; set; }
        public string? Preview { get; set; }
    }

    public class ChatMessageDto
    {
        public Guid Id { get; set; }
        public Guid SessionId { get; set; }
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
        public List<ChatSourceDto> Sources { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }
}
