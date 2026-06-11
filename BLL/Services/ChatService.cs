using BLL.Interfaces;
using Core.Entities;
using DAL.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pgvector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BLL.Services
{
    /// <summary>
    /// Service that coordinates RAG Chatbot operations using vector similarity search in pgvector and Gemini API for generation.
    /// </summary>
    public class ChatService : IChatService
    {
        private readonly IDocumentChunkRepository _documentChunkRepository;
        private readonly IGeminiEmbeddingProvider _embeddingProvider;
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            IDocumentChunkRepository documentChunkRepository,
            IGeminiEmbeddingProvider embeddingProvider,
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<ChatService> logger)
        {
            _documentChunkRepository = documentChunkRepository;
            _embeddingProvider = embeddingProvider;
            _httpClient = httpClient;
            _apiKey = configuration["GeminiSettings:ApiKey"] ?? string.Empty;
            _baseUrl = configuration["GeminiSettings:BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta/";
            _logger = logger;
        }

        // Supported Gemini generation models – verified via ListModels API (v1beta, generateContent).
        private static readonly HashSet<string> AllowedModels = new(StringComparer.OrdinalIgnoreCase)
        {
            "gemini-2.5-flash",       // Gemini 2.5 Flash      – 5 RPM, 20 RPD
            "gemini-2.5-flash-lite",  // Gemini 2.5 Flash Lite – 10 RPM, 20 RPD
            "gemini-2.0-flash",       // Gemini 2.0 Flash      – stable
            "gemini-3.1-flash-lite",  // Gemini 3.1 Flash Lite – 15 RPM, 500 RPD
            "gemini-3.5-flash",       // Gemini 3.5 Flash      – 5 RPM, 20 RPD
        };
        private const string DefaultModel = "gemini-2.5-flash";

        public async Task<string> ChatWithSubjectAsync(Guid subjectId, string query, string? model = null, List<Guid>? documentIds = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return "Please enter a question.";
            }

            try
            {
                _logger.LogInformation("Generating embedding for student query: {Query}", query);
                var queryEmbedding = await _embeddingProvider.GetEmbeddingAsync(query, cancellationToken);

                _logger.LogInformation("Performing similarity search for SubjectId: {SubjectId}", subjectId);

                var matchedChunks = await _documentChunkRepository.SearchSimilarChunksAsync(
                    subjectId,
                    new Vector(queryEmbedding),
                    limit: 5,
                    documentIds: documentIds,
                    cancellationToken: cancellationToken);

                if (!matchedChunks.Any())
                {
                    bool hasDocumentFilter = documentIds != null && documentIds.Count > 0;
                    return hasDocumentFilter
                        ? "Xin lỗi, các tài liệu bạn đã chọn chưa được xử lý hoặc không có nội dung phù hợp. Vui lòng chọn thêm tài liệu hoặc đặt câu hỏi khác."
                        : "Sorry, I could not find any documents or materials uploaded for this subject to answer your question. Please ask your lecturer to upload course materials.";
                }

                // Construct prompt
                var contextBuilder = new StringBuilder();
                foreach (var chunk in matchedChunks)
                {
                    contextBuilder.AppendLine($"[Source Document: {chunk.FileName}]");
                    contextBuilder.AppendLine(chunk.Content);
                    contextBuilder.AppendLine("---");
                }

                var systemPrompt = "You are a helpful University AI Learning Assistant. Answer the student's question based strictly on the provided course documents. If the documents do not contain enough information to answer, politely state that the answer is not in the course materials and ask the student to contact their lecturer for more details. Keep your response format clear, concise, and using markdown for formatting (such as bullet points or bold text) when helpful.";
                
                var prompt = $"System context:\n{systemPrompt}\n\nCourse materials context:\n{contextBuilder}\n\nStudent's Question: {query}\n\nAnswer:";

                // Validate and resolve model
                var resolvedModel = (!string.IsNullOrWhiteSpace(model) && AllowedModels.Contains(model))
                    ? model
                    : DefaultModel;

                _logger.LogInformation("Sending request to Gemini API using model: {Model}", resolvedModel);
                var answer = await GenerateTextAsync(prompt, resolvedModel, cancellationToken);
                return answer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during RAG chat with SubjectId: {SubjectId}", subjectId);
                return $"An error occurred while communicating with the AI Assistant: {ex.Message}";
            }
        }

        private async Task<string> GenerateTextAsync(string prompt, string model, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new InvalidOperationException("Gemini API key is not configured.");
            }

            var url = $"{_baseUrl.TrimEnd('/')}/models/{model}:generateContent?key={_apiKey}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Gemini generation API error: {(int)response.StatusCode} {error}");
            }

            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseString);
            
            try
            {
                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return text ?? "No response generated by the AI.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse Gemini text generation response. Raw response: {Raw}", responseString);
                throw new InvalidOperationException("Failed to parse AI response.");
            }
        }
    }
}
