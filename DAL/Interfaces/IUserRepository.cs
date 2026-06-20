using Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    /// <summary>
    /// Provides database and data access operations for managing users.
    /// </summary>
    public interface IUserRepository
    {
        Task<User?> GetUserByEmailHashAsync(string emailHash);

        Task<User?> GetByUserCodeAsync(string userCode);

        Task<User?> GetByIdAsync(Guid id);

        Task<User> AddUserAsync(User user);

        Task UpdateAsync(User user);

        Task DeleteAsync(User user);

        Task<IReadOnlyList<User>> GetAllUsersAsync();

        Task<IReadOnlyList<User>> QueryUsersAsync(string? search, UserRole? role, UserStatus? status, int pageIndex, int pageSize);

        Task<int> CountUsersAsync(string? search, UserRole? role, UserStatus? status);

        /// <summary>
        /// Returns an IQueryable&lt;User&gt; so the service layer can compose search/role
        /// filters with paging (used by Admin → Users page).
        /// </summary>
        IQueryable<User> Query();

        Task<bool> UserCodeExistsAsync(string userCode);
    }
}
