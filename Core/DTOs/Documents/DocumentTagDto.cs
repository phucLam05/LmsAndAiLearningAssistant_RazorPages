using System;

namespace Core.DTOs.Documents
{
    /// <summary>
    /// Data Transfer Object for document tag details.
    /// </summary>
    public class DocumentTagDto
    {
        /// <summary>
        /// Unique identifier for the tag.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Identifier of the user who owns the tag.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Display name of the tag.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional color value for UI display.
        /// </summary>
        public string? Color { get; set; }

        /// <summary>
        /// Date and time when the tag was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}
