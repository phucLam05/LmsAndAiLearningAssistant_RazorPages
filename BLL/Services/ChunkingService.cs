#pragma warning disable SKEXP0050, KMEXP00
using BLL.Interfaces;
using Core.DTOs.Common;
using Core.Entities;
using DAL.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Text;

namespace BLL.Services
{
    /// <summary>
    /// Reads document text and chunks it, saving to PostgreSQL.
    /// </summary>
    public class ChunkingService : IChunkingService
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly IDocumentChunkRepository _documentChunkRepository;
        private readonly ILogger<ChunkingService> _logger;
        private readonly ISupabaseStorageProvider _storageProvider;
        private readonly IChunkingConfigService _chunkingConfigService;

        public ChunkingService(
            IDocumentRepository documentRepository,
            IDocumentChunkRepository documentChunkRepository,
            ILogger<ChunkingService> logger, 
            ISupabaseStorageProvider storageProvider,
            IChunkingConfigService chunkingConfigService)
        {
            _documentRepository = documentRepository;
            _documentChunkRepository = documentChunkRepository;
            _logger = logger;
            _storageProvider = storageProvider;
            _chunkingConfigService = chunkingConfigService;
        }

        public async Task<Result> ProcessFileChunkingAsync(Guid documentId, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting chunking process for document {DocumentId}", documentId);

                var document = await _documentRepository.GetByIdAsync(documentId);
                if (document == null)
                {
                    _logger.LogWarning("Document {DocumentId} not found. Skipping chunking.", documentId);
                    return Result.Failure($"Document {documentId} not found.");
                }

                // Resumption logic: If already processing or success, skip to return Success so Hangfire continues to Embedding.
                if (document.Status >= DocumentStatus.Processing)
                {
                    _logger.LogInformation("Document {DocumentId} is already at status {Status}. Skipping chunking.", documentId, document.Status);
                    return Result.Success();
                }

                // Update status to Processing
                await _documentRepository.UpdateStatusAsync(documentId, DocumentStatus.Processing);

                // Download and extract text
                var fileContent = await ReadFileContentAsync(document, cancellationToken);
                
                if (!fileContent.Sections.Any())
                {
                    _logger.LogWarning("No content extracted from document {DocumentId}. Skipping chunk creation.", documentId);
                    // It will remain Processing for Embedding phase, which will handle empty chunks.
                    return Result.Success();
                }

                // Get chunking configuration dynamically
                var config = await _chunkingConfigService.GetConfigAsync();

                // Chunk the text
                string extension = Path.GetExtension(document.FileName).ToLowerInvariant();
                var chunks = ChunkText(fileContent, config.Method, config.ChunkSize, config.OverlapSize, document.Id, document.SubjectId, extension).ToList();

                if (chunks.Any())
                {
                    await _documentChunkRepository.BulkInsertChunksAsync(chunks);
                    _logger.LogInformation("Successfully inserted {ChunkCount} chunks for document {DocumentId}", chunks.Count, documentId);
                }

                // Status remains Processing for Embedding.
                
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while chunking document {DocumentId}", documentId);
                _documentRepository.ClearTracker();
                await _documentRepository.UpdateStatusAsync(documentId, DocumentStatus.Failed);
                return Result.Failure($"Chunking error: {ex.Message}");
            }
        }

        private async Task<Microsoft.KernelMemory.DataFormats.FileContent> ReadFileContentAsync(Document document, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Downloading file from Supabase Path: {StoragePath}", document.FileUrl);
                
                using var stream = await _storageProvider.DownloadAsync(document.FileUrl, cancellationToken);
                string extension = Path.GetExtension(document.FileName).ToLowerInvariant();

                if (extension == ".docx")
                {
                    _logger.LogInformation("Extracting text from DOCX using DocumentFormat.OpenXml");
                    var fileContent = new Microsoft.KernelMemory.DataFormats.FileContent("application/vnd.openxmlformats-officedocument.wordprocessingml.document");

                    using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(stream, false);
                    var body = doc.MainDocumentPart?.Document?.Body;
                    if (body != null)
                    {
                        var paragraphs = body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()
                                             .Select(p => p.InnerText)
                                             .Where(txt => !string.IsNullOrWhiteSpace(txt));
                        
                        var text = string.Join("\n\n", paragraphs);
                        var chunk = new Microsoft.KernelMemory.DataFormats.Chunk(text, 1);
                        fileContent.Sections.Add(chunk);
                    }
                    return fileContent;
                }

                Microsoft.KernelMemory.DataFormats.IContentDecoder decoder = extension switch
                {
                    ".pdf" => new Microsoft.KernelMemory.DataFormats.Pdf.PdfDecoder(),
                    ".md" => new Microsoft.KernelMemory.DataFormats.Text.MarkDownDecoder(),
                    _ => new Microsoft.KernelMemory.DataFormats.Text.TextDecoder()
                };

                _logger.LogInformation("Extracting text using Kernel Memory Decoder: {DecoderName}", decoder.GetType().Name);
                
                return await decoder.DecodeAsync(stream, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read file from Supabase Path: {StoragePath}", document.FileUrl);
                throw new InvalidOperationException($"Could not read file from storage: {document.FileUrl}", ex);
            }
        }

        /// <summary>
        /// Chunks the provided text based on chunk size and overlap size.
        /// </summary>
        /// <param name="text">The text to chunk.</param>
        /// <param name="chunkSize">The maximum size of each chunk.</param>
        /// <param name="overlap">The size of the overlap between consecutive chunks.</param>
        /// <param name="documentId">The ID of the document being chunked.</param>
        /// <param name="subjectId">The Subject ID for RAG filtering.</param>
        /// <returns>An enumerable of DocumentChunk entities.</returns>
        private IEnumerable<DocumentChunk> ChunkText(
            Microsoft.KernelMemory.DataFormats.FileContent fileContent, 
            string method,
            int chunkSize, 
            int overlap, 
            Guid documentId, 
            Guid? subjectId,
            string extension)
        {
            int index = 0;
            foreach (var section in fileContent.Sections)
            {
                if (string.IsNullOrWhiteSpace(section.Content)) continue;

                IEnumerable<string> chunkTexts;
                if (string.Equals(method, "Word", StringComparison.OrdinalIgnoreCase))
                {
                    chunkTexts = ChunkByWords(section.Content, chunkSize, overlap);
                }
                else if (string.Equals(method, "Character", StringComparison.OrdinalIgnoreCase))
                {
                    chunkTexts = ChunkByCharacters(section.Content, chunkSize, overlap);
                }
                else
                {
                    // Paragraph: split lines, then group to paragraphs using SK
                    if (string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase))
                    {
                        var lines = TextChunker.SplitMarkDownLines(section.Content, maxTokensPerLine: 100);
                        chunkTexts = TextChunker.SplitMarkdownParagraphs(lines, maxTokensPerParagraph: chunkSize, overlapTokens: overlap);
                    }
                    else
                    {
                        var lines = TextChunker.SplitPlainTextLines(section.Content, maxTokensPerLine: 100);
                        chunkTexts = TextChunker.SplitPlainTextParagraphs(lines, maxTokensPerParagraph: chunkSize, overlapTokens: overlap);
                    }
                }

                foreach (var p in chunkTexts)
                {
                    yield return new DocumentChunk
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = documentId,
                        SubjectId = subjectId,
                        ChunkIndex = index++,
                        Content = p.Replace("\0", string.Empty),
                        TokenCount = p.Length / 4, // Rough estimation since we don't have a tokenizer here
                        PageNumber = section.Number,
                        CreatedAt = DateTime.UtcNow
                    };
                }
            }
        }

        private IEnumerable<string> ChunkByWords(string text, int chunkSize, int overlapSize)
        {
            if (string.IsNullOrWhiteSpace(text)) yield break;
            var words = text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) yield break;

            if (chunkSize <= 0) chunkSize = 150;
            if (overlapSize < 0 || overlapSize >= chunkSize) overlapSize = chunkSize / 10;

            int step = chunkSize - overlapSize;
            if (step <= 0) step = 1;

            for (int i = 0; i < words.Length; i += step)
            {
                var chunkWords = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Take(System.Linq.Enumerable.Skip(words, i), chunkSize));
                if (chunkWords.Count == 0) break;
                yield return string.Join(" ", chunkWords);
                if (i + chunkSize >= words.Length) break;
            }
        }

        private IEnumerable<string> ChunkByCharacters(string text, int chunkSize, int overlapSize)
        {
            if (string.IsNullOrWhiteSpace(text)) yield break;
            if (chunkSize <= 0) chunkSize = 500;
            if (overlapSize < 0 || overlapSize >= chunkSize) overlapSize = chunkSize / 10;

            int step = chunkSize - overlapSize;
            if (step <= 0) step = 1;

            for (int i = 0; i < text.Length; i += step)
            {
                if (i + chunkSize >= text.Length)
                {
                    yield return text.Substring(i);
                    break;
                }
                yield return text.Substring(i, chunkSize);
            }
        }
    }
}
