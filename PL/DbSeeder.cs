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

        public static async Task SeedSubjectsAsync(IServiceProvider serviceProvider)
        {
            var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();

            var seedSubjects = new[]
            {
                new { SubjectCode = "PRN222", Name = "ASP.NET Core Web API", Description = "Lập trình ứng dụng web API với ASP.NET Core, Entity Framework Core và kiến trúc RESTful." },
                new { SubjectCode = "PRJ301", Name = "Java Web Application", Description = "Phát triển ứng dụng web với Java Servlet, JSP, JSTL và các framework MVC." },
                new { SubjectCode = "MAD101", Name = "Mobile Application Development", Description = "Lập trình ứng dụng di động đa nền tảng với Flutter/React Native." },
                new { SubjectCode = "SWP391", Name = "Software Project", Description = "Dự án phần mềm theo nhóm: lập kế hoạch, phân tích yêu cầu, thiết kế và triển khai." },
                new { SubjectCode = "SWD392", Name = "Software Architecture & Design", Description = "Kiến trúc phần mềm, Design Patterns, Microservices và phương pháp thiết kế hệ thống lớn." },
                new { SubjectCode = "SEP490", Name = "Software Engineering Capstone", Description = "Đồ án tốt nghiệp chuyên ngành Kỹ thuật phần mềm — phát triển sản phẩm thực tế hoàn chỉnh." },
                new { SubjectCode = "CSD203", Name = "Data Structures & Algorithms", Description = "Cấu trúc dữ liệu, giải thuật sắp xếp, tìm kiếm và phân tích độ phức tạp thuật toán." },
                new { SubjectCode = "PRF192", Name = "Programming Fundamentals C", Description = "Lập trình cơ bản với ngôn ngữ C: kiểu dữ liệu, con trỏ, mảng, hàm và file I/O." },
                new { SubjectCode = "IOT102", Name = "Internet of Things", Description = "Thiết kế và lập trình thiết bị IoT với Arduino, ESP32 và các giao thức MQTT, HTTP." },
                new { SubjectCode = "MAS291", Name = "Statistics & Probability", Description = "Xác suất thống kê ứng dụng trong phân tích dữ liệu và kiểm định giả thuyết." },
                new { SubjectCode = "SSG104", Name = "Communication & In-Group Working Skills", Description = "Kỹ năng giao tiếp, làm việc nhóm hiệu quả và kỹ năng thuyết trình chuyên nghiệp." },
                new { SubjectCode = "MLN111", Name = "Introduction to Marxism-Leninism Philosophy", Description = "Triết học Mác-Lênin: vật chất, ý thức, phép biện chứng duy vật và nhận thức luận." },
                new { SubjectCode = "OJT202", Name = "On-the-Job Training", Description = "Thực tập doanh nghiệp — áp dụng kiến thức chuyên ngành vào môi trường làm việc thực tế." },
                new { SubjectCode = "NWC203", Name = "Computer Networks", Description = "Kiến trúc mạng máy tính, giao thức TCP/IP, mô hình OSI, bảo mật mạng và quản trị hệ thống." },
                new { SubjectCode = "DBM302", Name = "Database Management Systems", Description = "Thiết kế cơ sở dữ liệu quan hệ, SQL nâng cao, tối ưu hóa truy vấn và quản trị DBMS." },
            };

            foreach (var s in seedSubjects)
            {
                var exists = await dbContext.Subjects.AnyAsync(x => x.SubjectCode == s.SubjectCode);
                if (!exists)
                {
                    await dbContext.Subjects.AddAsync(new Subject
                    {
                        Id = Guid.NewGuid(),
                        SubjectCode = s.SubjectCode,
                        Name = s.Name,
                        Description = s.Description,
                        Status = SubjectStatus.Active,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                    });
                }
            }

            await dbContext.SaveChangesAsync();
        }
    }
}

