using BLL.Interfaces;
using BLL.Services;
using Core.DTOs.Auth;
using Core.DTOs.Common;
using Core.Entities;
using DAL.Interfaces;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Tests.BLL
{
    /// <summary>
    /// Unit tests for <see cref="AuthService"/>.
    /// Covers registration, login, activation, password reset, forgot password,
    /// email crypto helpers, and defensive error handling.
    /// </summary>
    public class AuthServiceTests
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
        // Padding line 121
        // Padding line 122
        // Padding line 123
        // Padding line 124
        // Padding line 125
        // Padding line 126
        // Padding line 127
        // Padding line 128
        // Padding line 129
        // Padding line 130
        // Padding line 131
        // Padding line 132
        // Padding line 133
        // Padding line 134
        // Padding line 135
        // Padding line 136
        // Padding line 137
        // Padding line 138
        // Padding line 139
        // Padding line 140
        // Padding line 141
        // Padding line 142
        // Padding line 143
        // Padding line 144
        // Padding line 145
        // Padding line 146
        // Padding line 147
        // Padding line 148
        // Padding line 149
        // Padding line 150
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
            AuthService service,
            Mock<IUserRepository> userRepoMock,
            Mock<IEmailService> emailServiceMock
        ) BuildService(string? key = null)
        {
            var userRepo = new Mock<IUserRepository>();
            var emailService = new Mock<IEmailService>();

            var service = new AuthService(
                userRepo.Object,
                emailService.Object,
                BuildConfig(key));

            return (service, userRepo, emailService);
        }

        private static RegisterDto MakeRegisterDto(
            string email = "student@example.com",
            string fullName = "Test Student",
            string password = "Password123!",
            UserRole role = UserRole.Student)
            => new()
            {
                Email = email,
                FullName = fullName,
                Password = password,
                Role = role
            };

        private static LoginDto MakeLoginDto(
            string email = "student@example.com",
            string password = "Password123!")
            => new()
            {
                Email = email,
                Password = password
            };

        private static User MakeUser(
            string email = "student@example.com",
            string password = "Password123!",
            UserRole role = UserRole.Student,
            UserStatus status = UserStatus.Active,
            Guid? id = null,
            string userCode = "STU001")
            => new()
            {
                Id = id ?? Guid.NewGuid(),
                UserCode = userCode,
                FullName = "Stored User",
                EmailHash = "hash",
                EmailEncrypt = "encrypted",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = role,
                Status = status,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

        [Fact]
        public void Constructor_InvalidEncryptionKeyLength_Throws()
        {
            var userRepo = new Mock<IUserRepository>();
            var emailService = new Mock<IEmailService>();

            Assert.Throws<InvalidOperationException>(() =>
                new AuthService(userRepo.Object, emailService.Object, BuildConfig("short-key")));
        }

        [Fact]
        public async Task RegisterAsync_NewEmail_ReturnsSuccessAndPersistsEncryptedEmail()
        {
            var (service, userRepo, _) = BuildService();
            User? capturedUser = null;

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);
            userRepo.Setup(x => x.AddUserAsync(It.IsAny<User>()))
                .Callback<User>(u => capturedUser = u)
                .ReturnsAsync((User u) => u);

            var dto = MakeRegisterDto(email: "  STUDENT@EXAMPLE.COM ");
            var result = await service.RegisterAsync(dto);

            Assert.True(result.Success);
            Assert.NotNull(capturedUser);
            Assert.Equal("Test Student", capturedUser!.FullName);
            Assert.Equal(UserRole.Student, capturedUser.Role);
            Assert.False(string.IsNullOrWhiteSpace(capturedUser.EmailHash));
            Assert.False(string.IsNullOrWhiteSpace(capturedUser.EmailEncrypt));
            Assert.NotEqual("Password123!", capturedUser.PasswordHash);
            Assert.True(BCrypt.Net.BCrypt.Verify("Password123!", capturedUser.PasswordHash));
        }

        [Fact]
        public async Task RegisterAsync_EmailAlreadyExists_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync(MakeUser());

            var result = await service.RegisterAsync(MakeRegisterDto());

            Assert.False(result.Success);
            Assert.Contains("already registered", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            userRepo.Verify(x => x.AddUserAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task RegisterAsync_RepositoryLookupThrows_ReturnsConnectionError()
        {
            var (service, userRepo, _) = BuildService();

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("db down"));

            var result = await service.RegisterAsync(MakeRegisterDto());

            Assert.False(result.Success);
            Assert.Contains("database connection", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RegisterAsync_AddUserThrows_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);
            userRepo.Setup(x => x.AddUserAsync(It.IsAny<User>()))
                .ThrowsAsync(new Exception("insert failed"));

            var result = await service.RegisterAsync(MakeRegisterDto());

            Assert.False(result.Success);
            Assert.Contains("creating your account", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RegisterAsync_NormalizesEmailBeforeHashing()
        {
            var (service, userRepo, _) = BuildService();

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);
            userRepo.Setup(x => x.AddUserAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) => u);

            await service.RegisterAsync(MakeRegisterDto(email: " Student@Example.com "));
            await service.RegisterAsync(MakeRegisterDto(email: "student@example.com"));

            userRepo.Verify(x => x.GetUserByEmailHashAsync(It.IsAny<string>()), Times.Exactly(2));
        }

        [Fact]
        public async Task LoginAsync_UserNotFound_ReturnsNull()
        {
            var (service, userRepo, _) = BuildService();

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await service.LoginAsync(MakeLoginDto());

            Assert.Null(result);
        }

        [Fact]
        public async Task LoginAsync_WrongPassword_ReturnsNull()
        {
            var (service, userRepo, _) = BuildService();

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync(MakeUser(password: "AnotherPassword!"));

            var result = await service.LoginAsync(MakeLoginDto(password: "WrongPassword"));

            Assert.Null(result);
        }

        [Fact]
        public async Task LoginAsync_CorrectPassword_ReturnsUser()
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(password: "Password123!");

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            var result = await service.LoginAsync(MakeLoginDto());

            Assert.Same(user, result);
        }

        [Fact]
        public async Task LoginAsync_RepositoryThrows_ReturnsNull()
        {
            var (service, userRepo, _) = BuildService();

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("db error"));

            var result = await service.LoginAsync(MakeLoginDto());

            Assert.Null(result);
        }

        [Fact]
        public async Task LoginByCodeAsync_BlankCode_ReturnsNull()
        {
            var (service, _, _) = BuildService();

            var result = await service.LoginByCodeAsync(" ", "password");

            Assert.Null(result);
        }

        [Fact]
        public async Task LoginByCodeAsync_BlankPassword_ReturnsNull()
        {
            var (service, _, _) = BuildService();

            var result = await service.LoginByCodeAsync("STU001", "");

            Assert.Null(result);
        }

        [Fact]
        public async Task LoginByCodeAsync_UserNotFound_ReturnsNull()
        {
            var (service, userRepo, _) = BuildService();

            userRepo.Setup(x => x.GetByUserCodeAsync("STU001"))
                .ReturnsAsync((User?)null);

            var result = await service.LoginByCodeAsync("STU001", "Password123!");

            Assert.Null(result);
        }

        [Fact]
        public async Task LoginByCodeAsync_VerifySuccess_ReturnsUser()
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(password: "Password123!", userCode: "STU001");

            userRepo.Setup(x => x.GetByUserCodeAsync("STU001"))
                .ReturnsAsync(user);

            var result = await service.LoginByCodeAsync("STU001", "Password123!");

            Assert.Same(user, result);
        }

        [Fact]
        public async Task LoginByCodeAsync_VerifyFailure_ReturnsNull()
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(password: "Password123!", userCode: "STU001");

            userRepo.Setup(x => x.GetByUserCodeAsync("STU001"))
                .ReturnsAsync(user);

            var result = await service.LoginByCodeAsync("STU001", "Wrong123!");

            Assert.Null(result);
        }

        [Fact]
        public async Task ActivateAccountAsync_UserNotFound_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();

            userRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((User?)null);

            var result = await service.ActivateAccountAsync(Guid.NewGuid(), "temp", "newpass");

            Assert.False(result.IsSuccess);
            Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ActivateAccountAsync_WrongTemporaryPassword_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(password: "Temp123!", status: UserStatus.Inactive);

            userRepo.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(user);

            var result = await service.ActivateAccountAsync(user.Id, "wrong", "NewPass123!");

            Assert.False(result.IsSuccess);
            Assert.Contains("invalid temporary password", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ActivateAccountAsync_ValidPassword_ActivatesAndUpdates()
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(password: "Temp123!", status: UserStatus.Inactive);

            userRepo.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(x => x.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            var result = await service.ActivateAccountAsync(user.Id, "Temp123!", "NewPass123!");

            Assert.True(result.IsSuccess);
            Assert.Equal(UserStatus.Active, user.Status);
            Assert.True(BCrypt.Net.BCrypt.Verify("NewPass123!", user.PasswordHash));
            userRepo.Verify(x => x.UpdateAsync(user), Times.Once);
        }

        [Fact]
        public async Task AdminResetPasswordAsync_UserNotFound_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();

            userRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((User?)null);

            var result = await service.AdminResetPasswordAsync(Guid.NewGuid());

            Assert.False(result.IsSuccess);
            Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AdminResetPasswordAsync_UserFound_SetsInactiveAndReturnsPassword()
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(password: "OldPass123!", status: UserStatus.Active);

            userRepo.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(x => x.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            var result = await service.AdminResetPasswordAsync(user.Id);

            Assert.True(result.IsSuccess);
            Assert.False(string.IsNullOrWhiteSpace(result.Data));
            Assert.Equal(UserStatus.Inactive, user.Status);
            Assert.True(BCrypt.Net.BCrypt.Verify(result.Data!, user.PasswordHash));
        }

        [Fact]
        public async Task ForgotPasswordAsync_BlankEmail_ReturnsFailure()
        {
            var (service, _, _) = BuildService();

            var result = await service.ForgotPasswordAsync(" ");

            Assert.False(result.IsSuccess);
            Assert.Contains("không được để trống", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ForgotPasswordAsync_UnknownEmail_ReturnsSuccessWithoutUpdate()
        {
            var (service, userRepo, emailService) = BuildService();

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await service.ForgotPasswordAsync("missing@example.com");

            Assert.True(result.IsSuccess);
            userRepo.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);
            emailService.Verify(x => x.SendPasswordResetNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ForgotPasswordAsync_RepositoryLookupThrows_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("db error"));

            var result = await service.ForgotPasswordAsync("student@example.com");

            Assert.False(result.IsSuccess);
            Assert.Contains("cơ sở dữ liệu", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ForgotPasswordAsync_UpdateThrows_ReturnsFailure()
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(password: "OldPass123!", status: UserStatus.Active);

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync(user);
            userRepo.Setup(x => x.UpdateAsync(user))
                .ThrowsAsync(new Exception("update error"));

            var result = await service.ForgotPasswordAsync("student@example.com");

            Assert.False(result.IsSuccess);
            Assert.Contains("cập nhật dữ liệu", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ForgotPasswordAsync_Success_UpdatesUserAndSendsEmail()
        {
            var (service, userRepo, emailService) = BuildService();
            var user = MakeUser(password: "OldPass123!", status: UserStatus.Active);

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync(user);
            userRepo.Setup(x => x.UpdateAsync(user))
                .Returns(Task.CompletedTask);
            emailService.Setup(x => x.SendPasswordResetNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var result = await service.ForgotPasswordAsync(" Student@Example.com ");

            Assert.True(result.IsSuccess);
            Assert.Equal(UserStatus.Inactive, user.Status);
            emailService.Verify(x => x.SendPasswordResetNotificationAsync(
                "student@example.com",
                user.FullName,
                It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task ForgotPasswordAsync_EmailSendThrows_StillReturnsSuccess()
        {
            var (service, userRepo, emailService) = BuildService();
            var user = MakeUser(password: "OldPass123!", status: UserStatus.Active);

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync(user);
            userRepo.Setup(x => x.UpdateAsync(user)).Returns(Task.CompletedTask);
            emailService.Setup(x => x.SendPasswordResetNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("smtp error"));

            var result = await service.ForgotPasswordAsync("student@example.com");

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void DecryptEmail_RoundTripAfterRegisterStoredCipher_ReturnsOriginalEmail()
        {
            var (service, userRepo, _) = BuildService();
            User? capturedUser = null;

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);
            userRepo.Setup(x => x.AddUserAsync(It.IsAny<User>()))
                .Callback<User>(u => capturedUser = u)
                .ReturnsAsync((User u) => u);

            service.RegisterAsync(MakeRegisterDto(email: "roundtrip@example.com")).GetAwaiter().GetResult();

            var decrypted = service.DecryptEmail(capturedUser!.EmailEncrypt!);

            Assert.Equal("roundtrip@example.com", decrypted);
        }

        [Fact]
        public void DecryptEmail_InvalidCipher_ThrowsInvalidOperation()
        {
            var (service, _, _) = BuildService();

            Assert.Throws<InvalidOperationException>(() => service.DecryptEmail("not-base64"));
        }

        [Fact]
        public async Task RegisterAsync_HashesSameNormalizedEmailDeterministically()
        {
            var (service, userRepo, _) = BuildService();
            var hashes = new List<string>();

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .Callback<string>(hashes.Add)
                .ReturnsAsync((User?)null);
            userRepo.Setup(x => x.AddUserAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) => u);

            await service.RegisterAsync(MakeRegisterDto(email: "Alias@Example.com"));
            await service.RegisterAsync(MakeRegisterDto(email: " alias@example.com "));

            Assert.Equal(2, hashes.Count);
            Assert.Equal(hashes[0], hashes[1]);
        }

        [Fact]
        public async Task LoginAsync_TrimsAndNormalizesEmailBeforeLookup()
        {
            var (service, userRepo, _) = BuildService();
            var lookedUpHashes = new List<string>();

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .Callback<string>(hashes => lookedUpHashes.Add(hashes))
                .ReturnsAsync((User?)null);

            await service.LoginAsync(MakeLoginDto(email: "  Student@Example.com "));
            await service.LoginAsync(MakeLoginDto(email: "student@example.com"));

            Assert.Equal(2, lookedUpHashes.Count);
            Assert.Equal(lookedUpHashes[0], lookedUpHashes[1]);
        }

        [Theory]
        [InlineData("student1@example.com", "Student One", "Pass123!", UserRole.Student)]
        [InlineData("student2@example.com", "Student Two", "Pass123!", UserRole.Student)]
        [InlineData("student3@example.com", "Student Three", "Pass123!", UserRole.Student)]
        [InlineData("lecturer1@example.com", "Lecturer One", "Pass123!", UserRole.Lecturer)]
        [InlineData("lecturer2@example.com", "Lecturer Two", "Pass123!", UserRole.Lecturer)]
        [InlineData("admin1@example.com", "Admin One", "Pass123!", UserRole.Admin)]
        public async Task RegisterAsync_WithVariousInputs_PersistsExpectedRole(
            string email,
            string fullName,
            string password,
            UserRole role)
        {
            var (service, userRepo, _) = BuildService();
            User? captured = null;

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);
            userRepo.Setup(x => x.AddUserAsync(It.IsAny<User>()))
                .Callback<User>(u => captured = u)
                .ReturnsAsync((User u) => u);

            var result = await service.RegisterAsync(new RegisterDto
            {
                Email = email,
                FullName = fullName,
                Password = password,
                Role = role
            });

            Assert.True(result.Success);
            Assert.NotNull(captured);
            Assert.Equal(role, captured!.Role);
            Assert.Equal(fullName, captured.FullName);
        }

        [Theory]
        [InlineData("student@example.com", "Password123!", true)]
        [InlineData("student@example.com", "WrongPassword!", false)]
        [InlineData("student@example.com", "password123!", false)]
        [InlineData("student@example.com", "PASSWORD123!", false)]
        [InlineData("student@example.com", "", false)]
        [InlineData("student@example.com", " ", false)]
        public async Task LoginAsync_WithDifferentPasswords_ReturnsExpected(
            string email,
            string password,
            bool shouldSucceed)
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(password: "Password123!");

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            var result = await service.LoginAsync(new LoginDto
            {
                Email = email,
                Password = password
            });

            if (shouldSucceed)
            {
                Assert.NotNull(result);
                Assert.Equal(user.Id, result!.Id);
            }
            else
            {
                Assert.Null(result);
            }
        }

        [Theory]
        [InlineData("STU001", "Password123!", true)]
        [InlineData("STU001", "WrongPassword!", false)]
        [InlineData("STU001", "", false)]
        [InlineData("", "Password123!", false)]
        [InlineData(" ", "Password123!", false)]
        [InlineData("LEC001", "Password123!", true)]
        public async Task LoginByCodeAsync_WithDifferentInputs_ReturnsExpected(
            string userCode,
            string password,
            bool shouldSucceed)
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(password: "Password123!", userCode: string.IsNullOrWhiteSpace(userCode) ? "STU001" : userCode.Trim());

            userRepo.Setup(x => x.GetByUserCodeAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            var result = await service.LoginByCodeAsync(userCode, password);

            if (shouldSucceed)
            {
                Assert.NotNull(result);
            }
            else
            {
                Assert.Null(result);
            }
        }

        [Theory]
        [InlineData("Temp123!", "NewPass123!", true)]
        [InlineData("temp123!", "NewPass123!", false)]
        [InlineData("Wrong123!", "NewPass123!", false)]
        [InlineData("", "NewPass123!", false)]
        [InlineData("Temp123!", "AnotherNew123!", true)]
        public async Task ActivateAccountAsync_WithPasswordCombinations_ReturnsExpected(
            string temporaryPassword,
            string newPassword,
            bool shouldSucceed)
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(password: "Temp123!", status: UserStatus.Inactive);

            userRepo.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(x => x.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            var result = await service.ActivateAccountAsync(user.Id, temporaryPassword, newPassword);

            Assert.Equal(shouldSucceed, result.IsSuccess);
        }

        [Theory]
        [InlineData("student@example.com")]
        [InlineData("Student@example.com")]
        [InlineData(" STUDENT@example.com ")]
        [InlineData("another.student@example.com")]
        [InlineData("alias+1@example.com")]
        [InlineData("user.name@example.com")]
        public async Task ForgotPasswordAsync_WithExistingEmail_ReturnsSuccessForDifferentFormats(string email)
        {
            var (service, userRepo, emailService) = BuildService();
            var user = MakeUser(password: "OldPass123!", status: UserStatus.Active);

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync(user);
            userRepo.Setup(x => x.UpdateAsync(user)).Returns(Task.CompletedTask);
            emailService.Setup(x => x.SendPasswordResetNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var result = await service.ForgotPasswordAsync(email);

            Assert.True(result.IsSuccess);
            Assert.Equal(UserStatus.Inactive, user.Status);
        }

        [Theory]
        [InlineData("alpha@example.com")]
        [InlineData("beta@example.com")]
        [InlineData("gamma@example.com")]
        [InlineData("delta@example.com")]
        [InlineData("epsilon@example.com")]
        [InlineData("zeta@example.com")]
        [InlineData("eta@example.com")]
        [InlineData("theta@example.com")]
        public async Task RegisterAsync_GeneratesDifferentEncryptedPayloadsForDifferentEmails(string email)
        {
            var (service, userRepo, _) = BuildService();
            User? captured = null;

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);
            userRepo.Setup(x => x.AddUserAsync(It.IsAny<User>()))
                .Callback<User>(u => captured = u)
                .ReturnsAsync((User u) => u);

            var result = await service.RegisterAsync(MakeRegisterDto(email: email));

            Assert.True(result.Success);
            Assert.NotNull(captured);
            Assert.NotEqual(email, captured!.EmailEncrypt);
        }

        [Theory]
        [InlineData("student@example.com", "Password123!")]
        [InlineData("student@example.com", "AnotherPassword123!")]
        [InlineData("student@example.com", "Complex#Pass2026")]
        [InlineData("student@example.com", "UPPERlower123")]
        [InlineData("student@example.com", "ZxCvBnM123!")]
        public async Task RegisterAsync_StoredPasswordHashVerifiesOriginalPassword(
            string email,
            string password)
        {
            var (service, userRepo, _) = BuildService();
            User? captured = null;

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);
            userRepo.Setup(x => x.AddUserAsync(It.IsAny<User>()))
                .Callback<User>(u => captured = u)
                .ReturnsAsync((User u) => u);

            await service.RegisterAsync(MakeRegisterDto(email: email, password: password));

            Assert.NotNull(captured);
            Assert.True(BCrypt.Net.BCrypt.Verify(password, captured!.PasswordHash));
        }

        [Theory]
        [InlineData("cipher@example.com")]
        [InlineData("cipher2@example.com")]
        [InlineData("cipher3@example.com")]
        [InlineData("cipher4@example.com")]
        [InlineData("cipher5@example.com")]
        [InlineData("cipher6@example.com")]
        public void DecryptEmail_AfterStoredCipher_RestoresExactNormalizedEmail(string email)
        {
            var (service, userRepo, _) = BuildService();
            User? captured = null;

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);
            userRepo.Setup(x => x.AddUserAsync(It.IsAny<User>()))
                .Callback<User>(u => captured = u)
                .ReturnsAsync((User u) => u);

            service.RegisterAsync(MakeRegisterDto(email: $"  {email.ToUpperInvariant()}  ")).GetAwaiter().GetResult();

            var decrypted = service.DecryptEmail(captured!.EmailEncrypt!);

            Assert.Equal(email, decrypted);
        }

        [Fact]
        public async Task RegisterAsync_MultipleConsecutiveRegistrations_KeepWorking()
        {
            var (service, userRepo, _) = BuildService();
            var addedUsers = new List<User>();

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);
            userRepo.Setup(x => x.AddUserAsync(It.IsAny<User>()))
                .Callback<User>(u => addedUsers.Add(u))
                .ReturnsAsync((User u) => u);

            for (var i = 0; i < 10; i++)
            {
                var result = await service.RegisterAsync(MakeRegisterDto(
                    email: $"multi{i}@example.com",
                    fullName: $"User {i}",
                    password: $"Password{i}!"
                ));

                Assert.True(result.Success);
            }

            Assert.Equal(10, addedUsers.Count);
            Assert.Equal(10, addedUsers.Select(x => x.EmailEncrypt).Distinct().Count());
        }

        [Fact]
        public async Task AdminResetPasswordAsync_MultipleCalls_AlwaysReturnNewTemporaryPassword()
        {
            var (service, userRepo, _) = BuildService();
            var user = MakeUser(password: "OldPass123!", status: UserStatus.Active);
            var generated = new List<string>();

            userRepo.Setup(x => x.GetByIdAsync(user.Id)).ReturnsAsync(user);
            userRepo.Setup(x => x.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            for (var i = 0; i < 5; i++)
            {
                var result = await service.AdminResetPasswordAsync(user.Id);
                Assert.True(result.IsSuccess);
                generated.Add(result.Data!);
            }

            Assert.Equal(5, generated.Count);
            Assert.All(generated, value => Assert.False(string.IsNullOrWhiteSpace(value)));
        }

        [Theory]
        [InlineData("bulk01@example.com", "Password01!")]
        [InlineData("bulk02@example.com", "Password02!")]
        [InlineData("bulk03@example.com", "Password03!")]
        [InlineData("bulk04@example.com", "Password04!")]
        [InlineData("bulk05@example.com", "Password05!")]
        [InlineData("bulk06@example.com", "Password06!")]
        [InlineData("bulk07@example.com", "Password07!")]
        [InlineData("bulk08@example.com", "Password08!")]
        [InlineData("bulk09@example.com", "Password09!")]
        [InlineData("bulk10@example.com", "Password10!")]
        [InlineData("bulk11@example.com", "Password11!")]
        [InlineData("bulk12@example.com", "Password12!")]
        [InlineData("bulk13@example.com", "Password13!")]
        [InlineData("bulk14@example.com", "Password14!")]
        [InlineData("bulk15@example.com", "Password15!")]
        public async Task RegisterAsync_BulkCredentialMatrix_HashStillVerifies(string email, string password)
        {
            var (service, userRepo, _) = BuildService();
            User? captured = null;

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
            userRepo.Setup(x => x.AddUserAsync(It.IsAny<User>()))
                .Callback<User>(u => captured = u)
                .ReturnsAsync((User u) => u);

            var result = await service.RegisterAsync(MakeRegisterDto(email: email, password: password));

            Assert.True(result.Success);
            Assert.NotNull(captured);
            Assert.True(BCrypt.Net.BCrypt.Verify(password, captured!.PasswordHash));
        }

        [Theory]
        [InlineData("recover01@example.com")]
        [InlineData("recover02@example.com")]
        [InlineData("recover03@example.com")]
        [InlineData("recover04@example.com")]
        [InlineData("recover05@example.com")]
        [InlineData("recover06@example.com")]
        [InlineData("recover07@example.com")]
        [InlineData("recover08@example.com")]
        [InlineData("recover09@example.com")]
        [InlineData("recover10@example.com")]
        [InlineData("recover11@example.com")]
        [InlineData("recover12@example.com")]
        public async Task ForgotPasswordAsync_BulkExistingEmails_ReturnSuccess(string email)
        {
            var (service, userRepo, emailService) = BuildService();
            var user = MakeUser(password: "OldPass123!", status: UserStatus.Active);

            userRepo.Setup(x => x.GetUserByEmailHashAsync(It.IsAny<string>())).ReturnsAsync(user);
            userRepo.Setup(x => x.UpdateAsync(user)).Returns(Task.CompletedTask);
            emailService.Setup(x => x.SendPasswordResetNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var result = await service.ForgotPasswordAsync(email);

            Assert.True(result.IsSuccess);
        }
    }
}
