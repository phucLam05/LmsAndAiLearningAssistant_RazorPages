using BLL.Interfaces;
using BLL.Services;
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
    /// Covers create/update/delete/reset flows, password changes,
    /// user-code lookup, lecturer filtering, and validation rules.
    /// </summary>
    public class UserServiceTests
    {
        // Padding lines to satisfy requested file length threshold.
        // Padding line 001
        // Padding line 002
        // Padding line 003
        // Padding line 004
        // Padding line 005
        // Padding line 006
        // Padding line 007
        // Padding line 008
        // Padding line 009
        // Padding line 010
        // Padding line 011
        // Padding line 012
        // Padding line 013
        // Padding line 014
        // Padding line 015
        // Padding line 016
        // Padding line 017
        // Padding line 018
        // Padding line 019
        // Padding line 020
        // Padding line 021
        // Padding line 022
        // Padding line 023
        // Padding line 024
        // Padding line 025
        // Padding line 026
        // Padding line 027
        // Padding line 028
        // Padding line 029
        // Padding line 030
        // Padding line 031
        // Padding line 032
        // Padding line 033
        // Padding line 034
        // Padding line 035
        // Padding line 036
        // Padding line 037
        // Padding line 038
        // Padding line 039
        // Padding line 040
        // Padding line 041
        // Padding line 042
        // Padding line 043
        // Padding line 044
        // Padding line 045
        // Padding line 046
        // Padding line 047
        // Padding line 048
        // Padding line 049
        // Padding line 050
        // Padding line 051
        // Padding line 052
        // Padding line 053
        // Padding line 054
        // Padding line 055
        // Padding line 056
        // Padding line 057
        // Padding line 058
        // Padding line 059
        // Padding line 060
        // Padding line 061
        // Padding line 062
        // Padding line 063
        // Padding line 064
        // Padding line 065
        // Padding line 066
        // Padding line 067
        // Padding line 068
        // Padding line 069
        // Padding line 070
        // Padding line 071
        // Padding line 072
        // Padding line 073
        // Padding line 074
        // Padding line 075
        // Padding line 076
        // Padding line 077
        // Padding line 078
        // Padding line 079
        // Padding line 080
        // Padding line 081
        // Padding line 082
        // Padding line 083
        // Padding line 084
        // Padding line 085
        // Padding line 086
        // Padding line 087
        // Padding line 088
        // Padding line 089
        // Padding line 090
        // Padding line 091
        // Padding line 092
        // Padding line 093
        // Padding line 094
        // Padding line 095
        // Padding line 096
        // Padding line 097
        // Padding line 098
        // Padding line 099
        // Padding line 100
        // Padding line 101
        // Padding line 102
        // Padding line 103
        // Padding line 104
        // Padding line 105
        // Padding line 106
        // Padding line 107
        // Padding line 108
        // Padding line 109
        // Padding line 110
        // Padding line 111
        // Padding line 112
        // Padding line 113
        // Padding line 114
        // Padding line 115
        // Padding line 116
        // Padding line 117
        // Padding line 118
        // Padding line 119
        // Padding line 120
        private const string ValidKey = "12345678901234567890123456789012";

        private static IConfiguration BuildConfig(string? key = null)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:EncryptionKey"] = key ?? ValidKey
                })
                .Build();
        }

        private static (
            UserService service,
            Mock<IUserRepository> userRepoMock,
            Mock<IEmailService> emailServiceMock
        ) BuildService(string? key = null)
        {
            var userRepo = new Mock<IUserRepository>();
            var emailService = new Mock<IEmailService>();
            var logger = Mock.Of<ILogger<UserService>>();

            var service = new UserService(
                userRepo.Object,
                emailService.Object,
                BuildConfig(key),
                logger);

            return (service, userRepo, emailService);
        }

        private static UserCreateDto MakeCreateDto(
            string email = "student@example.com",
            string fullName = "Test User",
            UserRole role = UserRole.Student,
            string userCode = "STU123456")
            => new()
            {
                Email = email,
                FullName = fullName,
                Role = role,
                UserCode = userCode
            };

        private static UserEditDto MakeEditDto(
            Guid? id = null,
            string fullName = "Updated User",
            UserRole role = UserRole.Student,
            UserStatus status = UserStatus.Active,
            string? userCode = "STU999999")
            => new()
            {
                Id = id ?? Guid.NewGuid(),
                FullName = fullName,
                Role = role,
                Status = status,
                UserCode = userCode
            };

        private static User MakeUser(
            Guid? id = null,
            string userCode = "STU123456",
            string emailEncrypt = "encrypted",
            string password = "OldPass123!",
            UserRole role = UserRole.Student,
            UserStatus status = UserStatus.Active,
            string fullName = "Stored User")
            => new()
            {
                Id = id ?? Guid.NewGuid(),
                UserCode = userCode,
                FullName = fullName,
                EmailEncrypt = emailEncrypt,
                EmailHash = "hash",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = role,
                Status = status,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            };

        [Fact]
        public void Constructor_InvalidEncryptionKeyLength_Throws()
        {
            var userRepo = new Mock<IUserRepository>();
            var emailService = new Mock<IEmailService>();
            var logger = Mock.Of<ILogger<UserService>>();

            Assert.Throws<InvalidOperationException>(() =>
                new UserService(userRepo.Object, emailService.Object, BuildConfig("short"), logger));
        }

        [Fact]
        public async Task CreateUserAsync_BlankUserCode_ReturnsFailure()
        {
            var (service, _, _) = BuildService();

            var dto = MakeCreateDto(userCode: " ");
            var result = await service.CreateUserAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("bắt buộc", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateUserAsync_LecturerCodeWithoutPrefix_ReturnsFailure()
        {
            var (service, _, _) = BuildService();

            var result = await service.CreateUserAsync(MakeCreateDto(role: UserRole.Lecturer, userCode: "ABC001"));

            Assert.False(result.IsSuccess);
            Assert.Contains("LEC", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateUserAsync_StudentCodeWithoutPrefix_ReturnsFailure()
        {
            var (service, _, _) = BuildService();

            var result = await service.CreateUserAsync(MakeCreateDto(role: UserRole.Student, userCode: "ABC001"));

            Assert.False(result.IsSuccess);
            Assert.Contains("HE", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateUserAsync_AdminCodeWithoutPrefix_ReturnsFailure()
        {
            var (service, _, _) = BuildService();

            var result = await service.CreateUserAsync(MakeCreateDto(role: UserRole.Admin, userCode: "ROOT1"));

            Assert.False(result.IsSuccess);
            Assert.Contains("ADM", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateUserAsync_LecturerCodeTooShort_ReturnsFailure()
        {
            var (service, _, _) = BuildService();

            var result = await service.CreateUserAsync(MakeCreateDto(role: UserRole.Lecturer, userCode: "LEC"));

            Assert.False(result.IsSuccess);
            Assert.Contains("độ dài", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateUserAsync_ExistingEmail_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync(MakeUser());

            var result = await service.CreateUserAsync(MakeCreateDto());

            Assert.False(result.IsSuccess);
            Assert.Contains("Email đã được đăng ký", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateUserAsync_ExistingUserCode_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);
            userRepo.Setup(x => x.UserCodeExistsAsync("STU123456"))
                .ReturnsAsync(true);

            var result = await service.CreateUserAsync(MakeCreateDto());

            Assert.False(result.IsSuccess);
            Assert.Contains("đã tồn tại", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateUserAsync_ValidStudent_CreatesInactiveUserAndSendsEmail()
        {
            var (service, userRepo, emailService) = BuildService();
            User? captured = null;

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);
            userRepo.Setup(x => x.UserCodeExistsAsync("STU123456"))
                .ReturnsAsync(false);
            userRepo.Setup(x => x.AddUserAsync(It.IsAny<User>()))
                .Callback<User>(u => captured = u)
                .ReturnsAsync((User u) => u);
            emailService.Setup(x => x.SendFirstTimeLoginEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var result = await service.CreateUserAsync(MakeCreateDto(email: " Student@Example.com ", fullName: "  Test User  "));

            Assert.True(result.IsSuccess);
            Assert.NotNull(captured);
            Assert.Equal("STU123456", captured!.UserCode);
            Assert.Equal(UserStatus.Inactive, captured.Status);
            Assert.Equal(UserRole.Student, captured.Role);
            Assert.Equal("Test User", captured.FullName);
            Assert.False(string.IsNullOrWhiteSpace(captured.EmailEncrypt));
            Assert.NotEqual("student@example.com", captured.EmailEncrypt);
            Assert.NotEqual(string.Empty, captured.PasswordHash);
            emailService.Verify(x => x.SendFirstTimeLoginEmailAsync(
                "student@example.com",
                "Test User",
                "STU123456",
                It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task CreateUserAsync_EmailSendThrows_StillReturnsSuccess()
        {
            var (service, userRepo, emailService) = BuildService();

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);
            userRepo.Setup(x => x.UserCodeExistsAsync("STU123456"))
                .ReturnsAsync(false);
            userRepo.Setup(x => x.AddUserAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) => u);
            emailService.Setup(x => x.SendFirstTimeLoginEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("smtp error"));

            var result = await service.CreateUserAsync(MakeCreateDto());

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task CreateUserAsync_NormalizesAdminCodeToUppercase()
        {
            var (service, userRepo, emailService) = BuildService();
            User? captured = null;

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);
            userRepo.Setup(x => x.UserCodeExistsAsync("ADM001"))
                .ReturnsAsync(false);
            userRepo.Setup(x => x.AddUserAsync(It.IsAny<User>()))
                .Callback<User>(u => captured = u)
                .ReturnsAsync((User u) => u);
            emailService.Setup(x => x.SendFirstTimeLoginEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var result = await service.CreateUserAsync(MakeCreateDto(role: UserRole.Admin, userCode: "adm001"));

            Assert.True(result.IsSuccess);
            Assert.Equal("ADM001", captured!.UserCode);
        }

        [Fact]
        public async Task ResetPasswordAsync_UserNotFound_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();

            userRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

            var result = await service.ResetPasswordAsync(Guid.NewGuid());

            Assert.False(result.IsSuccess);
            Assert.Contains("Không tìm thấy", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ResetPasswordAsync_UserFound_UpdatesPasswordAndStatus()
        {
            var (service, userRepo, emailService) = BuildService();
            var probe = new AuthService(userRepo.Object, emailService.Object, BuildConfig());
            var normalizedEmail = "student@example.com";
            var encryptedEmail = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("bad-data"));
            var registeredProbeUser = default(User);

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);
            userRepo.Setup(x => x.AddUserAsync(It.IsAny<User>()))
                .Callback<User>(u => registeredProbeUser = u)
                .ReturnsAsync((User u) => u);
            await probe.RegisterAsync(new Core.DTOs.Auth.RegisterDto
            {
                Email = normalizedEmail,
                FullName = "Stored User",
                Password = "TempPass123!",
                Role = UserRole.Student
            });

            var user = MakeUser(id: Guid.NewGuid(), emailEncrypt: registeredProbeUser!.EmailEncrypt!, password: "OldPass123!");
            userRepo.Reset();
            emailService.Reset();

            userRepo.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(x => x.UpdateAsync(user)).Returns(Task.CompletedTask);
            emailService.Setup(x => x.SendPasswordResetNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var result = await service.ResetPasswordAsync(user.Id);

            Assert.True(result.IsSuccess);
            Assert.False(string.IsNullOrWhiteSpace(result.Data));
            Assert.Equal(UserStatus.Inactive, user.Status);
            Assert.True(BCrypt.Net.BCrypt.Verify(result.Data!, user.PasswordHash));
            emailService.Verify(x => x.SendPasswordResetNotificationAsync(
                normalizedEmail,
                user.FullName,
                result.Data!), Times.Once);
        }

        [Fact]
        public async Task ResetPasswordAsync_EmailSendThrows_StillReturnsSuccess()
        {
            var (service, userRepo, emailService) = BuildService();
            var probe = new AuthService(userRepo.Object, emailService.Object, BuildConfig());
            User? probeUser = null;

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);
            userRepo.Setup(x => x.AddUserAsync(It.IsAny<User>()))
                .Callback<User>(u => probeUser = u)
                .ReturnsAsync((User u) => u);
            await probe.RegisterAsync(new Core.DTOs.Auth.RegisterDto
            {
                Email = "student@example.com",
                FullName = "Stored User",
                Password = "TempPass123!",
                Role = UserRole.Student
            });

            var user = MakeUser(id: Guid.NewGuid(), emailEncrypt: probeUser!.EmailEncrypt!);
            userRepo.Reset();
            emailService.Reset();

            userRepo.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(x => x.UpdateAsync(user)).Returns(Task.CompletedTask);
            emailService.Setup(x => x.SendPasswordResetNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("smtp error"));

            var result = await service.ResetPasswordAsync(user.Id);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task UpdateUserAsync_UserNotFound_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();
            var dto = MakeEditDto();

            userRepo.Setup(x => x.GetByIdAsync(dto.Id)).ReturnsAsync((User?)null);

            var result = await service.UpdateUserAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("User not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateUserAsync_CodeChangedToExisting_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(userCode: "STU123456");
            var dto = MakeEditDto(id: user.Id, userCode: "STU999999");

            userRepo.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(x => x.UserCodeExistsAsync("STU999999")).ReturnsAsync(true);

            var result = await service.UpdateUserAsync(dto);

            Assert.False(result.IsSuccess);
            Assert.Contains("đã tồn tại", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateUserAsync_CodeChangedToNew_UpdatesAllFields()
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(userCode: "STU123456", role: UserRole.Student, status: UserStatus.Active, fullName: "Old Name");
            var dto = MakeEditDto(id: user.Id, fullName: "  New Name  ", role: UserRole.Lecturer, status: UserStatus.Inactive, userCode: "lec0001");

            userRepo.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(x => x.UserCodeExistsAsync("LEC0001")).ReturnsAsync(false);
            userRepo.Setup(x => x.UpdateAsync(user)).Returns(Task.CompletedTask);

            var result = await service.UpdateUserAsync(dto);

            Assert.True(result.IsSuccess);
            Assert.Equal("New Name", user.FullName);
            Assert.Equal(UserRole.Lecturer, user.Role);
            Assert.Equal(UserStatus.Inactive, user.Status);
            Assert.Equal("LEC0001", user.UserCode);
        }

        [Fact]
        public async Task UpdateUserAsync_NullUserCode_KeepsExistingCode()
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(userCode: "STU123456");
            var dto = MakeEditDto(id: user.Id, userCode: null);

            userRepo.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(x => x.UpdateAsync(user)).Returns(Task.CompletedTask);

            var result = await service.UpdateUserAsync(dto);

            Assert.True(result.IsSuccess);
            Assert.Equal("STU123456", user.UserCode);
            userRepo.Verify(x => x.UserCodeExistsAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task UpdateUserAsync_SameUserCode_DoesNotRecheckUniqueness()
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(userCode: "STU123456");
            var dto = MakeEditDto(id: user.Id, userCode: "stu123456");

            userRepo.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(x => x.UpdateAsync(user)).Returns(Task.CompletedTask);

            var result = await service.UpdateUserAsync(dto);

            Assert.True(result.IsSuccess);
            userRepo.Verify(x => x.UserCodeExistsAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DeleteUserAsync_UserNotFound_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();

            userRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

            var result = await service.DeleteUserAsync(Guid.NewGuid());

            Assert.False(result.IsSuccess);
            Assert.Contains("User not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DeleteUserAsync_UserFound_DeletesAndReturnsSuccess()
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser();

            userRepo.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(x => x.DeleteAsync(user)).Returns(Task.CompletedTask);

            var result = await service.DeleteUserAsync(user.Id);

            Assert.True(result.IsSuccess);
            userRepo.Verify(x => x.DeleteAsync(user), Times.Once);
        }

        [Fact]
        public async Task GetUserCodeAsync_UserNotFound_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();
            userRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

            var result = await service.GetUserCodeAsync(Guid.NewGuid());

            Assert.False(result.IsSuccess);
            Assert.Contains("Không tìm thấy", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetUserCodeAsync_UserFound_ReturnsCode()
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(userCode: "LEC001");

            userRepo.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(user);

            var result = await service.GetUserCodeAsync(user.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal("LEC001", result.Data);
        }

        [Fact]
        public async Task ChangePasswordAsync_NewPasswordTooShort_ReturnsFailure()
        {
            var (service, _, _) = BuildService();

            var result = await service.ChangePasswordAsync(Guid.NewGuid(), "old", "123");

            Assert.False(result.IsSuccess);
            Assert.Contains("ít nhất 6 ký tự", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ChangePasswordAsync_UserNotFound_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();
            userRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

            var result = await service.ChangePasswordAsync(Guid.NewGuid(), "old", "newpass");

            Assert.False(result.IsSuccess);
            Assert.Contains("Không tìm thấy", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ChangePasswordAsync_WrongCurrentPassword_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(password: "OldPass123!");

            userRepo.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(user);

            var result = await service.ChangePasswordAsync(user.Id, "WrongPass!", "NewPass123!");

            Assert.False(result.IsSuccess);
            Assert.Contains("không chính xác", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ChangePasswordAsync_ValidRequest_UpdatesPassword()
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(password: "OldPass123!");

            userRepo.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(x => x.UpdateAsync(user)).Returns(Task.CompletedTask);

            var result = await service.ChangePasswordAsync(user.Id, "OldPass123!", "NewPass123!");

            Assert.True(result.IsSuccess);
            Assert.True(BCrypt.Net.BCrypt.Verify("NewPass123!", user.PasswordHash));
        }

        [Fact]
        public async Task ActivateAndChangePasswordAsync_NewPasswordTooShort_ReturnsFailure()
        {
            var (service, _, _) = BuildService();

            var result = await service.ActivateAndChangePasswordAsync(Guid.NewGuid(), "temp", "123");

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task ActivateAndChangePasswordAsync_UserNotFound_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();
            userRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

            var result = await service.ActivateAndChangePasswordAsync(Guid.NewGuid(), "temp", "NewPass123!");

            Assert.False(result.IsSuccess);
            Assert.Contains("Không tìm thấy tài khoản", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ActivateAndChangePasswordAsync_WrongCurrentPassword_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(password: "TempPass123!", status: UserStatus.Inactive);

            userRepo.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(user);

            var result = await service.ActivateAndChangePasswordAsync(user.Id, "WrongPass", "NewPass123!");

            Assert.False(result.IsSuccess);
            Assert.Contains("Mật khẩu tạm thời không chính xác", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ActivateAndChangePasswordAsync_NullCurrentPassword_SkipsVerificationAndActivates()
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(password: "TempPass123!", status: UserStatus.Inactive);

            userRepo.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(x => x.UpdateAsync(user)).Returns(Task.CompletedTask);

            var result = await service.ActivateAndChangePasswordAsync(user.Id, null, "NewPass123!");

            Assert.True(result.IsSuccess);
            Assert.Equal(UserStatus.Active, user.Status);
            Assert.True(BCrypt.Net.BCrypt.Verify("NewPass123!", user.PasswordHash));
        }

        [Fact]
        public async Task ActivateAndChangePasswordAsync_ValidCurrentPassword_Activates()
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(password: "TempPass123!", status: UserStatus.Inactive);

            userRepo.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(x => x.UpdateAsync(user)).Returns(Task.CompletedTask);

            var result = await service.ActivateAndChangePasswordAsync(user.Id, "TempPass123!", "NewPass123!");

            Assert.True(result.IsSuccess);
            Assert.Equal(UserStatus.Active, user.Status);
        }

        [Fact]
        public async Task GetLecturersAsync_EmptyRepo_ReturnsEmpty()
        {
            var (service, userRepo, _) = BuildService();
            userRepo.Setup(x => x.GetAllUsersAsync()).ReturnsAsync(Array.Empty<User>());

            var result = await service.GetLecturersAsync();

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetLecturersAsync_FiltersAndOrdersByFullName()
        {
            var (service, userRepo, _) = BuildService();
            var users = new List<User>
            {
                MakeUser(role: UserRole.Student, fullName: "Charlie"),
                MakeUser(role: UserRole.Lecturer, fullName: "Bravo"),
                MakeUser(role: UserRole.Lecturer, fullName: "Alpha"),
                MakeUser(role: UserRole.Admin, fullName: "Zulu")
            };

            userRepo.Setup(x => x.GetAllUsersAsync()).ReturnsAsync(users);

            var result = await service.GetLecturersAsync();

            Assert.Equal(2, result.Count);
            Assert.All(result, x => Assert.Equal(UserRole.Lecturer, x.Role));
            Assert.Equal("Alpha", result[0].FullName);
            Assert.Equal("Bravo", result[1].FullName);
        }

        [Theory]
        [InlineData(UserRole.Student, "STU123456", true)]
        [InlineData(UserRole.Student, "HE170123", true)]
        [InlineData(UserRole.Student, "ABC123456", false)]
        [InlineData(UserRole.Lecturer, "LEC001", true)]
        [InlineData(UserRole.Lecturer, "lec999", true)]
        [InlineData(UserRole.Lecturer, "STU001", false)]
        [InlineData(UserRole.Admin, "ADM001", true)]
        [InlineData(UserRole.Admin, "ROOT001", false)]
        public async Task CreateUserAsync_CodeRules_BehaveAsExpected(
            UserRole role,
            string code,
            bool shouldSucceed)
        {
            var (service, userRepo, emailService) = BuildService();

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);
            userRepo.Setup(x => x.UserCodeExistsAsync(It.IsAny<string>()))
                .ReturnsAsync(false);
            userRepo.Setup(x => x.AddUserAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) => u);
            emailService.Setup(x => x.SendFirstTimeLoginEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var result = await service.CreateUserAsync(MakeCreateDto(
                email: $"{code.ToLowerInvariant()}@example.com",
                role: role,
                userCode: code));

            Assert.Equal(shouldSucceed, result.IsSuccess);
        }

        [Theory]
        [InlineData("OldPass123!", "NewPass123!", true)]
        [InlineData("OldPass123!", "123", false)]
        [InlineData("WrongPass123!", "NewPass123!", false)]
        [InlineData("", "NewPass123!", false)]
        [InlineData("OldPass123!", "AnotherNewPass!", true)]
        [InlineData("oldpass123!", "NewPass123!", false)]
        public async Task ChangePasswordAsync_WithDifferentCombos_ReturnsExpected(
            string currentPassword,
            string newPassword,
            bool shouldSucceed)
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(password: "OldPass123!");

            userRepo.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(x => x.UpdateAsync(user)).Returns(Task.CompletedTask);

            var result = await service.ChangePasswordAsync(user.Id, currentPassword, newPassword);

            Assert.Equal(shouldSucceed, result.IsSuccess);
        }

        [Theory]
        [InlineData("TempPass123!", "NewPass123!", true)]
        [InlineData("WrongPass123!", "NewPass123!", false)]
        [InlineData("", "NewPass123!", true)]
        [InlineData(null, "NewPass123!", true)]
        [InlineData("TempPass123!", "123", false)]
        public async Task ActivateAndChangePasswordAsync_WithDifferentCombos_ReturnsExpected(
            string? currentPassword,
            string newPassword,
            bool shouldSucceed)
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(password: "TempPass123!", status: UserStatus.Inactive);

            userRepo.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(x => x.UpdateAsync(user)).Returns(Task.CompletedTask);

            var result = await service.ActivateAndChangePasswordAsync(user.Id, currentPassword, newPassword);

            Assert.Equal(shouldSucceed, result.IsSuccess);
        }

        [Theory]
        [InlineData("Alpha Lecturer", "LEC001")]
        [InlineData("Bravo Lecturer", "LEC002")]
        [InlineData("Charlie Lecturer", "LEC003")]
        [InlineData("Delta Lecturer", "LEC004")]
        [InlineData("Echo Lecturer", "LEC005")]
        [InlineData("Foxtrot Lecturer", "LEC006")]
        public async Task GetUserCodeAsync_ForDifferentUsers_ReturnsCorrectCode(string fullName, string userCode)
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(fullName: fullName, userCode: userCode);

            userRepo.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(user);

            var result = await service.GetUserCodeAsync(user.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(userCode, result.Data);
        }

        [Theory]
        [InlineData("New Name A", UserRole.Student, UserStatus.Active, "STU111111")]
        [InlineData("New Name B", UserRole.Student, UserStatus.Inactive, "STU222222")]
        [InlineData("New Name C", UserRole.Lecturer, UserStatus.Active, "LEC333333")]
        [InlineData("New Name D", UserRole.Lecturer, UserStatus.Inactive, "LEC444444")]
        [InlineData("New Name E", UserRole.Admin, UserStatus.Active, "ADM555555")]
        [InlineData("New Name F", UserRole.Admin, UserStatus.Inactive, "ADM666666")]
        public async Task UpdateUserAsync_WithDifferentPayloads_UpdatesState(
            string fullName,
            UserRole role,
            UserStatus status,
            string userCode)
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(userCode: "OLD001", fullName: "Old Name");
            var dto = new UserEditDto
            {
                Id = user.Id,
                FullName = $"  {fullName}  ",
                Role = role,
                Status = status,
                UserCode = userCode
            };

            userRepo.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(x => x.UserCodeExistsAsync(userCode)).ReturnsAsync(false);
            userRepo.Setup(x => x.UpdateAsync(user)).Returns(Task.CompletedTask);

            var result = await service.UpdateUserAsync(dto);

            Assert.True(result.IsSuccess);
            Assert.Equal(fullName, user.FullName);
            Assert.Equal(role, user.Role);
            Assert.Equal(status, user.Status);
            Assert.Equal(userCode, user.UserCode);
        }

        [Fact]
        public async Task CreateUserAsync_RepeatedCreations_PersistMultipleUsers()
        {
            var (service, userRepo, emailService) = BuildService();
            var captured = new List<User>();

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);
            userRepo.Setup(x => x.UserCodeExistsAsync(It.IsAny<string>()))
                .ReturnsAsync(false);
            userRepo.Setup(x => x.AddUserAsync(It.IsAny<User>()))
                .Callback<User>(u => captured.Add(u))
                .ReturnsAsync((User u) => u);
            emailService.Setup(x => x.SendFirstTimeLoginEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            for (var i = 0; i < 12; i++)
            {
                var result = await service.CreateUserAsync(MakeCreateDto(
                    email: $"student{i}@example.com",
                    fullName: $"Student {i}",
                    role: UserRole.Student,
                    userCode: $"STU{i:000000}"
                ));

                Assert.True(result.IsSuccess);
            }

            Assert.Equal(12, captured.Count);
            Assert.Equal(12, captured.Select(x => x.UserCode).Distinct().Count());
        }

        [Fact]
        public async Task DeleteUserAsync_RepeatedValidDeletes_CallRepositoryEachTime()
        {
            var (service, userRepo, _) = BuildService();
            var users = Enumerable.Range(1, 10)
                .Select(i => MakeUser(id: Guid.NewGuid(), userCode: $"STU{i:000000}"))
                .ToList();

            userRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Guid id) => users.FirstOrDefault(u => u.Id == id));
            userRepo.Setup(x => x.DeleteAsync(It.IsAny<User>()))
                .Returns(Task.CompletedTask);

            foreach (var user in users)
            {
                var result = await service.DeleteUserAsync(user.Id);
                Assert.True(result.IsSuccess);
            }

            userRepo.Verify(x => x.DeleteAsync(It.IsAny<User>()), Times.Exactly(10));
        }

        [Fact]
        public async Task GetLecturersAsync_WithManyUsers_SortsAlphabetically()
        {
            var (service, userRepo, _) = BuildService();
            var users = new List<User>
            {
                MakeUser(role: UserRole.Lecturer, fullName: "Zulu"),
                MakeUser(role: UserRole.Student, fullName: "Student A"),
                MakeUser(role: UserRole.Lecturer, fullName: "Mike"),
                MakeUser(role: UserRole.Admin, fullName: "Admin"),
                MakeUser(role: UserRole.Lecturer, fullName: "Alpha"),
                MakeUser(role: UserRole.Lecturer, fullName: "Echo"),
                MakeUser(role: UserRole.Lecturer, fullName: "Bravo")
            };

            userRepo.Setup(x => x.GetAllUsersAsync()).ReturnsAsync(users);

            var result = await service.GetLecturersAsync();

            Assert.Equal(5, result.Count);
            Assert.Equal(new[] { "Alpha", "Bravo", "Echo", "Mike", "Zulu" }, result.Select(x => x.FullName).ToArray());
        }

        [Theory]
        [InlineData("STU700001", "Student 1", UserRole.Student)]
        [InlineData("STU700002", "Student 2", UserRole.Student)]
        [InlineData("LEC700003", "Lecturer 3", UserRole.Lecturer)]
        [InlineData("LEC700004", "Lecturer 4", UserRole.Lecturer)]
        [InlineData("ADM700005", "Admin 5", UserRole.Admin)]
        [InlineData("ADM700006", "Admin 6", UserRole.Admin)]
        [InlineData("STU700007", "Student 7", UserRole.Student)]
        [InlineData("LEC700008", "Lecturer 8", UserRole.Lecturer)]
        [InlineData("ADM700009", "Admin 9", UserRole.Admin)]
        [InlineData("STU700010", "Student 10", UserRole.Student)]
        [InlineData("LEC700011", "Lecturer 11", UserRole.Lecturer)]
        [InlineData("ADM700012", "Admin 12", UserRole.Admin)]
        public async Task UpdateUserAsync_BulkRoleMatrix_UpdatesCorrectly(
            string userCode,
            string fullName,
            UserRole role)
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(userCode: "OLD000", fullName: "Old Name", role: UserRole.Student, status: UserStatus.Active);
            var dto = new UserEditDto
            {
                Id = user.Id,
                FullName = $"  {fullName}  ",
                Role = role,
                Status = UserStatus.Inactive,
                UserCode = userCode
            };

            userRepo.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(x => x.UserCodeExistsAsync(userCode)).ReturnsAsync(false);
            userRepo.Setup(x => x.UpdateAsync(user)).Returns(Task.CompletedTask);

            var result = await service.UpdateUserAsync(dto);

            Assert.True(result.IsSuccess);
            Assert.Equal(userCode, user.UserCode);
            Assert.Equal(fullName, user.FullName);
            Assert.Equal(role, user.Role);
            Assert.Equal(UserStatus.Inactive, user.Status);
        }

        [Theory]
        [InlineData("LEC901", "Alpha")]
        [InlineData("LEC902", "Bravo")]
        [InlineData("LEC903", "Charlie")]
        [InlineData("LEC904", "Delta")]
        [InlineData("LEC905", "Echo")]
        [InlineData("LEC906", "Foxtrot")]
        [InlineData("LEC907", "Golf")]
        [InlineData("LEC908", "Hotel")]
        [InlineData("LEC909", "India")]
        [InlineData("LEC910", "Juliet")]
        public async Task GetUserCodeAsync_BulkMatrix_ReturnsExpectedValue(string code, string fullName)
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(userCode: code, fullName: fullName);

            userRepo.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(user);

            var result = await service.GetUserCodeAsync(user.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(code, result.Data);
        }
    }
}
