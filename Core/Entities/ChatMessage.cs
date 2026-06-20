using System;

namespace Core.Entities
{
    public class ChatMessage
    {
        public Guid Id { get; set; }
        public Guid SessionId { get; set; }
        public string Role { get; set; } = "user"; // user | assistant | system
        public string Content { get; set; } = string.Empty;
        public string? SourcesJson { get; set; }
        public int? PromptTokens { get; set; }
        public int? CompletionTokens { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ChatSession? Session { get; set; }
    }
}
