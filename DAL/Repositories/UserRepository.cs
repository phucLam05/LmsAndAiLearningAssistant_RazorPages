using Core.Entities;
using DAL.Data;
using DAL.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace DAL.Repositories
{
    /// <summary>
    /// Implementation of the User repository.
    /// Handles database operations directly related to the User entity using Entity Framework Core.
    /// </summary>
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;

        public UserRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Retrieves a user from the database by their hashed email.
        /// </summary>
        /// <param name="emailHash">The hashed email string to search for.</param>
        /// <returns>User object if found, null otherwise.</returns>
        public async Task<User?> GetUserByEmailHashAsync(string emailHash)
        {
            // FirstOrDefaultAsync returns the first element that satisfies the condition or null
            return await _context.Users.FirstOrDefaultAsync(u => u.EmailHash == emailHash);
        }

        /// <summary>
        /// Saves a new user to the database.
        /// </summary>
        /// <param name="user">The user object to be saved.</param>
        /// <returns>The newly created user object with populated Id.</returns>
        public async Task<User> AddUserAsync(User user)
        {
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
            return user;
        }
    }
}
