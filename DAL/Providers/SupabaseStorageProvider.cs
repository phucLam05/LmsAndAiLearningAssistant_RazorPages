using Core.Configurations;
using DAL.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DAL.Providers
{
    /// <summary>
    /// Uploads and deletes private files in Supabase Storage by using the backend-only service role key.
    /// </summary>
    public class SupabaseStorageProvider : ISupabaseStorageProvider
    {
        private readonly HttpClient _httpClient;
        private readonly SupabaseOptions _options;

        /// <summary>
        /// Creates a storage service and loads Supabase URL, service role key, and bucket name from configuration.
        /// </summary>
        public SupabaseStorageProvider(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;

            var section = configuration.GetSection("Supabase");
            _options = new SupabaseOptions
            {
                Url = section["Url"] ?? string.Empty,
                ServiceRoleKey = section["ServiceRoleKey"] ?? string.Empty,
                Bucket = string.IsNullOrWhiteSpace(section["Bucket"]) ? "documents" : section["Bucket"]!
            };
        }

        /// <summary>
        /// Uploads a stream to a private Supabase Storage object path without overwriting existing files.
        /// </summary>
        public async Task UploadAsync(string storagePath, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();

            using var request = new HttpRequestMessage(HttpMethod.Post, BuildObjectUri(storagePath));
            AddAuthHeaders(request);
            request.Headers.TryAddWithoutValidation("x-upsert", "false");
            request.Content = new StreamContent(content);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(NormalizeContentType(contentType));

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Supabase upload failed: {(int)response.StatusCode} {body}");
            }
        }

        /// <summary>
        /// Downloads a private Supabase Storage object into a memory stream.
        /// </summary>
        public async Task<Stream> DownloadAsync(string storagePath, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();

            using var request = new HttpRequestMessage(HttpMethod.Get, BuildObjectUri(storagePath));
            AddAuthHeaders(request);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Supabase download failed: {(int)response.StatusCode} {body}");
            }

            var ms = new MemoryStream();
            await response.Content.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;
            return ms;
        }

        /// <summary>
        /// Deletes a private Supabase Storage object by path. Missing objects are treated as storage API errors.
        /// </summary>
        public async Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();

            using var request = new HttpRequestMessage(HttpMethod.Delete, BuildBucketUri());
            AddAuthHeaders(request);
            request.Content = new StringContent(
                JsonSerializer.Serialize(new { prefixes = new[] { NormalizeStoragePath(storagePath) } }),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Supabase delete failed: {(int)response.StatusCode} {body}");
            }
        }

        /// <summary>
        /// Builds the Supabase upload URL and tolerates config values with or without /rest/v1 or /storage/v1 suffixes.
        /// </summary>
        private Uri BuildObjectUri(string storagePath)
        {
            var encodedPath = string.Join("/", NormalizeStoragePath(storagePath).Split('/').Select(Uri.EscapeDataString));
            return new Uri($"{GetSupabaseOrigin()}/storage/v1/object/{Uri.EscapeDataString(NormalizeBucket(_options.Bucket))}/{encodedPath}");
        }

        /// <summary>
        /// Builds the Supabase delete URL for the configured bucket.
        /// </summary>
        private Uri BuildBucketUri()
        {
            return new Uri($"{GetSupabaseOrigin()}/storage/v1/object/{Uri.EscapeDataString(NormalizeBucket(_options.Bucket))}");
        }

        /// <summary>
        /// Adds service-role authorization headers required by Supabase Storage private buckets.
        /// </summary>
        private void AddAuthHeaders(HttpRequestMessage request)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ServiceRoleKey);
            request.Headers.TryAddWithoutValidation("apikey", _options.ServiceRoleKey);
        }

        /// <summary>
        /// Validates required Supabase settings before making HTTP calls.
        /// </summary>
        private void EnsureConfigured()
        {
            if (string.IsNullOrWhiteSpace(_options.Url) ||
                string.IsNullOrWhiteSpace(_options.ServiceRoleKey) ||
                string.IsNullOrWhiteSpace(_options.Bucket))
            {
                throw new InvalidOperationException("Supabase storage is not configured.");
            }
        }

        /// <summary>
        /// Converts any Supabase API URL to only scheme and host, preventing duplicated path segments.
        /// </summary>
        private string GetSupabaseOrigin()
        {
            var rawUrl = _options.Url.Trim().TrimEnd('/');
            if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
            {
                throw new InvalidOperationException("Supabase URL is invalid.");
            }

            return uri.GetLeftPart(UriPartial.Authority);
        }

        /// <summary>
        /// Ensures object paths never start with a slash, which Supabase rejects as an invalid storage path.
        /// </summary>
        private static string NormalizeStoragePath(string storagePath)
        {
            return storagePath.Replace('\\', '/').Trim('/');
        }

        /// <summary>
        /// Ensures the bucket name has no leading or trailing slashes.
        /// </summary>
        private static string NormalizeBucket(string bucket)
        {
            return bucket.Trim().Trim('/');
        }

        /// <summary>
        /// Falls back to application/octet-stream for archive and unknown browser MIME values.
        /// </summary>
        private static string NormalizeContentType(string contentType)
        {
            return string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
        }

        /// <summary>
        /// Generates a signed download URL for a private storage object path.
        /// </summary>
        public async Task<string> GetSignedUrlAsync(string storagePath, int expiresInSeconds = 3600, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();

            var encodedPath = string.Join("/", NormalizeStoragePath(storagePath).Split('/').Select(Uri.EscapeDataString));
            var signUri = new Uri($"{GetSupabaseOrigin()}/storage/v1/object/sign/{Uri.EscapeDataString(NormalizeBucket(_options.Bucket))}/{encodedPath}");

            using var request = new HttpRequestMessage(HttpMethod.Post, signUri);
            AddAuthHeaders(request);
            request.Content = new StringContent(
                JsonSerializer.Serialize(new { expiresIn = expiresInSeconds }),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Supabase sign URL failed: {(int)response.StatusCode} {body}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseBody);
            
            if (doc.RootElement.TryGetProperty("signedURL", out var signedUrlProperty) || doc.RootElement.TryGetProperty("signedUrl", out signedUrlProperty))
            {
                var relativeUrl = signedUrlProperty.GetString() ?? string.Empty;
                if (relativeUrl.StartsWith("/"))
                {
                    return GetSupabaseOrigin() + relativeUrl;
                }
                return relativeUrl;
            }
            throw new InvalidOperationException("Failed to parse signed URL from Supabase response.");
        }
    }
}
