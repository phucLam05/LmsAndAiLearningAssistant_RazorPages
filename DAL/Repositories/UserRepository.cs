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
    /// EF Core implementation of the User repository.
    /// </summary>
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;

        public UserRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetUserByEmailHashAsync(string emailHash)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.EmailHash == emailHash);
        }

        public async Task<User?> GetByIdAsync(Guid id)
        {
            return await _context.Users.FindAsync(id);
        }

        public async Task<User?> GetByUserCodeAsync(string userCode)
        {
            if (string.IsNullOrWhiteSpace(userCode)) return null;
            var normalized = userCode.Trim().ToUpperInvariant();
            return await _context.Users.FirstOrDefaultAsync(u => u.UserCode == normalized);
        }

        public async Task<User> AddUserAsync(User user)
        {
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task UpdateAsync(User user)
        {
            user.UpdatedAt = DateTime.UtcNow;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(User user)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<User>> GetAllUsersAsync()
        {
            return await _context.Users
                .AsNoTracking()
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<User>> QueryUsersAsync(string? search, UserRole? role, UserStatus? status, int pageIndex, int pageSize)
        {
            var q = ApplyFilters(_context.Users.AsNoTracking(), search, role, status);
            return await q
                .OrderByDescending(u => u.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> CountUsersAsync(string? search, UserRole? role, UserStatus? status)
        {
            return await ApplyFilters(_context.Users.AsNoTracking(), search, role, status).CountAsync();
        }

        private static IQueryable<User> ApplyFilters(IQueryable<User> q, string? search, UserRole? role, UserStatus? status)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToUpperInvariant();
                q = q.Where(u =>
                    u.UserCode.ToUpper().Contains(s) ||
                    u.FullName.ToUpper().Contains(s));
            }
            if (role.HasValue) q = q.Where(u => u.Role == role.Value);
            if (status.HasValue) q = q.Where(u => u.Status == status.Value);
            return q;
        }

        public async Task<bool> UserCodeExistsAsync(string userCode)
        {
            return await _context.Users.AnyAsync(u => u.UserCode == userCode);
        }
    }
}
