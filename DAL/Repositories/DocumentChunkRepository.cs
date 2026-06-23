using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Entities;
using DAL.Data;
using DAL.Interfaces;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace DAL.Repositories
{
    /// <summary>
    /// EF Core implementation for storing and querying document chunks.
    /// </summary>
    public class DocumentChunkRepository : IDocumentChunkRepository
    {
        private readonly ApplicationDbContext _context;

        public DocumentChunkRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task BulkInsertChunksAsync(IEnumerable<DocumentChunk> chunks)
        {
            foreach (var chunk in chunks)
            {
                if (chunk.Content != null)
                {
                    chunk.Content = chunk.Content.Replace("\0", string.Empty);
                }
            }
            await _context.DocumentChunks.AddRangeAsync(chunks);
            await _context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<DocumentChunk>> GetChunksByDocumentIdAsync(Guid documentId)
        {
            return await _context.DocumentChunks
                .Where(c => c.DocumentId == documentId)
                .OrderBy(c => c.ChunkIndex)
                .ToListAsync();
        }

        public async Task DeleteChunksByDocumentIdAsync(Guid documentId)
        {
            var chunks = await _context.DocumentChunks.Where(c => c.DocumentId == documentId).ToListAsync();
            if (chunks.Any())
            {
                _context.DocumentChunks.RemoveRange(chunks);
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateChunksAsync(IEnumerable<DocumentChunk> chunks)
        {
            foreach (var chunk in chunks)
            {
                if (chunk.Content != null)
                {
                    chunk.Content = chunk.Content.Replace("\0", string.Empty);
                }
            }
            _context.DocumentChunks.UpdateRange(chunks);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> HasChunksAsync(Guid documentId)
        {
            return await _context.DocumentChunks.AnyAsync(c => c.DocumentId == documentId);
        }

        public async Task<IReadOnlyList<DocumentChunk>> SearchSimilarChunksAsync(
            Guid subjectId,
            Vector queryEmbedding,
            int limit,
            List<Guid>? documentIds = null,
            CancellationToken cancellationToken = default)
        {
            var query = _context.DocumentChunks
                .Where(c => c.SubjectId == subjectId && c.Embedding != null);

            if (documentIds != null && documentIds.Count > 0)
            {
                query = query.Where(c => c.DocumentId != null && documentIds.Contains(c.DocumentId!.Value));
            }

            return await query
                .OrderBy(c => c.Embedding!.CosineDistance(queryEmbedding))
                .Take(limit)
                .Include(c => c.Document)
                .ToListAsync(cancellationToken);
        }

        public async Task<int> CountAllAsync()
        {
            return await _context.DocumentChunks.CountAsync();
        }
    }
}
