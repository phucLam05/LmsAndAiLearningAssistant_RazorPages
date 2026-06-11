using System;

namespace Core.DTOs.Documents
{
    /// <summary>
    /// Data Transfer Object for a document chunk.
    /// </summary>
    public class DocumentChunkDto
    {
        /// <summary>
        /// Unique identifier for the chunk.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Identifier of the parent document.
        /// </summary>
        public Guid DocumentId { get; set; }

        /// <summary>
        /// Zero-based index of the chunk within the document.
        /// </summary>
        public int ChunkIndex { get; set; }

        /// <summary>
        /// Text content of the chunk.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Token count of the chunk content.
        /// </summary>
        public int TokenCount { get; set; }

        /// <summary>
        /// Optional page number the chunk originated from.
        /// </summary>
        public int? PageNumber { get; set; }

        /// <summary>
        /// Embedding vector for similarity search.
        /// </summary>
        public float[] Embedding { get; set; } = Array.Empty<float>();

        /// <summary>
        /// Date and time when the chunk was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}
