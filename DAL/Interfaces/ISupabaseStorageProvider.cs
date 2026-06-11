using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    /// <summary>
    /// Abstracts Supabase Storage operations so document business logic is not tied directly to HTTP calls.
    /// </summary>
    public interface ISupabaseStorageProvider
    {
        Task UploadAsync(string storagePath, Stream content, string contentType, CancellationToken cancellationToken = default);
        
        Task<Stream> DownloadAsync(string storagePath, CancellationToken cancellationToken = default);

        Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);

        Task<string> GetSignedUrlAsync(string storagePath, int expiresInSeconds = 3600, CancellationToken cancellationToken = default);
    }
}
