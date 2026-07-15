using BLL.Interfaces;
using Core.DTOs.Admin;
using Core.DTOs.Common;
using Core.Entities;
using DAL.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services
{
    /// <summary>
    /// Core user management service for manual CRUD and bulk imports.
    /// </summary>
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IEmailService _emailService;
        private readonly string _encryptionKey;
        private readonly ILogger<UserService> _logger;

        public UserService(
            IUserRepository userRepository,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<UserService> logger)
        {
            _userRepository = userRepository;
            _emailService = emailService;
            _logger = logger;
            _encryptionKey = configuration["Security:EncryptionKey"] ?? "FallbackKeyForDevExactly32Bytes!";

            if (Encoding.UTF8.GetByteCount(_encryptionKey) != 32)
            {
                throw new InvalidOperationException($"Encryption key must be exactly 32 bytes long for AES-256. Current length: {Encoding.UTF8.GetByteCount(_encryptionKey)}");
            }
        }

        public async Task<Result<User>> CreateUserAsync(UserCreateDto dto)
        {
            // ── Validation: UserCode required, format tied to role
            if (string.IsNullOrWhiteSpace(dto.UserCode))
                return Result<User>.Failure("Mã người dùng là bắt buộc.");

            var normalizedCode = dto.UserCode.Trim().ToUpperInvariant();

            // Lecturer codes must start with LEC and be unique
            if (dto.Role == UserRole.Lecturer)
            {
                if (!normalizedCode.StartsWith("LEC", StringComparison.Ordinal))
                    return Result<User>.Failure("Mã giảng viên phải bắt đầu bằng 'LEC' (VD: LEC001).");
                if (normalizedCode.Length < 4 || normalizedCode.Length > 50)
                    return Result<User>.Failure("Mã giảng viên không hợp lệ (độ dài 4-50).");
            }
            else if (dto.Role == UserRole.Student)
            {
                if (!normalizedCode.StartsWith("HE", StringComparison.Ordinal) && !normalizedCode.StartsWith("STU", StringComparison.Ordinal))
                    return Result<User>.Failure("Mã sinh viên phải bắt đầu bằng 'HE' hoặc 'STU' (VD: HE170123).");
            }
            else if (dto.Role == UserRole.Admin)
            {
                if (!normalizedCode.StartsWith("ADM", StringComparison.Ordinal))
                    return Result<User>.Failure("Mã quản trị phải bắt đầu bằng 'ADM' (VD: ADM001).");
            }

            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
            var emailHash = HashEmail(normalizedEmail);

            var existingUser = await _userRepository.GetUserByEmailHashAsync(emailHash);
            if (existingUser != null)
            {
                return Result<User>.Failure("Email đã được đăng ký.");
            }

            var codeExists = await _userRepository.UserCodeExistsAsync(normalizedCode);
            if (codeExists)
            {
                return Result<User>.Failure($"Mã '{normalizedCode}' đã tồn tại.");
            }

            var tempPassword = GenerateRandomPassword();
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);

            var newUser = new User
            {
                Id = Guid.NewGuid(),
                UserCode = normalizedCode,
                FullName = dto.FullName.Trim(),
                EmailHash = emailHash,
                EmailEncrypt = EncryptEmail(normalizedEmail),
                PasswordHash = passwordHash,
                Role = dto.Role,
                Status = UserStatus.Inactive, // Inactive triggers first-time login flow
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _userRepository.AddUserAsync(newUser);

            // Send notification email in background (with fallback to console if SMTP not configured)
            try
            {
                await _emailService.SendFirstTimeLoginEmailAsync(normalizedEmail, newUser.FullName, newUser.UserCode, tempPassword);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Email send failed for {Email}, but user {UserCode} was created.", normalizedEmail, normalizedCode);
            }

            return Result<User>.Success(newUser);
        }

        public async Task<Result<string>> ResetPasswordAsync(Guid id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null) return Result<string>.Failure("Không tìm thấy người dùng.");

            var newPassword = GenerateRandomPassword();
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.Status = UserStatus.Inactive; // force password change on next login
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);

            // Try to notify the user (best-effort; fall back to console if SMTP not configured).
            try
            {
                var email = !string.IsNullOrEmpty(user.EmailEncrypt)
                    ? DecryptEmailInternal(user.EmailEncrypt)
                    : null;
                if (!string.IsNullOrEmpty(email))
                {
                    await _emailService.SendPasswordResetNotificationAsync(email, user.FullName, newPassword);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send password-reset notification for user {UserCode}.", user.UserCode);
            }

            return Result<string>.Success(newPassword);
        }

        private string DecryptEmailInternal(string encryptedEmailBase64)
        {
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(_encryptionKey);
            var allBytes = Convert.FromBase64String(encryptedEmailBase64);
            var iv = new byte[aes.BlockSize / 8];
            Buffer.BlockCopy(allBytes, 0, iv, 0, iv.Length);
            aes.IV = iv;
            var cipherBytes = new byte[allBytes.Length - iv.Length];
            Buffer.BlockCopy(allBytes, iv.Length, cipherBytes, 0, cipherBytes.Length);

            var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }

        public async Task<Result> UpdateUserAsync(UserEditDto dto)
        {
            var user = await _userRepository.GetByIdAsync(dto.Id);
            if (user == null)
            {
                return Result.Failure("User not found.");
            }

            user.FullName = dto.FullName.Trim();
            user.Role = dto.Role;
            user.Status = dto.Status;
            user.UpdatedAt = DateTime.UtcNow;

            // Update UserCode only if provided and different
            if (!string.IsNullOrWhiteSpace(dto.UserCode))
            {
                var normalizedCode = dto.UserCode.Trim().ToUpperInvariant();
                if (normalizedCode != user.UserCode)
                {
                    var codeExists = await _userRepository.UserCodeExistsAsync(normalizedCode);
                    if (codeExists)
                        return Result.Failure($"Mã '{normalizedCode}' đã tồn tại.");
                    user.UserCode = normalizedCode;
                }
            }

            await _userRepository.UpdateAsync(user);
            return Result.Success();
        }

        public async Task<Result> DeleteUserAsync(Guid id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                return Result.Failure("User not found.");
            }

            await _userRepository.DeleteAsync(user);
            return Result.Success();
        }

        public async Task<Result<int>> ImportStudentsFromExcelAsync(Stream excelStream)
        {
            try
            {
                var importedCount = 0;
                var skippedCount = 0;

                using (var doc = SpreadsheetDocument.Open(excelStream, false))
                {
                    var workbookPart = doc.WorkbookPart;
                    if (workbookPart == null) return Result<int>.Failure("Invalid Excel structure.");

                    var sstPart = workbookPart.GetPartsOfType<SharedStringTablePart>().FirstOrDefault();
                    var sst = sstPart?.SharedStringTable;

                    var sheet = workbookPart.Workbook.Sheets.GetFirstChild<Sheet>();
                    if (sheet == null || sheet.Id == null) return Result<int>.Failure("No sheets found in Excel.");

                    var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id.Value!);
                    var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
                    if (sheetData == null) return Result<int>.Failure("Worksheet has no data.");

                    var isHeader = true;
                    foreach (Row row in sheetData.Elements<Row>())
                    {
                        if (isHeader)
                        {
                            isHeader = false;
                            continue; // Skip header row
                        }

                        string fullName = string.Empty;
                        string email = string.Empty;

                        foreach (Cell cell in row.Elements<Cell>())
                        {
                            var column = GetColumnLetter(cell.CellReference?.Value ?? string.Empty);
                            var cellValue = GetCellValue(doc, cell).Trim();

                            if (column == "A") fullName = cellValue;
                            else if (column == "B") email = cellValue;
                        }

                        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
                        {
                            continue;
                        }

                        // Try to register the student
                        try
                        {
                            var userCode = await GenerateUniqueUserCodeAsync("STU");
                            var createDto = new UserCreateDto
                            {
                                Email = email,
                                FullName = fullName,
                                UserCode = userCode,
                                Role = UserRole.Student
                            };

                            var result = await CreateUserAsync(createDto);
                            if (result.IsSuccess)
                            {
                                importedCount++;
                            }
                            else
                            {
                                skippedCount++;
                                _logger.LogWarning("Skipped student row: {Email}. Reason: {Msg}", email, result.ErrorMessage);
                            }
                        }
                        catch (Exception ex)
                        {
                            skippedCount++;
                            _logger.LogError(ex, "Failed to import student row for email: {Email}", email);
                        }
                    }
                }

                _logger.LogInformation("Import summary: {Imported} imported successfully, {Skipped} skipped.", importedCount, skippedCount);
                return Result<int>.Success(importedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse Excel stream.");
                return Result<int>.Failure($"Excel parsing error: {ex.Message}");
            }
        }

        private string GetCellValue(SpreadsheetDocument doc, Cell cell)
        {
            if (cell == null || cell.CellValue == null) return string.Empty;
            
            string value = cell.CellValue.InnerText;
            if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
            {
                return doc.WorkbookPart!.SharedStringTablePart!.SharedStringTable.ChildElements[int.Parse(value)].InnerText;
            }
            return value;
        }

        private string GetColumnLetter(string cellReference)
        {
            return new string(cellReference.TakeWhile(char.IsLetter).ToArray());
        }

        private string GenerateRandomPassword()
        {
            const string chars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$";
            var passwordChars = new char[8];
            for (int i = 0; i < 8; i++)
            {
                int index = RandomNumberGenerator.GetInt32(chars.Length);
                passwordChars[i] = chars[index];
            }
            return new string(passwordChars);
        }

        private async Task<string> GenerateUniqueUserCodeAsync(string prefix)
        {
            var random = new Random();
            string code;
            do
            {
                code = prefix + random.Next(100000, 999999).ToString();
            } while (await _userRepository.UserCodeExistsAsync(code));
            
            return code;
        }

        private string HashEmail(string email)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(email);
                var hashBytes = sha256.ComputeHash(bytes);
                return Convert.ToHexString(hashBytes).ToLowerInvariant();
            }
        }

        private string EncryptEmail(string email)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(_encryptionKey);
                aes.GenerateIV();
                
                var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                var emailBytes = Encoding.UTF8.GetBytes(email);
                var encryptedBytes = encryptor.TransformFinalBlock(emailBytes, 0, emailBytes.Length);

                var resultBytes = new byte[aes.IV.Length + encryptedBytes.Length];
                Buffer.BlockCopy(aes.IV, 0, resultBytes, 0, aes.IV.Length);
                Buffer.BlockCopy(encryptedBytes, 0, resultBytes, aes.IV.Length, encryptedBytes.Length);

                return Convert.ToBase64String(resultBytes);
            }
        }
        public async Task<Result<string>> GetUserCodeAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return Result<string>.Failure("Không tìm thấy người dùng.");
            return Result<string>.Success(user.UserCode);
        }

        public async Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
                return Result.Failure("Mật khẩu mới phải có ít nhất 6 ký tự.");

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return Result.Failure("Không tìm thấy người dùng.");

            if (string.IsNullOrEmpty(currentPassword) || !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
                return Result.Failure("Mật khẩu hiện tại không chính xác.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);
            return Result.Success();
        }

        public async Task<Result> ActivateAndChangePasswordAsync(Guid userId, string? currentPassword, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
                return Result.Failure("Mật khẩu mới phải có ít nhất 6 ký tự.");

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return Result.Failure("Không tìm thấy tài khoản.");

            if (!string.IsNullOrEmpty(currentPassword) && !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
                return Result.Failure("Mật khẩu tạm thời không chính xác.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.Status = UserStatus.Active;
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);
            return Result.Success();
        }

        public async Task<IReadOnlyList<User>> GetLecturersAsync()
        {
            var all = await _userRepository.GetAllUsersAsync();
            return all
                .Where(u => u.Role == UserRole.Lecturer)
                .OrderBy(u => u.FullName)
                .ToList();
        }
    }
}
