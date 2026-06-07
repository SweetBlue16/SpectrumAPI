using Microsoft.Extensions.Configuration;
using Moq;
using Spectrum.API.Dtos.Auth;
using Spectrum.API.Exceptions;
using Spectrum.API.Models;
using Spectrum.API.Repositories;
using Spectrum.API.Services.Auth;
using Spectrum.API.Services.Email;
using Spectrum.API.Utilities;

namespace Spectrum.Tests.UnitTests.Services
{
    public class AuthServiceTests
    {
        private readonly Mock<IUserRepository> _userRepositoryMock;
        private readonly Mock<IAdminDetailRepository> _adminRepositoryMock;
        private readonly Mock<IConfiguration> _configMock;
        private readonly Mock<IVerificationCodeService> _verificationCodeServiceMock;
        private readonly Mock<IEmailService> _emailServiceMock;
        private readonly AuthService _authService;

        public AuthServiceTests()
        {
            _userRepositoryMock = new Mock<IUserRepository>();
            _adminRepositoryMock = new Mock<IAdminDetailRepository>();
            _configMock = new Mock<IConfiguration>();
            _verificationCodeServiceMock = new Mock<IVerificationCodeService>();
            _emailServiceMock = new Mock<IEmailService>();

            _configMock.Setup(c => c["JwtSettings:Secret"]).Returns("ThisIsAVerySecureAndLongSecretKeyForTesting123!");
            _configMock.Setup(c => c["JwtSettings:Issuer"]).Returns("TestIssuer");
            _configMock.Setup(c => c["JwtSettings:Audience"]).Returns("TestAudience");

            _configMock.Setup(c => c["AdminSettings:MasterKey"]).Returns("SuperSecretMasterKey");

            _authService = new AuthService(
                _userRepositoryMock.Object,
                _adminRepositoryMock.Object,
                _configMock.Object,
                _verificationCodeServiceMock.Object,
                _emailServiceMock.Object
            );
        }

        [Fact]
        public async Task TestRegisterAsyncWhenValidInputShouldReturnVerificationResponseAndCreateUser()
        {
            var registerDto = new RegisterDto
            {
                Username = "new_user",
                Email = "new@test.com",
                Password = "StrongPassword123!"
            };

            _userRepositoryMock.Setup(r => r.EmailExistsAsync(registerDto.Email)).ReturnsAsync(false);
            _userRepositoryMock.Setup(r => r.UsernameExistsAsync(registerDto.Username)).ReturnsAsync(false);
            _userRepositoryMock.Setup(r => r.AddUserAsync(It.IsAny<User>())).ReturnsAsync((User u) => u);
            _verificationCodeServiceMock
                .Setup(s => s.CreateCodeAsync(VerificationPurpose.RegisterVerification, registerDto.Email, It.IsAny<Guid?>()))
                .ReturnsAsync("123456");

            var result = await _authService.RegisterAsync(registerDto);

            Assert.NotNull(result);
            Assert.Equal(registerDto.Email, result.Email);
            Assert.True(result.RequiresVerification);

            _userRepositoryMock.Verify(r => r.AddUserAsync(It.IsAny<User>()), Times.Once);
            _emailServiceMock.Verify(s => s.SendRegistrationVerificationAsync(registerDto.Email, "123456"), Times.Once);
        }

        [Fact]
        public async Task TestRegisterAsyncWhenEmailAlreadyExistsShouldThrowSpectrumBusinessException()
        {
            var registerDto = new RegisterDto { Username = "newuser", Email = "taken@test.com", Password = "Password123!" };
            _userRepositoryMock.Setup(r => r.EmailExistsAsync(registerDto.Email)).ReturnsAsync(true);

            var exception = await Assert.ThrowsAsync<SpectrumBusinessException>(() =>
                _authService.RegisterAsync(registerDto));

            Assert.Equal(Constants.ErrorMessages.EmailAlreadyRegistered, exception.Message);
            _userRepositoryMock.Verify(r => r.AddUserAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task TestRegisterAsyncWhenUsernameAlreadyExistsShouldThrowSpectrumBusinessException()
        {
            var registerDto = new RegisterDto { Username = "taken_user", Email = "new@test.com", Password = "Password123!" };
            _userRepositoryMock.Setup(r => r.EmailExistsAsync(registerDto.Email)).ReturnsAsync(false);
            _userRepositoryMock.Setup(r => r.UsernameExistsAsync(registerDto.Username)).ReturnsAsync(true);

            var exception = await Assert.ThrowsAsync<SpectrumBusinessException>(() =>
                _authService.RegisterAsync(registerDto));

            Assert.Equal(Constants.ErrorMessages.UsernameAlreadyTaken, exception.Message);
            _userRepositoryMock.Verify(r => r.AddUserAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task TestLoginAsyncWhenValidCredentialsShouldReturnAuthResponseDto()
        {
            var password = "CorrectPassword123!";
            var loginDto = new LoginDto { Email = "test@test.com", Password = password };
            var existingUser = new User
            {
                Id = Guid.NewGuid(),
                Email = loginDto.Email,
                Username = "test_user",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = Constants.Roles.Reviewer,
                IsDeleted = false,
                IsSuspended = false,
                IsEmailVerified = true
            };

            _userRepositoryMock.Setup(r => r.GetUserByEmailAsync(loginDto.Email)).ReturnsAsync(existingUser);

            var result = await _authService.LoginAsync(loginDto);

            Assert.NotNull(result);
            Assert.Equal(existingUser.Username, result.Username);
            Assert.Equal(existingUser.Email, result.Email);
            Assert.False(string.IsNullOrWhiteSpace(result.Token));
        }

        [Fact]
        public async Task TestLoginAsyncWhenDemoAdminSeedCredentialsShouldReturnAdminAuthResponseDto()
        {
            var password = "DemoAdmin123!";
            var loginDto = new LoginDto { Email = "admin@demo.spectrum.local", Password = password };
            var existingUser = new User
            {
                Id = Guid.NewGuid(),
                Email = loginDto.Email,
                Username = "DEMO_Admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = Constants.Roles.Admin,
                IsDeleted = false,
                IsSuspended = false,
                IsEmailVerified = true
            };

            _userRepositoryMock.Setup(r => r.GetUserByEmailAsync(loginDto.Email)).ReturnsAsync(existingUser);

            var result = await _authService.LoginAsync(loginDto);

            Assert.NotNull(result);
            Assert.Equal("DEMO_Admin", result.Username);
            Assert.Equal(loginDto.Email, result.Email);
            Assert.Equal(Constants.Roles.Admin, result.Role);
            Assert.False(string.IsNullOrWhiteSpace(result.Token));
        }

        [Fact]
        public async Task TestLoginAsyncWhenUserDoesNotExistShouldThrowSpectrumUnauthorizedException()
        {
            var loginDto = new LoginDto { Email = "ghost@test.com", Password = "Password123!" };
            _userRepositoryMock.Setup(r => r.GetUserByEmailAsync(loginDto.Email)).ReturnsAsync((User?)null);

            var exception = await Assert.ThrowsAsync<SpectrumUnauthorizedException>(() =>
                _authService.LoginAsync(loginDto));

            Assert.Equal(Constants.ErrorMessages.InvalidCredentials, exception.Message);
        }

        [Fact]
        public async Task TestLoginAsyncWhenWrongPasswordShouldThrowSpectrumUnauthorizedException()
        {
            var loginDto = new LoginDto { Email = "test@test.com", Password = "WrongPassword!" };
            var existingUser = new User
            {
                Email = loginDto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword123!"),
                IsEmailVerified = true
            };

            _userRepositoryMock.Setup(r => r.GetUserByEmailAsync(loginDto.Email)).ReturnsAsync(existingUser);

            var exception = await Assert.ThrowsAsync<SpectrumUnauthorizedException>(() =>
                _authService.LoginAsync(loginDto));

            Assert.Equal(Constants.ErrorMessages.InvalidCredentials, exception.Message);
        }

        [Fact]
        public async Task TestLoginAsyncWhenUserIsSuspendedShouldThrowSpectrumUnauthorizedException()
        {
            var password = "CorrectPassword123!";
            var loginDto = new LoginDto { Email = "test@test.com", Password = password };
            var existingUser = new User
            {
                Email = loginDto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                IsSuspended = true,
                IsEmailVerified = true
            };

            _userRepositoryMock.Setup(r => r.GetUserByEmailAsync(loginDto.Email)).ReturnsAsync(existingUser);

            var exception = await Assert.ThrowsAsync<SpectrumUnauthorizedException>(() =>
                _authService.LoginAsync(loginDto));

            Assert.Equal(Constants.ErrorMessages.AccountSuspended, exception.Message);
        }

        [Fact]
        public async Task TestLoginAsyncWhenUserIsBannedShouldThrowSpectrumUnauthorizedException()
        {
            var password = "CorrectPassword123!";
            var loginDto = new LoginDto { Email = "banned@test.com", Password = password };
            var existingUser = new User
            {
                Email = loginDto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                IsBanned = true,
                IsEmailVerified = true
            };

            _userRepositoryMock
                .Setup(r => r.GetUserByEmailIncludingDeletedAsync(loginDto.Email))
                .ReturnsAsync(existingUser);

            var exception = await Assert.ThrowsAsync<SpectrumUnauthorizedException>(() =>
                _authService.LoginAsync(loginDto));

            Assert.Equal(Constants.ErrorMessages.AccountBanned, exception.Message);
        }

        [Fact]
        public async Task ForgotPasswordAsyncWhenEmailIsNotRegisteredShouldThrowBusinessException()
        {
            var dto = new ForgotPasswordDto { Email = "missing@test.com" };
            _userRepositoryMock
                .Setup(r => r.GetUserByEmailIncludingDeletedAsync(dto.Email))
                .ReturnsAsync((User?)null);

            var exception = await Assert.ThrowsAsync<SpectrumBusinessException>(() =>
                _authService.ForgotPasswordAsync(dto));

            Assert.Equal(Constants.ErrorMessages.EmailNotRegistered, exception.Message);
        }

        [Fact]
        public async Task ForgotPasswordAsyncWhenEmailExistsShouldSendResetCode()
        {
            var dto = new ForgotPasswordDto { Email = "user@test.com" };
            var user = new User { Id = Guid.NewGuid(), Email = dto.Email, IsEmailVerified = true };
            _userRepositoryMock
                .Setup(r => r.GetUserByEmailIncludingDeletedAsync(dto.Email))
                .ReturnsAsync(user);
            _verificationCodeServiceMock
                .Setup(service => service.CreateCodeAsync(VerificationPurpose.PasswordReset, dto.Email, user.Id))
                .ReturnsAsync("123456");

            var result = await _authService.ForgotPasswordAsync(dto);

            Assert.Equal(Constants.ErrorMessages.PasswordResetInstructionsSent, result.Message);
            _emailServiceMock.Verify(service => service.SendPasswordResetAsync(dto.Email, "123456"), Times.Once);
        }

        [Fact]
        public async Task TestRegisterAdminAsyncWhenValidInputShouldCreateAdminUserAndDetails()
        {
            var masterKey = "SuperSecretMasterKey";

            var adminDto = new RegisterAdminDto
            {
                Username = "admin_user",
                Email = "admin@test.com",
                Password = "AdminPassword123!",
                FirstName = "John",
                LastName = "Doe",
                PhoneNumber = "+1234567890",
                Address = "123 Admin St",
                Rfc = "ABCD123456EFG",
                AdminSecretKey = masterKey
            };

            _userRepositoryMock.Setup(r => r.EmailExistsAsync(adminDto.Email)).ReturnsAsync(false);
            _userRepositoryMock.Setup(r => r.UsernameExistsAsync(adminDto.Username)).ReturnsAsync(false);
            _userRepositoryMock.Setup(r => r.AddUserAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) =>
                {
                    u.Id = Guid.NewGuid();
                    return u;
                });

            var result = await _authService.RegisterAdminAsync(adminDto);

            Assert.NotNull(result);
            Assert.Equal(adminDto.Username, result.Username);
            Assert.Equal(Constants.Roles.Admin, result.Role);
            Assert.False(string.IsNullOrWhiteSpace(result.Token));

            _userRepositoryMock.Verify(r => r.AddUserAsync(It.Is<User>(u => u.Role == Constants.Roles.Admin)), Times.Once);
            _adminRepositoryMock.Verify(r => r.AddAdminDetailAsync(It.IsAny<AdminDetail>()), Times.Once);
        }
    }
}
