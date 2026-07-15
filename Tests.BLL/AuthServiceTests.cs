using BLL.Interfaces;
using BLL.Services;
using Core.DTOs.Auth;
using Core.Entities;
using DAL.Interfaces;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Tests.BLL
{
    /// <summary>
    /// Unit tests for <see cref="AuthService"/>.
    /// Uses Moq to isolate from the database and IConfiguration.
    /// </summary>
    public class AuthServiceTests
    {
        // ── Helpers ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates an IConfiguration mock that returns a valid 32-byte AES key.
        /// </summary>
        private static IConfiguration BuildConfig(string key = "FallbackKeyForDevExactly32Bytes!")
        {
            var inMemory = new Dictionary<string, string?>
            {
                ["Security:EncryptionKey"] = key
            };
            return new ConfigurationBuilder()
                .AddInMemoryCollection(inMemory)
                .Build();
        }

        /// <summary>
        /// Creates an AuthService with mocked repository and config.
        /// </summary>
        private static (AuthService service, Mock<IUserRepository> repoMock) BuildService()
        {
            var repoMock = new Mock<IUserRepository>();
            var emailMock = new Mock<IEmailService>();
            var config = BuildConfig();
            var service = new AuthService(repoMock.Object, emailMock.Object, config);
            return (service, repoMock);
        }

        // ── Constructor ─────────────────────────────────────────────────────────

        [Fact]
        public void Constructor_ValidKey_DoesNotThrow()
        {
            var repoMock = new Mock<IUserRepository>();
            var emailMock = new Mock<IEmailService>();
            var config = BuildConfig("FallbackKeyForDevExactly32Bytes!");

            // Should not throw
            var service = new AuthService(repoMock.Object, emailMock.Object, config);
            Assert.NotNull(service);
        }

        [Fact]
        public void Constructor_InvalidKeyLength_ThrowsInvalidOperationException()
        {
            var repoMock = new Mock<IUserRepository>();
            var emailMock = new Mock<IEmailService>();
            var config = BuildConfig("short"); // not 32 bytes

            Assert.Throws<InvalidOperationException>(() =>
                new AuthService(repoMock.Object, emailMock.Object, config));
        }

        // ── RegisterAsync ────────────────────────────────────────────────────────

        [Fact]
        public async Task RegisterAsync_EmailAlreadyExists_ReturnsFailure()
        {
            var (service, repoMock) = BuildService();

            var existingUser = new User { Id = Guid.NewGuid(), FullName = "Existing" };
            repoMock
                .Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync(existingUser);

            var dto = new RegisterDto
            {
                Email = "test@example.com",
                Password = "password123",
                ConfirmPassword = "password123",
                FullName = "Test User",
                Role = UserRole.Student
            };

            var result = await service.RegisterAsync(dto);

            Assert.False(result.Success);
            Assert.Equal("Email is already registered.", result.ErrorMessage);
        }

        [Fact]
        public async Task RegisterAsync_NewEmail_CreatesUserAndReturnsSuccess()
        {
            var (service, repoMock) = BuildService();

            // No existing user
            repoMock
                .Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            repoMock
                .Setup(r => r.AddUserAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) => u);

            var dto = new RegisterDto
            {
                Email = "newuser@example.com",
                Password = "SecurePass1",
                ConfirmPassword = "SecurePass1",
                FullName = "New User",
                Role = UserRole.Student
            };

            var result = await service.RegisterAsync(dto);

            Assert.True(result.Success);
            Assert.Equal(string.Empty, result.ErrorMessage);

            // Verify AddUserAsync was called once
            repoMock.Verify(r => r.AddUserAsync(It.IsAny<User>()), Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_DatabaseThrowsOnGetHash_ReturnsFailure()
        {
            var (service, repoMock) = BuildService();

            repoMock
                .Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("DB error"));

            var dto = new RegisterDto
            {
                Email = "test@example.com",
                Password = "password123",
                ConfirmPassword = "password123",
                FullName = "Test User",
                Role = UserRole.Student
            };

            var result = await service.RegisterAsync(dto);

            Assert.False(result.Success);
            Assert.Contains("database connection error", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RegisterAsync_DatabaseThrowsOnAdd_ReturnsFailure()
        {
            var (service, repoMock) = BuildService();

            repoMock
                .Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            repoMock
                .Setup(r => r.AddUserAsync(It.IsAny<User>()))
                .ThrowsAsync(new Exception("DB insert error"));

            var dto = new RegisterDto
            {
                Email = "test@example.com",
                Password = "password123",
                ConfirmPassword = "password123",
                FullName = "Test User",
                Role = UserRole.Student
            };

            var result = await service.RegisterAsync(dto);

            Assert.False(result.Success);
            Assert.Contains("error occurred", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        // ── LoginAsync ───────────────────────────────────────────────────────────

        [Fact]
        public async Task LoginAsync_UserNotFound_ReturnsNull()
        {
            var (service, repoMock) = BuildService();

            repoMock
                .Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var dto = new LoginDto { Email = "notfound@example.com", Password = "pass" };

            var result = await service.LoginAsync(dto);

            Assert.Null(result);
        }

        [Fact]
        public async Task LoginAsync_WrongPassword_ReturnsNull()
        {
            var (service, repoMock) = BuildService();

            var hash = BCrypt.Net.BCrypt.HashPassword("correctpassword");
            var existingUser = new User { Id = Guid.NewGuid(), PasswordHash = hash };

            repoMock
                .Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync(existingUser);

            var dto = new LoginDto { Email = "user@example.com", Password = "wrongpassword" };

            var result = await service.LoginAsync(dto);

            Assert.Null(result);
        }

        [Fact]
        public async Task LoginAsync_CorrectCredentials_ReturnsUser()
        {
            var (service, repoMock) = BuildService();

            const string plainPassword = "MySecurePass1!";
            var hash = BCrypt.Net.BCrypt.HashPassword(plainPassword);

            var existingUser = new User
            {
                Id = Guid.NewGuid(),
                FullName = "Test User",
                PasswordHash = hash
            };

            repoMock
                .Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync(existingUser);

            var dto = new LoginDto { Email = "user@example.com", Password = plainPassword };

            var result = await service.LoginAsync(dto);

            Assert.NotNull(result);
            Assert.Equal(existingUser.Id, result.Id);
        }

        [Fact]
        public async Task LoginAsync_DatabaseThrows_ReturnsNull()
        {
            var (service, repoMock) = BuildService();

            repoMock
                .Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("DB error"));

            var dto = new LoginDto { Email = "user@example.com", Password = "pass" };

            var result = await service.LoginAsync(dto);

            Assert.Null(result);
        }

        // ── LoginByCodeAsync ─────────────────────────────────────────────────────

        [Fact]
        public async Task LoginByCodeAsync_EmptyCode_ReturnsNull()
        {
            var (service, _) = BuildService();

            var result = await service.LoginByCodeAsync("", "password");
            Assert.Null(result);
        }

        [Fact]
        public async Task LoginByCodeAsync_NullPassword_ReturnsNull()
        {
            var (service, _) = BuildService();

            var result = await service.LoginByCodeAsync("STU001", null!);
            Assert.Null(result);
        }

        [Fact]
        public async Task LoginByCodeAsync_UserNotFound_ReturnsNull()
        {
            var (service, repoMock) = BuildService();

            repoMock
                .Setup(r => r.GetByUserCodeAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await service.LoginByCodeAsync("STU999", "password");
            Assert.Null(result);
        }

        [Fact]
        public async Task LoginByCodeAsync_CorrectCodeAndPassword_ReturnsUser()
        {
            var (service, repoMock) = BuildService();

            const string password = "Pass@123";
            var user = new User
            {
                Id = Guid.NewGuid(),
                UserCode = "STU001",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
            };

            repoMock
                .Setup(r => r.GetByUserCodeAsync("STU001"))
                .ReturnsAsync(user);

            var result = await service.LoginByCodeAsync("STU001", password);

            Assert.NotNull(result);
            Assert.Equal("STU001", result.UserCode);
        }

        // ── ActivateAccountAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task ActivateAccountAsync_UserNotFound_ReturnsFailure()
        {
            var (service, repoMock) = BuildService();

            repoMock
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((User?)null);

            var result = await service.ActivateAccountAsync(Guid.NewGuid(), "tempPass", "newPass");

            Assert.False(result.IsSuccess);
            Assert.Equal("User not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task ActivateAccountAsync_WrongTemporaryPassword_ReturnsFailure()
        {
            var (service, repoMock) = BuildService();

            var user = new User
            {
                Id = Guid.NewGuid(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("correctTemp")
            };

            repoMock
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(user);

            var result = await service.ActivateAccountAsync(user.Id, "wrongTemp", "newPassword");

            Assert.False(result.IsSuccess);
            Assert.Equal("Invalid temporary password.", result.ErrorMessage);
        }

        [Fact]
        public async Task ActivateAccountAsync_ValidCredentials_ActivatesUserAndReturnsSuccess()
        {
            var (service, repoMock) = BuildService();

            const string tempPassword = "TempPass1";
            var user = new User
            {
                Id = Guid.NewGuid(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword),
                Status = UserStatus.Inactive
            };

            repoMock
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(user);

            repoMock
                .Setup(r => r.UpdateAsync(It.IsAny<User>()))
                .Returns(Task.CompletedTask);

            var result = await service.ActivateAccountAsync(user.Id, tempPassword, "NewPass123!");

            Assert.True(result.IsSuccess);
            Assert.Equal(UserStatus.Active, user.Status);
            repoMock.Verify(r => r.UpdateAsync(It.Is<User>(u => u.Status == UserStatus.Active)), Times.Once);
        }

        // ── AdminResetPasswordAsync ──────────────────────────────────────────────

        [Fact]
        public async Task AdminResetPasswordAsync_UserNotFound_ReturnsFailure()
        {
            var (service, repoMock) = BuildService();

            repoMock
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((User?)null);

            var result = await service.AdminResetPasswordAsync(Guid.NewGuid());

            Assert.False(result.IsSuccess);
            Assert.Equal("User not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task AdminResetPasswordAsync_ValidUser_ResetsPasswordAndSetsInactive()
        {
            var (service, repoMock) = BuildService();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Status = UserStatus.Active,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("oldPassword")
            };

            repoMock
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(user);

            repoMock
                .Setup(r => r.UpdateAsync(It.IsAny<User>()))
                .Returns(Task.CompletedTask);

            var result = await service.AdminResetPasswordAsync(user.Id);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.NotEmpty(result.Data);

            // Ensure user is set to Inactive (forced change on next login)
            Assert.Equal(UserStatus.Inactive, user.Status);
        }

        // ── DecryptEmail ─────────────────────────────────────────────────────────

        [Fact]
        public async Task DecryptEmail_RoundTrip_ReturnsOriginalEmail()
        {
            var repoMock = new Mock<IUserRepository>();
            var emailMock = new Mock<IEmailService>();
            var config = BuildConfig();
            var service = new AuthService(repoMock.Object, emailMock.Object, config);

            // Capture the encrypted email stored during RegisterAsync
            const string originalEmail = "roundtrip@example.com";

            User? capturedUser = null;
            repoMock
                .Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);
            repoMock
                .Setup(r => r.AddUserAsync(It.IsAny<User>()))
                .Callback<User>(u => capturedUser = u)
                .ReturnsAsync((User u) => u);

            await service.RegisterAsync(new RegisterDto
            {
                Email = originalEmail,
                Password = "pass123456",
                ConfirmPassword = "pass123456",
                FullName = "Round Trip",
                Role = UserRole.Student
            });

            Assert.NotNull(capturedUser);
            Assert.NotNull(capturedUser!.EmailEncrypt);

            // Now decrypt and verify
            var decrypted = service.DecryptEmail(capturedUser.EmailEncrypt!);
            Assert.Equal(originalEmail, decrypted);
        }

        [Fact]
        public void DecryptEmail_InvalidBase64_ThrowsInvalidOperationException()
        {
            var (service, _) = BuildService();

            Assert.Throws<InvalidOperationException>(() =>
                service.DecryptEmail("this-is-not-valid-base64!!!"));
        }

        // ── ForgotPasswordAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task ForgotPasswordAsync_EmptyEmail_ReturnsFailure()
        {
            var (service, _) = BuildService();
            var result = await service.ForgotPasswordAsync("");
            Assert.False(result.IsSuccess);
            Assert.Equal("Email không được để trống.", result.ErrorMessage);
        }

        [Fact]
        public async Task ForgotPasswordAsync_UserNotFound_ReturnsSuccess()
        {
            var (service, repoMock) = BuildService();
            repoMock
                .Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await service.ForgotPasswordAsync("notfound@example.com");
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task ForgotPasswordAsync_UserFound_UpdatesUserAndSendsEmail()
        {
            var repoMock = new Mock<IUserRepository>();
            var emailMock = new Mock<IEmailService>();
            var config = BuildConfig();
            var service = new AuthService(repoMock.Object, emailMock.Object, config);

            var user = new User
            {
                Id = Guid.NewGuid(),
                FullName = "Found User",
                PasswordHash = "oldhash",
                Status = UserStatus.Active
            };

            repoMock
                .Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            repoMock
                .Setup(r => r.UpdateAsync(It.IsAny<User>()))
                .Returns(Task.CompletedTask);

            emailMock
                .Setup(e => e.SendPasswordResetNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var result = await service.ForgotPasswordAsync("found@example.com");

            Assert.True(result.IsSuccess);
            Assert.Equal(UserStatus.Inactive, user.Status);
            repoMock.Verify(r => r.UpdateAsync(It.Is<User>(u => u.Status == UserStatus.Inactive)), Times.Once);
            emailMock.Verify(e => e.SendPasswordResetNotificationAsync("found@example.com", "Found User", It.IsAny<string>()), Times.Once);
        }
    }
}
