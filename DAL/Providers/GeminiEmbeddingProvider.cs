using DAL.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace DAL.Providers
{
    /// <summary>
    /// Implementation of Gemini API provider for embeddings.
    /// </summary>
    public class GeminiEmbeddingProvider : IGeminiEmbeddingProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly string _baseUrl;

        public GeminiEmbeddingProvider(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["GeminiSettings:ApiKey"] ?? string.Empty;
            _model = configuration["GeminiSettings:Model"] ?? "models/gemini-embedding-001";
            _baseUrl = configuration["GeminiSettings:BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta/";
        }

        public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new InvalidOperationException("Gemini API Key is not configured.");
            }

            // Determine endpoint dynamically
            var modelId = _model.StartsWith("models/") ? _model.Substring(7) : _model;
            var url = $"{_baseUrl.TrimEnd('/')}/models/{modelId}:embedContent?key={_apiKey}";

            var requestBody = new
            {
                model = _model,
                content = new { parts = new[] { new { text = text } } }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Gemini API error: {(int)response.StatusCode} {errorBody}");
            }

            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseString);

            var values = doc.RootElement.GetProperty("embedding").GetProperty("values");
            return JsonSerializer.Deserialize<float[]>(values.GetRawText()) ?? Array.Empty<float>();
        }
    }
}
