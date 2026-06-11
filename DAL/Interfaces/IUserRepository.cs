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

        Task<User?> GetByIdAsync(Guid id);

        Task<User> AddUserAsync(User user);

        Task UpdateAsync(User user);

        Task DeleteAsync(User user);

        Task<IReadOnlyList<User>> GetAllUsersAsync();

        Task<bool> UserCodeExistsAsync(string userCode);
    }
}
