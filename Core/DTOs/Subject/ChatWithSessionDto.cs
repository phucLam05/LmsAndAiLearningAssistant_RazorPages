using System;
using System.Collections.Generic;

namespace Core.DTOs.Subject
{
    public class ChatWithSessionDto
    {
        public Guid SessionId { get; set; }
        public ChatResponseDto Response { get; set; } = new();
    }
}
