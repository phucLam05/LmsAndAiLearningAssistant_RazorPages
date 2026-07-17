using BLL.Services;
using Core.DTOs.Admin;
using Core.Entities;
using DAL.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Linq;

namespace Tests.BLL
{
    /// <summary>
    /// Extended unit tests for <see cref="UserService"/> covering user creation validation,
    /// update logic, deletion, password management, and lecturer queries.
    /// Complements the existing UserServiceTests.cs with deeper coverage.
    /// </summary>
    public class UserServiceExtendedTests
    {
        // ── Helpers ─────────────────────────────────────────────────────────────

        private static IConfiguration BuildConfig(string key = "FallbackKeyForDevExactly32Bytes!")
        {
            var inMemory = new Dictionary<string, string?>
            {
                ["Security:EncryptionKey"] = key
            };
            return new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        }

        private static (
            UserService service,
            Mock<IUserRepository> userRepoMock,
            Mock<IEmailService> emailServiceMock
        ) BuildService()
        {
            var userRepo     = new Mock<IUserRepository>();
            var emailService = new Mock<IEmailService>();
            var logger       = Mock.Of<ILogger<UserService>>();
            var config       = BuildConfig();

            var service = new UserService(
                userRepo.Object,
                emailService.Object,
                config,
                logger);

            return (service, userRepo, emailService);
        }

        private static User MakeUser(
            UserRole role   = UserRole.Student,
            UserStatus status = UserStatus.Active,
            Guid? id        = null,
            string userCode = "HE170001",
            string fullName = "Nguyen Van A",
            string passwordHash = "")
            => new User
            {
                Id           = id ?? Guid.NewGuid(),
                UserCode     = userCode,
                FullName     = fullName,
                Role         = role,
                Status       = status,
                PasswordHash = string.IsNullOrEmpty(passwordHash)
                    ? BCrypt.Net.BCrypt.HashPassword("Password@123")
                    : passwordHash,
                EmailEncrypt = "",
                EmailHash    = "somehash",
                CreatedAt    = DateTime.UtcNow,
                UpdatedAt    = DateTime.UtcNow
            };

        // ── CreateUserAsync — UserCode validation ────────────────────────────────

        [Fact]
        public async Task CreateUserAsync_EmptyUserCode_ReturnsFailure()
        {
            var (service, _, _) = BuildService();

            var dto = new UserCreateDto
            {
                UserCode  = "",
                FullName  = "Test User",
                Email     = "test@fpt.edu.vn",
                Role      = UserRole.Student
            };

            var result = await service.CreateUserAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("Mã người dùng", result.ErrorMessage);
        }

        [Fact]
        public async Task CreateUserAsync_WhitespaceUserCode_ReturnsFailure()
        {
            var (service, _, _) = BuildService();

            var dto = new UserCreateDto
            {
                UserCode  = "   ",
                FullName  = "Test User",
                Email     = "test@fpt.edu.vn",
                Role      = UserRole.Student
            };

            var result = await service.CreateUserAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("Mã người dùng", result.ErrorMessage);
        }

        // ── CreateUserAsync — Lecturer code validation ────────────────────────────

        [Fact]
        public async Task CreateUserAsync_LecturerCodeMissingLECPrefix_ReturnsFailure()
        {
            var (service, _, _) = BuildService();

            var dto = new UserCreateDto
            {
                UserCode  = "PRF001",
                FullName  = "Dr. Smith",
                Email     = "smith@fpt.edu.vn",
                Role      = UserRole.Lecturer
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
                UserCode  = "LEC", // only 3 chars
                FullName  = "Dr. Smith",
                Email     = "smith@fpt.edu.vn",
                Role      = UserRole.Lecturer
            };

            var result = await service.CreateUserAsync(dto);

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task CreateUserAsync_ValidLecturerCode_PassesCodeValidation()
        {
            var (service, userRepo, emailService) = BuildService();

            userRepo
                .Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            userRepo
                .Setup(r => r.UserCodeExistsAsync(It.IsAny<string>()))
                .ReturnsAsync(false);

            userRepo
                .Setup(r => r.AddUserAsync(It.IsAny<User>()))
                .Returns(Task.CompletedTask);

            emailService
                .Setup(e => e.SendFirstTimeLoginEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var dto = new UserCreateDto
            {
                UserCode  = "LEC001",
                FullName  = "Dr. Smith",
                Email     = "smith@fpt.edu.vn",
                Role      = UserRole.Lecturer
            };

            var result = await service.CreateUserAsync(dto);

            Assert.True(result.IsSuccess);
        }

        // ── CreateUserAsync — Student code validation ─────────────────────────────

        [Fact]
        public async Task CreateUserAsync_StudentCodeWithHEPrefix_PassesCodeValidation()
        {
            var (service, userRepo, emailService) = BuildService();

            userRepo.Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
            userRepo.Setup(r => r.UserCodeExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            userRepo.Setup(r => r.AddUserAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            emailService.Setup(e => e.SendFirstTimeLoginEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            var dto = new UserCreateDto
            {
                UserCode  = "HE170123",
                FullName  = "Nguyen Van A",
                Email     = "a.nh@fpt.edu.vn",
                Role      = UserRole.Student
            };

            var result = await service.CreateUserAsync(dto);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task CreateUserAsync_StudentCodeWithSTUPrefix_PassesCodeValidation()
        {
            var (service, userRepo, emailService) = BuildService();

            userRepo.Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
            userRepo.Setup(r => r.UserCodeExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            userRepo.Setup(r => r.AddUserAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            emailService.Setup(e => e.SendFirstTimeLoginEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            var dto = new UserCreateDto
            {
                UserCode  = "STU20001",
                FullName  = "Tran Thi B",
                Email     = "b.tt@student.fpt.edu.vn",
                Role      = UserRole.Student
            };

            var result = await service.CreateUserAsync(dto);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task CreateUserAsync_StudentCodeWithInvalidPrefix_ReturnsFailure()
        {
            var (service, _, _) = BuildService();

            var dto = new UserCreateDto
            {
                UserCode  = "SV20001",
                FullName  = "Tran Thi B",
                Email     = "b.tt@fpt.edu.vn",
                Role      = UserRole.Student
            };

            var result = await service.CreateUserAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("HE", result.ErrorMessage);
        }

        // ── CreateUserAsync — Admin code validation ───────────────────────────────

        [Fact]
        public async Task CreateUserAsync_AdminCodeWithADMPrefix_PassesCodeValidation()
        {
            var (service, userRepo, emailService) = BuildService();

            userRepo.Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
            userRepo.Setup(r => r.UserCodeExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            userRepo.Setup(r => r.AddUserAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            emailService.Setup(e => e.SendFirstTimeLoginEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            var dto = new UserCreateDto
            {
                UserCode  = "ADM001",
                FullName  = "Super Admin",
                Email     = "admin@fpt.edu.vn",
                Role      = UserRole.Admin
            };

            var result = await service.CreateUserAsync(dto);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task CreateUserAsync_AdminCodeWithoutADMPrefix_ReturnsFailure()
        {
            var (service, _, _) = BuildService();

            var dto = new UserCreateDto
            {
                UserCode  = "MGR001",
                FullName  = "Admin User",
                Email     = "admin@fpt.edu.vn",
                Role      = UserRole.Admin
            };

            var result = await service.CreateUserAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("ADM", result.ErrorMessage);
        }

        // ── CreateUserAsync — duplicate email ─────────────────────────────────────

        [Fact]
        public async Task CreateUserAsync_DuplicateEmail_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();

            userRepo
                .Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync(MakeUser());

            var dto = new UserCreateDto
            {
                UserCode  = "HE170123",
                FullName  = "Nguyen Van A",
                Email     = "existing@fpt.edu.vn",
                Role      = UserRole.Student
            };

            var result = await service.CreateUserAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("Email", result.ErrorMessage);
        }

        // ── CreateUserAsync — duplicate UserCode ──────────────────────────────────

        [Fact]
        public async Task CreateUserAsync_DuplicateUserCode_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();

            userRepo
                .Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            userRepo
                .Setup(r => r.UserCodeExistsAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            var dto = new UserCreateDto
            {
                UserCode  = "HE170123",
                FullName  = "Nguyen Van A",
                Email     = "a.nh@fpt.edu.vn",
                Role      = UserRole.Student
            };

            var result = await service.CreateUserAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("HE170123", result.ErrorMessage);
        }

        // ── CreateUserAsync — user code normalization ─────────────────────────────

        [Fact]
        public async Task CreateUserAsync_LowercaseUserCode_NormalizesToUppercase()
        {
            var (service, userRepo, emailService) = BuildService();

            User? capturedUser = null;
            userRepo.Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
            userRepo.Setup(r => r.UserCodeExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            userRepo
                .Setup(r => r.AddUserAsync(It.IsAny<User>()))
                .Callback<User>(u => capturedUser = u)
                .Returns(Task.CompletedTask);
            emailService.Setup(e => e.SendFirstTimeLoginEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            var dto = new UserCreateDto
            {
                UserCode  = "he170001",
                FullName  = "Nguyen Van A",
                Email     = "a.nh@fpt.edu.vn",
                Role      = UserRole.Student
            };

            await service.CreateUserAsync(dto);

            Assert.NotNull(capturedUser);
            Assert.Equal("HE170001", capturedUser!.UserCode);
        }

        // ── CreateUserAsync — new user properties ─────────────────────────────────

        [Fact]
        public async Task CreateUserAsync_NewUser_StatusIsInactive()
        {
            var (service, userRepo, emailService) = BuildService();

            User? capturedUser = null;
            userRepo.Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
            userRepo.Setup(r => r.UserCodeExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            userRepo
                .Setup(r => r.AddUserAsync(It.IsAny<User>()))
                .Callback<User>(u => capturedUser = u)
                .Returns(Task.CompletedTask);
            emailService.Setup(e => e.SendFirstTimeLoginEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            var dto = new UserCreateDto
            {
                UserCode  = "HE170001",
                FullName  = "Nguyen Van A",
                Email     = "a.nh@fpt.edu.vn",
                Role      = UserRole.Student
            };

            await service.CreateUserAsync(dto);

            Assert.Equal(UserStatus.Inactive, capturedUser!.Status);
        }

        [Fact]
        public async Task CreateUserAsync_NewUser_HasValidPasswordHash()
        {
            var (service, userRepo, emailService) = BuildService();

            User? capturedUser = null;
            userRepo.Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
            userRepo.Setup(r => r.UserCodeExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            userRepo
                .Setup(r => r.AddUserAsync(It.IsAny<User>()))
                .Callback<User>(u => capturedUser = u)
                .Returns(Task.CompletedTask);
            emailService.Setup(e => e.SendFirstTimeLoginEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            var dto = new UserCreateDto
            {
                UserCode  = "HE170001",
                FullName  = "Test User",
                Email     = "test@fpt.edu.vn",
                Role      = UserRole.Student
            };

            await service.CreateUserAsync(dto);

            Assert.NotNull(capturedUser);
            Assert.NotEmpty(capturedUser!.PasswordHash);
            // Password should be a valid BCrypt hash
            Assert.True(capturedUser.PasswordHash.StartsWith("$2"));
        }

        [Fact]
        public async Task CreateUserAsync_NewUser_IdIsNotEmpty()
        {
            var (service, userRepo, emailService) = BuildService();

            User? capturedUser = null;
            userRepo.Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
            userRepo.Setup(r => r.UserCodeExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            userRepo
                .Setup(r => r.AddUserAsync(It.IsAny<User>()))
                .Callback<User>(u => capturedUser = u)
                .Returns(Task.CompletedTask);
            emailService.Setup(e => e.SendFirstTimeLoginEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            var dto = new UserCreateDto
            {
                UserCode  = "HE170001",
                FullName  = "Test User",
                Email     = "test@fpt.edu.vn",
                Role      = UserRole.Student
            };

            await service.CreateUserAsync(dto);

            Assert.NotEqual(Guid.Empty, capturedUser!.Id);
        }

        // ── CreateUserAsync — email sending ───────────────────────────────────────

        [Fact]
        public async Task CreateUserAsync_EmailSendFails_StillReturnsSuccess()
        {
            var (service, userRepo, emailService) = BuildService();

            userRepo.Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
            userRepo.Setup(r => r.UserCodeExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            userRepo.Setup(r => r.AddUserAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            emailService
                .Setup(e => e.SendFirstTimeLoginEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("SMTP server not configured"));

            var dto = new UserCreateDto
            {
                UserCode  = "HE170001",
                FullName  = "Test User",
                Email     = "test@fpt.edu.vn",
                Role      = UserRole.Student
            };

            var result = await service.CreateUserAsync(dto);

            // Email failure should be swallowed — user still created
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task CreateUserAsync_ValidDto_SendsFirstTimeLoginEmail()
        {
            var (service, userRepo, emailService) = BuildService();

            userRepo.Setup(r => r.GetUserByEmailHashAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
            userRepo.Setup(r => r.UserCodeExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            userRepo.Setup(r => r.AddUserAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            emailService.Setup(e => e.SendFirstTimeLoginEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            var dto = new UserCreateDto
            {
                UserCode  = "HE170001",
                FullName  = "Test User",
                Email     = "test@fpt.edu.vn",
                Role      = UserRole.Student
            };

            await service.CreateUserAsync(dto);

            emailService.Verify(
                e => e.SendFirstTimeLoginEmailAsync(
                    It.Is<string>(email => email == "test@fpt.edu.vn"),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()),
                Times.Once);
        }

        // ── UpdateUserAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task UpdateUserAsync_UserNotFound_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();

            userRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((User?)null);

            var dto = new UserEditDto
            {
                Id       = Guid.NewGuid(),
                FullName = "New Name",
                Role     = UserRole.Student,
                Status   = UserStatus.Active
            };

            var result = await service.UpdateUserAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Equal("User not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task UpdateUserAsync_ValidUser_UpdatesFieldsAndReturnsSuccess()
        {
            var (service, userRepo, _) = BuildService();

            var user = MakeUser(role: UserRole.Student, status: UserStatus.Active, userCode: "HE170001", fullName: "Old Name");
            userRepo
                .Setup(r => r.GetByIdAsync(user.Id))
                .ReturnsAsync(user);

            userRepo
                .Setup(r => r.UpdateAsync(It.IsAny<User>()))
                .Returns(Task.CompletedTask);

            var dto = new UserEditDto
            {
                Id       = user.Id,
                FullName = "New Name",
                Role     = UserRole.Lecturer,
                Status   = UserStatus.Inactive
            };

            var result = await service.UpdateUserAsync(dto);

            Assert.True(result.IsSuccess);
            Assert.Equal("New Name", user.FullName);
            Assert.Equal(UserRole.Lecturer, user.Role);
            Assert.Equal(UserStatus.Inactive, user.Status);
        }

        [Fact]
        public async Task UpdateUserAsync_NewUserCodeAlreadyTaken_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();

            var user = MakeUser(userCode: "HE170001");
            userRepo
                .Setup(r => r.GetByIdAsync(user.Id))
                .ReturnsAsync(user);

            userRepo
                .Setup(r => r.UserCodeExistsAsync("HE170002"))
                .ReturnsAsync(true);

            var dto = new UserEditDto
            {
                Id       = user.Id,
                FullName = user.FullName,
                UserCode = "HE170002",
                Role     = user.Role,
                Status   = user.Status
            };

            var result = await service.UpdateUserAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("HE170002", result.ErrorMessage);
        }

        [Fact]
        public async Task UpdateUserAsync_SameUserCode_DoesNotCheckForDuplicate()
        {
            var (service, userRepo, _) = BuildService();

            var user = MakeUser(userCode: "HE170001");
            userRepo
                .Setup(r => r.GetByIdAsync(user.Id))
                .ReturnsAsync(user);

            userRepo
                .Setup(r => r.UpdateAsync(It.IsAny<User>()))
                .Returns(Task.CompletedTask);

            var dto = new UserEditDto
            {
                Id       = user.Id,
                FullName = user.FullName,
                UserCode = "HE170001", // same code
                Role     = user.Role,
                Status   = user.Status
            };

            var result = await service.UpdateUserAsync(dto);

            Assert.True(result.IsSuccess);
            userRepo.Verify(r => r.UserCodeExistsAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task UpdateUserAsync_EmptyUserCode_DoesNotUpdateCode()
        {
            var (service, userRepo, _) = BuildService();

            var user = MakeUser(userCode: "HE170001");
            userRepo
                .Setup(r => r.GetByIdAsync(user.Id))
                .ReturnsAsync(user);

            userRepo
                .Setup(r => r.UpdateAsync(It.IsAny<User>()))
                .Returns(Task.CompletedTask);

            var dto = new UserEditDto
            {
                Id       = user.Id,
                FullName = "Updated Name",
                UserCode = "",   // empty means no change
                Role     = user.Role,
                Status   = user.Status
            };

            await service.UpdateUserAsync(dto);

            Assert.Equal("HE170001", user.UserCode); // unchanged
        }

        // ── DeleteUserAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteUserAsync_UserNotFound_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();

            userRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((User?)null);

            var result = await service.DeleteUserAsync(Guid.NewGuid());

            Assert.False(result.IsSuccess);
            Assert.Equal("User not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task DeleteUserAsync_ValidUser_CallsDeleteAndReturnsSuccess()
        {
            var (service, userRepo, _) = BuildService();

            var user = MakeUser();
            userRepo
                .Setup(r => r.GetByIdAsync(user.Id))
                .ReturnsAsync(user);

            userRepo
                .Setup(r => r.DeleteAsync(user))
                .Returns(Task.CompletedTask);

            var result = await service.DeleteUserAsync(user.Id);

            Assert.True(result.IsSuccess);
            userRepo.Verify(r => r.DeleteAsync(user), Times.Once);
        }

        [Fact]
        public async Task DeleteUserAsync_ValidUser_DoesNotDeleteOtherUsers()
        {
            var (service, userRepo, _) = BuildService();

            var user1 = MakeUser();
            var user2 = MakeUser();

            userRepo
                .Setup(r => r.GetByIdAsync(user1.Id))
                .ReturnsAsync(user1);

            userRepo
                .Setup(r => r.DeleteAsync(user1))
                .Returns(Task.CompletedTask);

            await service.DeleteUserAsync(user1.Id);

            userRepo.Verify(r => r.DeleteAsync(user2), Times.Never);
        }

        // ── ResetPasswordAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task ResetPasswordAsync_UserNotFound_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();

            userRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((User?)null);

            var result = await service.ResetPasswordAsync(Guid.NewGuid());

            Assert.False(result.IsSuccess);
            Assert.Contains("Không tìm thấy", result.ErrorMessage);
        }

        [Fact]
        public async Task ResetPasswordAsync_ValidUser_SetsStatusToInactive()
        {
            var (service, userRepo, emailService) = BuildService();

            var user = MakeUser(status: UserStatus.Active);
            userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            emailService.Setup(e => e.SendPasswordResetNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            await service.ResetPasswordAsync(user.Id);

            Assert.Equal(UserStatus.Inactive, user.Status);
        }

        [Fact]
        public async Task ResetPasswordAsync_ValidUser_ReturnsNewPassword()
        {
            var (service, userRepo, emailService) = BuildService();

            var user = MakeUser(status: UserStatus.Active);
            userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            emailService.Setup(e => e.SendPasswordResetNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            var result = await service.ResetPasswordAsync(user.Id);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.NotEmpty(result.Data!);
        }

        [Fact]
        public async Task ResetPasswordAsync_ValidUser_UpdatesPasswordHash()
        {
            var (service, userRepo, emailService) = BuildService();

            var user = MakeUser(status: UserStatus.Active);
            var originalHash = user.PasswordHash;

            userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            emailService.Setup(e => e.SendPasswordResetNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            await service.ResetPasswordAsync(user.Id);

            Assert.NotEqual(originalHash, user.PasswordHash);
        }

        [Fact]
        public async Task ResetPasswordAsync_EmailFails_StillReturnsSuccess()
        {
            var (service, userRepo, emailService) = BuildService();

            var user = MakeUser();
            userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            emailService
                .Setup(e => e.SendPasswordResetNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("SMTP error"));

            var result = await service.ResetPasswordAsync(user.Id);

            // Email failure should be swallowed
            Assert.True(result.IsSuccess);
        }

        // ── ChangePasswordAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task ChangePasswordAsync_UserNotFound_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();

            userRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((User?)null);

            var result = await service.ChangePasswordAsync(Guid.NewGuid(), "old", "newPass123");

            Assert.False(result.IsSuccess);
            Assert.Contains("Không tìm thấy", result.ErrorMessage);
        }

        [Fact]
        public async Task ChangePasswordAsync_NewPasswordTooShort_ReturnsFailure()
        {
            var (service, _, _) = BuildService();

            var result = await service.ChangePasswordAsync(Guid.NewGuid(), "old", "12345");

            Assert.False(result.IsSuccess);
            Assert.Contains("6 ký tự", result.ErrorMessage);
        }

        [Fact]
        public async Task ChangePasswordAsync_EmptyNewPassword_ReturnsFailure()
        {
            var (service, _, _) = BuildService();

            var result = await service.ChangePasswordAsync(Guid.NewGuid(), "old", "");

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task ChangePasswordAsync_IncorrectCurrentPassword_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();

            var user = MakeUser(passwordHash: BCrypt.Net.BCrypt.HashPassword("CorrectPassword123"));
            userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

            var result = await service.ChangePasswordAsync(user.Id, "WrongPassword", "NewPass123456");

            Assert.False(result.IsSuccess);
            Assert.Contains("hiện tại", result.ErrorMessage);
        }

        [Fact]
        public async Task ChangePasswordAsync_CorrectCurrentPassword_UpdatesAndReturnsSuccess()
        {
            var (service, userRepo, _) = BuildService();

            const string currentPwd = "CorrectPassword123";
            var user = MakeUser(passwordHash: BCrypt.Net.BCrypt.HashPassword(currentPwd));
            var originalHash = user.PasswordHash;

            userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            var result = await service.ChangePasswordAsync(user.Id, currentPwd, "NewPassword123456");

            Assert.True(result.IsSuccess);
            Assert.NotEqual(originalHash, user.PasswordHash);
        }

        // ── ActivateAndChangePasswordAsync ───────────────────────────────────────

        [Fact]
        public async Task ActivateAndChangePasswordAsync_UserNotFound_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();

            userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

            var result = await service.ActivateAndChangePasswordAsync(Guid.NewGuid(), "temp", "newpass123");

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task ActivateAndChangePasswordAsync_NewPasswordTooShort_ReturnsFailure()
        {
            var (service, _, _) = BuildService();

            var result = await service.ActivateAndChangePasswordAsync(Guid.NewGuid(), "temp", "short");

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task ActivateAndChangePasswordAsync_IncorrectTempPassword_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();

            var user = MakeUser(passwordHash: BCrypt.Net.BCrypt.HashPassword("TempPass123"));
            userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

            var result = await service.ActivateAndChangePasswordAsync(user.Id, "WrongTemp", "NewPassword123456");

            Assert.False(result.IsSuccess);
            Assert.Contains("tạm thời", result.ErrorMessage);
        }

        [Fact]
        public async Task ActivateAndChangePasswordAsync_ValidInput_ActivatesUserAndChangesPassword()
        {
            var (service, userRepo, _) = BuildService();

            const string tempPwd = "TempPass123";
            var user = MakeUser(status: UserStatus.Inactive, passwordHash: BCrypt.Net.BCrypt.HashPassword(tempPwd));

            userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            var result = await service.ActivateAndChangePasswordAsync(user.Id, tempPwd, "NewPassword123456");

            Assert.True(result.IsSuccess);
            Assert.Equal(UserStatus.Active, user.Status);
        }

        [Fact]
        public async Task ActivateAndChangePasswordAsync_NullCurrentPassword_SkipsPasswordCheck()
        {
            var (service, userRepo, _) = BuildService();

            var user = MakeUser(status: UserStatus.Inactive);
            userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            var result = await service.ActivateAndChangePasswordAsync(user.Id, null, "NewPassword123456");

            Assert.True(result.IsSuccess);
        }

        // ── GetUserCodeAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task GetUserCodeAsync_UserNotFound_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();

            userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

            var result = await service.GetUserCodeAsync(Guid.NewGuid());

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task GetUserCodeAsync_ValidUser_ReturnsUserCode()
        {
            var (service, userRepo, _) = BuildService();

            var user = MakeUser(userCode: "HE170999");
            userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

            var result = await service.GetUserCodeAsync(user.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal("HE170999", result.Data);
        }

        // ── GetLecturersAsync ─────────────────────────────────────────────────────

        [Fact]
        public async Task GetLecturersAsync_ReturnsOnlyLecturers()
        {
            var (service, userRepo, _) = BuildService();

            var users = new List<User>
            {
                MakeUser(role: UserRole.Admin,    userCode: "ADM001"),
                MakeUser(role: UserRole.Lecturer, userCode: "LEC001", fullName: "Dr. A"),
                MakeUser(role: UserRole.Student,  userCode: "HE001"),
                MakeUser(role: UserRole.Lecturer, userCode: "LEC002", fullName: "Dr. B")
            };

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(users);

            var result = (await service.GetLecturersAsync()).ToList();

            Assert.Equal(2, result.Count);
            Assert.All(result, u => Assert.Equal(UserRole.Lecturer, u.Role));
        }

        [Fact]
        public async Task GetLecturersAsync_ReturnsLecturersOrderedByFullName()
        {
            var (service, userRepo, _) = BuildService();

            var users = new List<User>
            {
                MakeUser(role: UserRole.Lecturer, userCode: "LEC001", fullName: "Zara Lee"),
                MakeUser(role: UserRole.Lecturer, userCode: "LEC002", fullName: "Alice Chen"),
                MakeUser(role: UserRole.Lecturer, userCode: "LEC003", fullName: "Mike Brown")
            };

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(users);

            var result = (await service.GetLecturersAsync()).ToList();

            Assert.Equal("Alice Chen", result[0].FullName);
            Assert.Equal("Mike Brown", result[1].FullName);
            Assert.Equal("Zara Lee",   result[2].FullName);
        }

        [Fact]
        public async Task GetLecturersAsync_NoLecturers_ReturnsEmpty()
        {
            var (service, userRepo, _) = BuildService();

            var users = new List<User>
            {
                MakeUser(role: UserRole.Admin,   userCode: "ADM001"),
                MakeUser(role: UserRole.Student, userCode: "HE001")
            };

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(users);

            var result = await service.GetLecturersAsync();

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetLecturersAsync_EmptyRepository_ReturnsEmpty()
        {
            var (service, userRepo, _) = BuildService();

            userRepo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(new List<User>());

            var result = await service.GetLecturersAsync();

            Assert.Empty(result);
        }
    }
}
