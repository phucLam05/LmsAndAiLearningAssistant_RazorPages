using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Entities;
using DAL.Data;
using DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repositories
{
    /// <summary>
    /// EF Core implementation for storing and querying uploaded document metadata.
    /// </summary>
    public class DocumentRepository : IDocumentRepository
    {
        private readonly ApplicationDbContext _context;

        public DocumentRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<Document>> GetBySubjectIdAsync(Guid subjectId)
        {
            return await _context.Documents
                .AsNoTracking()
                .Include(d => d.Subject)
                .Include(d => d.Uploader)
                .Where(d => d.SubjectId == subjectId)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Document>> GetByUploadedByAsync(Guid userId)
        {
            return await _context.Documents
                .AsNoTracking()
                .Include(d => d.Subject)
                .Where(d => d.UploadedBy == userId)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }

        public async Task<Document?> GetByIdForUserAsync(Guid documentId, Guid userId)
        {
            return await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == documentId && d.UploadedBy == userId);
        }

        public async Task<Document?> GetByIdAsync(Guid id)
        {
            return await _context.Documents
                .Include(d => d.Subject)
                .Include(d => d.Uploader)
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task<Document> AddAsync(Document document)
        {
            await _context.Documents.AddAsync(document);
            await _context.SaveChangesAsync();
            return document;
        }

        public async Task DeleteAsync(Document document)
        {
            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Updates the processing status of a specific document.
        /// </summary>
        /// <param name="id">The unique identifier of the document.</param>
        /// <param name="status">The new processing status.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task UpdateStatusAsync(Guid id, DocumentStatus status)
        {
            var document = await _context.Documents.FindAsync(id);
            if (document != null)
            {
                document.Status = status;
                document.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Updates an existing document in the database.
        /// </summary>
        public async Task UpdateAsync(Document document)
        {
            _context.Documents.Update(document);
            await _context.SaveChangesAsync();
        }

        public void ClearTracker()
        {
            _context.ChangeTracker.Clear();
        }

        public async Task<IReadOnlyList<Document>> GetAllWithDetailsAsync()
        {
            return await _context.Documents
                .AsNoTracking()
                .Include(d => d.Subject)
                .Include(d => d.Uploader)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Document>> QueryAsync(string? search, Core.Entities.DocumentStatus? status, Guid? subjectId, int pageIndex, int pageSize)
        {
            var q = ApplyFilters(_context.Documents.AsNoTracking().Include(d => d.Subject).Include(d => d.Uploader), search, status, subjectId);
            return await q.OrderByDescending(d => d.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> CountAsync(string? search, Core.Entities.DocumentStatus? status, Guid? subjectId)
        {
            return await ApplyFilters(_context.Documents.AsNoTracking(), search, status, subjectId).CountAsync();
        }

        private static IQueryable<Document> ApplyFilters(IQueryable<Document> q, string? search, Core.Entities.DocumentStatus? status, Guid? subjectId)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                q = q.Where(d => d.FileName.ToLower().Contains(s));
            }
            if (status.HasValue) q = q.Where(d => d.Status == status.Value);
            if (subjectId.HasValue) q = q.Where(d => d.SubjectId == subjectId.Value);
            return q;
        }
    }
}
