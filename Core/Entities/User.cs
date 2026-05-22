using System;

namespace Core.Entities
{
    /// <summary>
    /// Represents a user in the system.
    /// Stores necessary authentication details such as encrypted and hashed email, and hashed password.
    /// </summary>
    public class User
    {
        /// <summary>
        /// Unique identifier for the user.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Encrypted version of the user's email. 
        /// Used to retrieve the original email when needed without storing it in plain text.
        /// </summary>
        public string EmailEncrypt { get; set; } = string.Empty;

        /// <summary>
        /// Hashed version of the user's email.
        /// Used for deterministic searching/querying of the user by email without decrypting the entire table.
        /// </summary>
        public string EmailHash { get; set; } = string.Empty;

        /// <summary>
        /// Hashed user password using BCrypt or similar secure algorithm.
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// The full name of the user.
        /// </summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// Date and time when the user was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
