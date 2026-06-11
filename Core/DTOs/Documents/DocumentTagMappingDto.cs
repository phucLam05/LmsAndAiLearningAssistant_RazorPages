using System;

namespace Core.DTOs.Documents
{
    /// <summary>
    /// Data Transfer Object for mapping a document to a tag.
    /// </summary>
    public class DocumentTagMappingDto
    {
        /// <summary>
        /// Identifier of the document.
        /// </summary>
        public Guid DocumentId { get; set; }

        /// <summary>
        /// Identifier of the tag.
        /// </summary>
        public Guid TagId { get; set; }
    }
}
