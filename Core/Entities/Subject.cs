using System;
using System.Collections.Generic;

namespace Core.Entities
{
    public class Subject
    {
        public Guid Id { get; set; }
        public string SubjectCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid? LecturerId { get; set; }
        public SubjectStatus Status { get; set; } = SubjectStatus.Active;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public Guid? UpdatedBy { get; set; }

        public User? Lecturer { get; set; }
        public User? Updater { get; set; }
        public ICollection<Document> Documents { get; set; } = new List<Document>();
        public ICollection<DocumentChunk> DocumentChunks { get; set; } = new List<DocumentChunk>();
    }
}
