using Core.Entities;
using DAL.Data;
using DAL.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DAL.Repositories
{
    /// <summary>
    /// EF Core implementation of ISubjectRepository.
    /// Always includes the Lecturer navigation property for display purposes.
    /// </summary>
    public class SubjectRepository : ISubjectRepository
    {
        private readonly ApplicationDbContext _db;

        public SubjectRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<Subject>> GetAllAsync()
        {
            return await _db.Subjects
                .Include(s => s.Lecturer)
                .OrderBy(s => s.SubjectCode)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<Subject>> GetByLecturerIdAsync(Guid lecturerId)
        {
            return await _db.Subjects
                .Include(s => s.Lecturer)
                .Where(s => s.LecturerId == lecturerId)
                .OrderBy(s => s.SubjectCode)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<Subject>> GetActiveAsync()
        {
            return await _db.Subjects
                .Include(s => s.Lecturer)
                .Where(s => s.Status == SubjectStatus.Active)
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public Task<Subject?> GetByIdAsync(Guid id)
        {
            return _db.Subjects
                .Include(s => s.Lecturer)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        /// <inheritdoc/>
        public async Task<Subject> CreateAsync(Subject subject)
        {
            _db.Subjects.Add(subject);
            await _db.SaveChangesAsync();
            return subject;
        }

        /// <inheritdoc/>
        public async Task<Subject> UpdateAsync(Subject subject)
        {
            _db.Subjects.Update(subject);
            await _db.SaveChangesAsync();
            return subject;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(Guid id)
        {
            var subject = await _db.Subjects.FindAsync(id);
            if (subject == null) return false;

            _db.Subjects.Remove(subject);
            await _db.SaveChangesAsync();
            return true;
        }

        /// <inheritdoc/>
        public Task<bool> ExistsAsync(string subjectCode, Guid? excludeId = null)
        {
            return _db.Subjects.AnyAsync(s =>
                s.SubjectCode == subjectCode &&
                (excludeId == null || s.Id != excludeId.Value));
        }

        public IQueryable<Subject> Query() => _db.Subjects.Include(s => s.Lecturer);

        public async Task<(IReadOnlyList<Subject> Items, int TotalCount)> GetPagedAsync(
            string? search, SubjectStatus? status, int pageIndex, int pageSize)
        {
            var q = _db.Subjects.Include(s => s.Lecturer).AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToUpperInvariant();
                q = q.Where(x => x.SubjectCode.ToUpper().Contains(s) || x.Name.ToUpper().Contains(s));
            }
            if (status.HasValue) q = q.Where(x => x.Status == status.Value);
            var total = await q.CountAsync();
            var items = await q.OrderBy(x => x.SubjectCode).Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, total);
        }

        public async Task<(IReadOnlyList<Subject> Items, int TotalCount)> GetPagedByLecturerAsync(
            Guid lecturerId, string? search, int pageIndex, int pageSize)
        {
            var q = _db.Subjects.Include(s => s.Lecturer).AsNoTracking().Where(s => s.LecturerId == lecturerId);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToUpperInvariant();
                q = q.Where(x => x.SubjectCode.ToUpper().Contains(s) || x.Name.ToUpper().Contains(s));
            }
            var total = await q.CountAsync();
            var items = await q.OrderBy(x => x.SubjectCode).Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, total);
        }

        public async Task<(IReadOnlyList<Subject> Items, int TotalCount)> GetPagedActiveAsync(
            string? search, int pageIndex, int pageSize)
        {
            var q = _db.Subjects.Include(s => s.Lecturer).AsNoTracking().Where(s => s.Status == SubjectStatus.Active);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToUpperInvariant();
                q = q.Where(x => x.SubjectCode.ToUpper().Contains(s) || x.Name.ToUpper().Contains(s));
            }
            var total = await q.CountAsync();
            var items = await q.OrderBy(x => x.Name).Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, total);
        }
    }
}
