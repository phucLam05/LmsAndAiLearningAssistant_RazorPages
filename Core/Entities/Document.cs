using System;
using System.Collections.Generic;

namespace Core.Entities
{
    public class Document
    {
        public Guid Id { get; set; }
        public Guid? SubjectId { get; set; }
        public Guid? UploadedBy { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public long FileSize { get; set; } = 0;
        public DocumentStatus Status { get; set; } = DocumentStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public Guid? UpdatedBy { get; set; }

        public Subject? Subject { get; set; }
        public User? Uploader { get; set; }
        public User? Updater { get; set; }
        public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
    }
}
