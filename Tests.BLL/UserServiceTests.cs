using BLL.Services;
using BLL.Interfaces;
using Core.DTOs.Admin;
using Core.Entities;
using DAL.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Tests.BLL
{
    /// <summary>
    /// Unit tests for <see cref="UserService"/>.
    /// Verifies user creation validation, role-based user code rules, reset password, and update/delete.
    /// </summary>
    public class UserServiceTests
    {
        // ── Helpers ─────────────────────────────────────────────────────────────

        private static IConfiguration BuildConfig()
        {
            var inMemory = new Dictionary<string, string?>
            {
                ["Security:EncryptionKey"] = "FallbackKeyForDevExactly32Bytes!"
            };
            return new ConfigurationBuilder()
                .AddInMemoryCollection(inMemory)
                .Build();
        }

        private static (
            UserService service,
            Mock<IUserRepository> repoMock,
            Mock<IEmailService> emailMock
        ) BuildService()
        {
            var repoMock = new Mock<IUserRepository>();
            var emailMock = new Mock<IEmailService>();
            var config = BuildConfig();
            var logger = Mock.Of<ILogger<UserService>>();

            var service = new UserService(repoMock.Object, emailMock.Object, config, logger);
            return (service, repoMock, emailMock);
        }

        // ── CreateUserAsync — UserCode validation ────────────────────────────────

        [Fact]
        public async Task CreateUserAsync_EmptyUserCode_ReturnsFailure()
        {
            var (service, _, _) = BuildService();

            var dto = new UserCreateDto
            {
                Email = "test@example.com",
                FullName = "Test",
                Role = UserRole.Student,
                UserCode = ""
            };

            var result = await service.CreateUserAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("Mã người dùng", result.ErrorMessage);
        }

        [Fact]
        public async Task CreateUserAsync_LecturerCodeNotStartingWithLEC_ReturnsFailure()
        {
            var (service, _, _) = BuildService();

            var dto = new UserCreateDto
            {
                Email = "lecturer@example.com",
                FullName = "Dr. Test",
                Role = UserRole.Lecturer,
                UserCode = "STU001" // wrong prefix
            };

            var result = await service.CreateUserAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("LEC", result.ErrorMessage);
        }

        [Fact]
        public async Task CreateUserAsync_LecturerCodeTooShort_ReturnsFailure()
        {
            var (service, _, _) = BuildService();

            var dto = new UserCreateDto
            {
                Email = "lecturer@example.com",
                FullName = "Dr. Test",
                Role = UserRole.Lecturer,
                UserCode = "LEC" // exactly 3 chars — too short (min 4)
            };

            var result = await service.CreateUserAsync(dto);

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task CreateUserAsync_StudentCodeWithInvalidPrefix_ReturnsFailure()
        {
            var (service, _, _) = BuildService();

            var dto = new UserCreateDto
            {
                Email = "student@example.com",
                FullName = "Student One",
                Role = UserRole.Student,
                UserCode = "LEC001" // wrong for student
            };

            var result = await service.CreateUserAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("HE", result.ErrorMessage);
        }

        [Fact]
        public async Task CreateUserAsync_AdminCodeNotStartingWithADM_ReturnsFailure()
        {
            var (service, _, _) = BuildService();

            var dto = new UserCreateDto
            {
                Email = "admin@example.com",
                FullName = "Admin User",
                Role = UserRole.Admin,
                UserCode = "MGR001"
            };

            var result = await service.CreateUserAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("ADM", result.ErrorMessage);
        }

        // ── CreateUserAsync — duplicate checks ───────────────────────────────────

        [Fact]
        public async Task CreateUserAsync_DuplicateEmail_ReturnsFailure()
        {
            var (service, repoMock, _) = BuildService();

            repoMock
                .Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync(new User { Id = Guid.NewGuid() });

            var dto = new UserCreateDto
            {
                Email = "existing@example.com",
                FullName = "Existing",
                Role = UserRole.Student,
                UserCode = "HE170001"
            };

            var result = await service.CreateUserAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("Email", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateUserAsync_DuplicateUserCode_ReturnsFailure()
        {
            var (service, repoMock, _) = BuildService();

            repoMock
                .Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            repoMock
                .Setup(r => r.UserCodeExistsAsync("HE170001"))
                .ReturnsAsync(true);

            var dto = new UserCreateDto
            {
                Email = "new@example.com",
                FullName = "New User",
                Role = UserRole.Student,
                UserCode = "HE170001"
            };

            var result = await service.CreateUserAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("HE170001", result.ErrorMessage);
        }

        // ── CreateUserAsync — happy path ─────────────────────────────────────────

        [Fact]
        public async Task CreateUserAsync_ValidStudentDto_CreatesUserAndReturnsSuccess()
        {
            var (service, repoMock, emailMock) = BuildService();

            repoMock
                .Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            repoMock
                .Setup(r => r.UserCodeExistsAsync(It.IsAny<string>()))
                .ReturnsAsync(false);

            repoMock
                .Setup(r => r.AddUserAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) => u);

            emailMock
                .Setup(e => e.SendFirstTimeLoginEmailAsync(
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var dto = new UserCreateDto
            {
                Email = "student@example.com",
                FullName = "Student Test",
                Role = UserRole.Student,
                UserCode = "HE170999"
            };

            var result = await service.CreateUserAsync(dto);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(UserStatus.Inactive, result.Data!.Status);
            Assert.Equal("HE170999", result.Data.UserCode);

            repoMock.Verify(r => r.AddUserAsync(It.IsAny<User>()), Times.Once);
        }

        [Fact]
        public async Task CreateUserAsync_ValidLecturerDto_CreatesUserSuccessfully()
        {
            var (service, repoMock, emailMock) = BuildService();

            repoMock
                .Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            repoMock
                .Setup(r => r.UserCodeExistsAsync(It.IsAny<string>()))
                .ReturnsAsync(false);

            repoMock
                .Setup(r => r.AddUserAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) => u);

            emailMock
                .Setup(e => e.SendFirstTimeLoginEmailAsync(
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var dto = new UserCreateDto
            {
                Email = "lecturer@example.com",
                FullName = "Dr. Lecturer",
                Role = UserRole.Lecturer,
                UserCode = "LEC001"
            };

            var result = await service.CreateUserAsync(dto);

            Assert.True(result.IsSuccess);
            Assert.Equal(UserRole.Lecturer, result.Data!.Role);
        }

        [Fact]
        public async Task CreateUserAsync_EmailSendFails_StillReturnsSuccess()
        {
            // Email sending is best-effort — even if it fails, user should be created
            var (service, repoMock, emailMock) = BuildService();

            repoMock
                .Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            repoMock
                .Setup(r => r.UserCodeExistsAsync(It.IsAny<string>()))
                .ReturnsAsync(false);

            repoMock
                .Setup(r => r.AddUserAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) => u);

            emailMock
                .Setup(e => e.SendFirstTimeLoginEmailAsync(
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("SMTP error"));

            var dto = new UserCreateDto
            {
                Email = "student@example.com",
                FullName = "Email Fail Test",
                Role = UserRole.Student,
                UserCode = "STU00001"
            };

            var result = await service.CreateUserAsync(dto);

            // User was still created even though email failed
            Assert.True(result.IsSuccess);
        }

        // ── UpdateUserAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task UpdateUserAsync_UserNotFound_ReturnsFailure()
        {
            var (service, repoMock, _) = BuildService();

            repoMock
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((User?)null);

            var dto = new UserEditDto
            {
                Id = Guid.NewGuid(),
                FullName = "Updated Name",
                Role = UserRole.Student,
                Status = UserStatus.Active
            };

            var result = await service.UpdateUserAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Equal("User not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task UpdateUserAsync_DuplicateUserCode_ReturnsFailure()
        {
            var (service, repoMock, _) = BuildService();

            var user = new User
            {
                Id = Guid.NewGuid(),
                UserCode = "HE000001",
                FullName = "Old Name",
                Role = UserRole.Student,
                Status = UserStatus.Active
            };

            repoMock
                .Setup(r => r.GetByIdAsync(user.Id))
                .ReturnsAsync(user);

            repoMock
                .Setup(r => r.UserCodeExistsAsync("HE000002"))
                .ReturnsAsync(true); // taken

            var dto = new UserEditDto
            {
                Id = user.Id,
                FullName = "New Name",
                Role = UserRole.Student,
                Status = UserStatus.Active,
                UserCode = "HE000002"
            };

            var result = await service.UpdateUserAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("HE000002", result.ErrorMessage);
        }

        [Fact]
        public async Task UpdateUserAsync_ValidDto_UpdatesUserAndReturnsSuccess()
        {
            var (service, repoMock, _) = BuildService();

            var user = new User
            {
                Id = Guid.NewGuid(),
                UserCode = "HE000001",
                FullName = "Original Name",
                Role = UserRole.Student,
                Status = UserStatus.Active
            };

            repoMock
                .Setup(r => r.GetByIdAsync(user.Id))
                .ReturnsAsync(user);

            repoMock
                .Setup(r => r.UpdateAsync(It.IsAny<User>()))
                .Returns(Task.CompletedTask);

            var dto = new UserEditDto
            {
                Id = user.Id,
                FullName = "Updated Name",
                Role = UserRole.Lecturer,
                Status = UserStatus.Inactive
            };

            var result = await service.UpdateUserAsync(dto);

            Assert.True(result.IsSuccess);
            Assert.Equal("Updated Name", user.FullName);
            Assert.Equal(UserRole.Lecturer, user.Role);
            Assert.Equal(UserStatus.Inactive, user.Status);
        }

        // ── DeleteUserAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteUserAsync_UserNotFound_ReturnsFailure()
        {
            var (service, repoMock, _) = BuildService();

            repoMock
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((User?)null);

            var result = await service.DeleteUserAsync(Guid.NewGuid());

            Assert.False(result.IsSuccess);
            Assert.Equal("User not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task DeleteUserAsync_ExistingUser_DeletesAndReturnsSuccess()
        {
            var (service, repoMock, _) = BuildService();

            var user = new User { Id = Guid.NewGuid() };

            repoMock
                .Setup(r => r.GetByIdAsync(user.Id))
                .ReturnsAsync(user);

            repoMock
                .Setup(r => r.DeleteAsync(It.IsAny<User>()))
                .Returns(Task.CompletedTask);

            var result = await service.DeleteUserAsync(user.Id);

            Assert.True(result.IsSuccess);
            repoMock.Verify(r => r.DeleteAsync(user), Times.Once);
        }

        // ── ResetPasswordAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task ResetPasswordAsync_UserNotFound_ReturnsFailure()
        {
            var (service, repoMock, _) = BuildService();

            repoMock
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((User?)null);

            var result = await service.ResetPasswordAsync(Guid.NewGuid());

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task ResetPasswordAsync_ExistingUser_ResetsPasswordAndSetsInactive()
        {
            var (service, repoMock, emailMock) = BuildService();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Status = UserStatus.Active,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("oldpass"),
                EmailEncrypt = null // no email to send to
            };

            repoMock
                .Setup(r => r.GetByIdAsync(user.Id))
                .ReturnsAsync(user);

            repoMock
                .Setup(r => r.UpdateAsync(It.IsAny<User>()))
                .Returns(Task.CompletedTask);

            var result = await service.ResetPasswordAsync(user.Id);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.NotEmpty(result.Data!);
            Assert.Equal(UserStatus.Inactive, user.Status);

            // New password should be verifiable (bcrypt)
            Assert.True(BCrypt.Net.BCrypt.Verify(result.Data, user.PasswordHash));
        }
    }
}
