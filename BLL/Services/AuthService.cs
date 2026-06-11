using BLL.Interfaces;
using Core.DTOs.Auth;
using Core.DTOs.Common;
using Core.Entities;
using DAL.Interfaces;
using Microsoft.Extensions.Configuration;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services
{
    /// <summary>
    /// Implementation of authentication operations.
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly string _encryptionKey;

        public AuthService(IUserRepository userRepository, IConfiguration configuration)
        {
            _userRepository = userRepository;
            _encryptionKey = configuration["Security:EncryptionKey"] ?? "FallbackKeyForDevExactly32Bytes!"; // 32 bytes string
            
            // Ensure key is 32 bytes for AES-256
            if (Encoding.UTF8.GetByteCount(_encryptionKey) != 32)
            {
                throw new InvalidOperationException($"Encryption key must be exactly 32 bytes long for AES-256. Current length: {Encoding.UTF8.GetByteCount(_encryptionKey)}");
            }
        }

        public async Task<(bool Success, string ErrorMessage)> RegisterAsync(RegisterDto registerDto)
        {
            // 1. Normalize email
            var normalizedEmail = registerDto.Email.Trim().ToLowerInvariant();

            // 2. Hash the email to check if it already exists
            var emailHash = HashEmail(normalizedEmail);

            User? existingUser = null;
            try
            {
                existingUser = await _userRepository.GetUserByEmailHashAsync(emailHash);
            }
            catch (Exception)
            {
                // Log exception in production
                return (false, "A database connection error occurred. Please try again later.");
            }

            if (existingUser != null)
            {
                return (false, "Email is already registered.");
            }

            // 3. Encrypt the email for storage
            var emailEncrypt = EncryptEmail(normalizedEmail);

            // 4. Hash the password using BCrypt
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password);

            // 5. Create and save the new user
            var newUser = new User
            {
                Id = Guid.NewGuid(),
                FullName = registerDto.FullName,
                EmailHash = emailHash,
                EmailEncrypt = emailEncrypt,
                PasswordHash = passwordHash,
                Role = registerDto.Role,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _userRepository.AddUserAsync(newUser);
            }
            catch (Exception ex)
            {
                // Handle unexpected database exceptions (e.g., connection failure)
                // In a production app, log the exception (ex) here using ILogger
                return (false, "An error occurred while creating your account. Please try again later.");
            }

            return (true, string.Empty);
        }

        public async Task<User?> LoginAsync(LoginDto loginDto)
        {
            var normalizedEmail = loginDto.Email.Trim().ToLowerInvariant();
            var emailHash = HashEmail(normalizedEmail);

            User? user = null;
            try
            {
                // Fetch user by deterministic email hash
                user = await _userRepository.GetUserByEmailHashAsync(emailHash);
            }
            catch (Exception)
            {
                // In a production app, log the exception here
                // We return null to fail the login gracefully
                return null;
            }
            
            if (user == null)
            {
                return null;
            }

            // Verify password using BCrypt
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash);

            if (!isPasswordValid)
            {
                return null;
            }

            return user;
        }

        public async Task<Result> ActivateAccountAsync(Guid userId, string temporaryPassword, string newPassword)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return Result.Failure("User not found.");
            }

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(temporaryPassword, user.PasswordHash);
            if (!isPasswordValid)
            {
                return Result.Failure("Invalid temporary password.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.Status = UserStatus.Active;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user);
            return Result.Success();
        }

        /// <summary>
        /// Hashes the email using SHA256 for deterministic searching.
        /// </summary>
        private string HashEmail(string email)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(email);
                var hashBytes = sha256.ComputeHash(bytes);
                return Convert.ToHexString(hashBytes).ToLowerInvariant();
            }
        }

        /// <summary>
        /// Encrypts the email using AES.
        /// </summary>
        private string EncryptEmail(string email)
        {
            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = Encoding.UTF8.GetBytes(_encryptionKey);
                    // Generate a random IV for each encryption
                    aes.GenerateIV();
                    
                    var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                    var emailBytes = Encoding.UTF8.GetBytes(email);
                    var encryptedBytes = encryptor.TransformFinalBlock(emailBytes, 0, emailBytes.Length);

                    // Prepend the IV to the encrypted data so we can decrypt it later
                    var resultBytes = new byte[aes.IV.Length + encryptedBytes.Length];
                    Buffer.BlockCopy(aes.IV, 0, resultBytes, 0, aes.IV.Length);
                    Buffer.BlockCopy(encryptedBytes, 0, resultBytes, aes.IV.Length, encryptedBytes.Length);

                    return Convert.ToBase64String(resultBytes);
                }
            }
            catch
            {
                // Fallback or handle error appropriately in production
                throw new InvalidOperationException("Encryption failed.");
            }
        }

        /// <summary>
        /// Decrypts the email using AES.
        /// </summary>
        public string DecryptEmail(string encryptedEmailBase64)
        {
            try
            {
                var fullCipher = Convert.FromBase64String(encryptedEmailBase64);

                using (var aes = Aes.Create())
                {
                    aes.Key = Encoding.UTF8.GetBytes(_encryptionKey);
                    
                    // Extract IV from the first 16 bytes
                    var iv = new byte[aes.BlockSize / 8];
                    var cipherText = new byte[fullCipher.Length - iv.Length];
                    
                    Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
                    Buffer.BlockCopy(fullCipher, iv.Length, cipherText, 0, cipherText.Length);
                    
                    aes.IV = iv;

                    var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                    var decryptedBytes = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);

                    return Encoding.UTF8.GetString(decryptedBytes);
                }
            }
            catch
            {
                throw new InvalidOperationException("Decryption failed.");
            }
        }


    }
}
