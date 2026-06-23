using Core.Entities;
using DAL.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PL
{
    public static class DbSeeder
    {
        public static async Task SeedAdminUserAsync(IServiceProvider serviceProvider)
        {
            var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();

            var encryptionKey = configuration["Security:EncryptionKey"]
                ?? throw new InvalidOperationException("Security:EncryptionKey is required in configuration.");

            await SeedUserAsync(
                dbContext,
                encryptionKey,
                userCode: "ADMIN001",
                email: "admin@lmsai.com",
                password: "Admin123!",
                fullName: "System Admin",
                role: UserRole.Admin
            );

            await SeedUserAsync(
                dbContext,
                encryptionKey,
                userCode: "LECTURER001",
                email: "lecturer@lmsai.com",
                password: "Lecturer123!",
                fullName: "Default Lecturer",
                role: UserRole.Lecturer
            );

            await SeedUserAsync(
                dbContext,
                encryptionKey,
                userCode: "STUDENT001",
                email: "student@lmsai.com",
                password: "Student123!",
                fullName: "Default Student",
                role: UserRole.Student
            );
        }

        private static async Task SeedUserAsync(
            ApplicationDbContext dbContext,
            string encryptionKey,
            string userCode,
            string email,
            string password,
            string fullName,
            UserRole role)
        {
            var emailHash = HashEmail(email);

            var existingUser = await dbContext.Users
                .FirstOrDefaultAsync(u => u.EmailHash == emailHash);

            if (existingUser == null)
            {
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
                var emailEncrypt = EncryptEmail(email, encryptionKey);

                var user = new User
                {
                    Id = Guid.NewGuid(),
                    UserCode = userCode,
                    FullName = fullName,
                    EmailHash = emailHash,
                    EmailEncrypt = emailEncrypt,
                    PasswordHash = passwordHash,
                    Role = role,
                    Status = UserStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await dbContext.Users.AddAsync(user);
                await dbContext.SaveChangesAsync();
            }
            else
            {
                bool updated = false;

                if (existingUser.Role != role)
                {
                    existingUser.Role = role;
                    updated = true;
                }

                if (existingUser.Status != UserStatus.Active)
                {
                    existingUser.Status = UserStatus.Active;
                    updated = true;
                }

                if (string.IsNullOrWhiteSpace(existingUser.UserCode) || existingUser.UserCode == "string")
                {
                    existingUser.UserCode = userCode;
                    updated = true;
                }

                if (string.IsNullOrWhiteSpace(existingUser.FullName) || existingUser.FullName == "string")
                {
                    existingUser.FullName = fullName;
                    updated = true;
                }

                if (updated)
                {
                    existingUser.UpdatedAt = DateTime.UtcNow;
                    dbContext.Users.Update(existingUser);
                    await dbContext.SaveChangesAsync();
                }
            }
        }

        private static string HashEmail(string email)
        {
            using var sha256 = SHA256.Create();

            var bytes = Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant());
            var hashBytes = sha256.ComputeHash(bytes);

            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        private static string EncryptEmail(string email, string encryptionKey)
        {
            using var aes = Aes.Create();

            aes.Key = Encoding.UTF8.GetBytes(encryptionKey);
            aes.GenerateIV();

            var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            var emailBytes = Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant());
            var encryptedBytes = encryptor.TransformFinalBlock(emailBytes, 0, emailBytes.Length);

            var resultBytes = new byte[aes.IV.Length + encryptedBytes.Length];

            Buffer.BlockCopy(aes.IV, 0, resultBytes, 0, aes.IV.Length);
            Buffer.BlockCopy(encryptedBytes, 0, resultBytes, aes.IV.Length, encryptedBytes.Length);

            return Convert.ToBase64String(resultBytes);
        }
    }
}
