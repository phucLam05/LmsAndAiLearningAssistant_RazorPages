using DAL.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DAL.Providers
{
    /// <summary>
    /// Implementation of Gemini API provider for text generation/chat.
    /// </summary>
    public class GeminiChatProvider : IGeminiChatProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly ILogger<GeminiChatProvider> _logger;

        public GeminiChatProvider(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<GeminiChatProvider> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["GeminiSettings:ApiKey"] ?? string.Empty;
            _baseUrl = configuration["GeminiSettings:BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta/";
        }

        public async Task<(string Answer, int PromptTokens, int CompletionTokens)> GenerateTextAsync(
            string prompt, string model, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new InvalidOperationException("Gemini API key is not configured.");

            var url = $"{_baseUrl.TrimEnd('/')}/models/{model}:generateContent?key={_apiKey}";
            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } }
            };
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Gemini API error: {(int)response.StatusCode} {error}");
            }

            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseString);
            try
            {
                var answer = doc.RootElement.GetProperty("candidates")[0]
                    .GetProperty("content").GetProperty("parts")[0]
                    .GetProperty("text").GetString() ?? "AI không trả về nội dung.";

                int promptTokens = 0;
                int completionTokens = 0;
                if (doc.RootElement.TryGetProperty("usageMetadata", out var usageMeta))
                {
                    if (usageMeta.TryGetProperty("promptTokenCount", out var pElement))
                        promptTokens = pElement.GetInt32();
                    if (usageMeta.TryGetProperty("candidatesTokenCount", out var cElement))
                        completionTokens = cElement.GetInt32();
                }

                return (answer, promptTokens, completionTokens);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse Gemini response. Raw: {Raw}", responseString);
                throw new InvalidOperationException("Không thể phân tích phản hồi từ AI.", ex);
            }
        }
    }
}
