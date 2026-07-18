using System.Text;
using BLL.Interfaces;
using Core.DTOs.Common;
using Core.DTOs.Documents;
using Core.Entities;
using DAL.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace BLL.Services;

public class DocumentConflictService : IDocumentConflictService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentChunkRepository _chunkRepository;
    private readonly IGeminiChatProvider _geminiChat;
    private readonly IMemoryCache _cache;

    public DocumentConflictService(
        IDocumentRepository documentRepository,
        IDocumentChunkRepository chunkRepository,
        IGeminiChatProvider geminiChat,
        IMemoryCache cache)
    {
        _documentRepository = documentRepository;
        _chunkRepository = chunkRepository;
        _geminiChat = geminiChat;
        _cache = cache;
    }

    public async Task<Result<DocumentConflictComparisonDto>> CompareAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(documentId);
        if (document == null || document.Status != DocumentStatus.Conflict)
            return Result<DocumentConflictComparisonDto>.Failure("Tài liệu không ở trạng thái cần kiểm tra.");

        var cacheKey = $"document-conflict:{document.Id}:{document.UpdatedAt.Ticks}";
        if (_cache.TryGetValue<DocumentConflictComparisonDto>(cacheKey, out var cached) && cached != null)
        {
            cached.Cached = true;
            return Result<DocumentConflictComparisonDto>.Success(cached);
        }

        var chunks = await _chunkRepository.GetChunksByDocumentIdAsync(documentId);
        var pairs = new List<(DocumentChunk NewChunk, DocumentChunk OldChunk)>();
        foreach (var chunk in chunks.Where(c => c.Embedding != null))
        {
            var matches = await _chunkRepository.FindSimilarChunksFromOtherDocumentsAsync(
                document.SubjectId ?? Guid.Empty, documentId, chunk.Embedding!, 1, cancellationToken);
            if (matches.Count > 0) pairs.Add((chunk, matches[0]));
        }

        if (pairs.Count == 0)
            return Result<DocumentConflictComparisonDto>.Failure("Không tìm thấy đoạn tương đồng để so sánh.");

        var oldName = pairs[0].OldChunk.Document?.FileName ?? "Tài liệu đã có";
        var prompt = new StringBuilder(
            $"Bạn là hệ thống kiểm tra trùng kiến thức. So sánh các cặp đoạn của tài liệu mới '{document.FileName}' " +
            $"và tài liệu cũ '{oldName}'. Chỉ trả JSON hợp lệ, không markdown, dạng " +
            "{\"summary\":\"...\",\"matches\":[{\"newText\":\"đoạn trùng trong tài liệu mới\",\"oldText\":\"đoạn tương ứng trong tài liệu cũ\",\"reason\":\"kiến thức giống nhau\"}]}. " +
            "Không đưa phần không trùng.\n");
        foreach (var pair in pairs)
            prompt.AppendLine($"NEW: {pair.NewChunk.Content}\nOLD: {pair.OldChunk.Content}\n---");

        var generated = await _geminiChat.GenerateTextAsync(prompt.ToString(), "gemini-2.5-flash", cancellationToken);
        var comparison = new DocumentConflictComparisonDto
        {
            NewFileName = document.FileName,
            OldFileName = oldName,
            Analysis = generated.Answer,
            Cached = false
        };
        _cache.Set(cacheKey, comparison, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromHours(6),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1)
        });
        return Result<DocumentConflictComparisonDto>.Success(comparison);
    }
}
