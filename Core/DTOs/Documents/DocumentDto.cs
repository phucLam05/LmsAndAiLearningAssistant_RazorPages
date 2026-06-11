using System;
using Core.Entities;

namespace Core.DTOs.Documents
{
    /// <summary>
    /// Data Transfer Object for document metadata.
    /// </summary>
    public class DocumentDto
    {
        public Guid Id { get; set; }

        public Guid? SubjectId { get; set; }

        public Guid? UploadedBy { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string FileUrl { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public DocumentStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public Guid? UpdatedBy { get; set; }

        // Optional readable properties for display
        public string? SubjectCode { get; set; }
        public string? SubjectName { get; set; }
        public string? UploaderName { get; set; }
    }
}
