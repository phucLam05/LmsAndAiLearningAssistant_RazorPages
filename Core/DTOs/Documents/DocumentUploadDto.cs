using System;
using System.IO;

namespace Core.DTOs.Documents
{
    /// <summary>
    /// Carries upload request data from the MVC layer to the business layer.
    /// </summary>
    public class DocumentUploadDto
    {
        public Guid UploadedBy { get; set; }

        public Guid SubjectId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public Stream Content { get; set; } = Stream.Null;
    }
}
