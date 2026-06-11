namespace Core.Configurations
{
    /// <summary>
    /// Configuration options for document uploads.
    /// </summary>
    public class UploadOptions
    {
        /// <summary>
        /// Maximum allowed file size in bytes. Default is 50MB.
        /// </summary>
        public long MaxFileSize { get; set; } = 50L * 1024L * 1024L;

        /// <summary>
        /// Dictionary mapping allowed file extensions to their expected MIME types.
        /// Extension should include the dot (e.g., ".pdf").
        /// </summary>
        public Dictionary<string, string[]> AllowedMimeTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
