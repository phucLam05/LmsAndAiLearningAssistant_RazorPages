using System;
using Pgvector;

namespace Core.Entities
{
    public class DocumentChunk
    {
        public Guid Id { get; set; }
        public Guid? DocumentId { get; set; }
        public Guid? SubjectId { get; set; }
        public int ChunkIndex { get; set; }
        public string Content { get; set; } = string.Empty;
        public int TokenCount { get; set; }
        public int? PageNumber { get; set; }
        public Vector? Embedding { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Document? Document { get; set; }
        public Subject? Subject { get; set; }
    }
}
