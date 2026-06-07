using Microsoft.Extensions.Configuration;
using Moq;
using Spectrum.API.Dtos.Auth;
using Spectrum.API.Exceptions;
using Spectrum.API.Models;
using Spectrum.API.Repositories;
using Spectrum.API.Utilities;
using System.IdentityModel.Tokens.Jwt;

namespace Spectrum.Tests.UnitTests.Utilities
{
    public class AuthUtilitiesTests
    {
        private readonly Mock<IUserRepository> _userRepositoryMock;
        private readonly Mock<IAdminDetailRepository> _adminRepositoryMock;
        private readonly Mock<IConfiguration> _configMock;

        public AuthUtilitiesTests()
        {
            _userRepositoryMock = new Mock<IUserRepository>();
            _adminRepositoryMock = new Mock<IAdminDetailRepository>();
            _configMock = new Mock<IConfiguration>();
        }

        [Fact]
        public async Task TestValidateLoginInputWhenUserIsNullShouldThrowsSpectrumUnauthorizedException()
        {
            User? nullUser = null;
            var loginDto = new LoginDto { Password = "AnyPassword" };

            var exception = await Assert.ThrowsAsync<SpectrumUnauthorizedException>(() =>
                AuthUtilities.ValidateLoginInput(nullUser, loginDto));

            Assert.Equal(Constants.ErrorMessages.InvalidCredentials, exception.Message);
        }

        [Fact]
        public async Task TestValidateLoginInputWhenUserIsDeletedShouldThrowSpectrumUnauthorizedException()
        {
            var deletedUser = new User { IsDeleted = true };
            var loginDto = new LoginDto { Password = "AnyPassword" };

            var exception = await Assert.ThrowsAsync<SpectrumUnauthorizedException>(() =>
                AuthUtilities.ValidateLoginInput(deletedUser, loginDto));

            Assert.Equal(Constants.ErrorMessages.AccountDeleted, exception.Message);
        }

        [Fact]
        public async Task TestValidateLoginInputWhenInvalidPasswordShouldThrowSpectrumUnauthorizedException()
        {
            var user = new User
            {
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword123!"),
                IsEmailVerified = true
            };
            var loginDto = new LoginDto { Password = "WrongPassword" };

            var exception = await Assert.ThrowsAsync<SpectrumUnauthorizedException>(() =>
                AuthUtilities.ValidateLoginInput(user, loginDto));

            Assert.Equal(Constants.ErrorMessages.InvalidCredentials, exception.Message);
        }

        [Fact]
        public async Task TestValidateLoginInputWhenUserIsSuspendedShouldThrowSpectrumUnauthorizedException()
        {
            var user = new User
            {
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword123!"),
                IsSuspended = true,
                IsEmailVerified = true
            };
            var loginDto = new LoginDto { Password = "CorrectPassword123!" };

            var exception = await Assert.ThrowsAsync<SpectrumUnauthorizedException>(() =>
                AuthUtilities.ValidateLoginInput(user, loginDto));

            Assert.Equal(Constants.ErrorMessages.AccountSuspended, exception.Message);
        }

        [Fact]
        public async Task TestValidateLoginInputWhenValidCredentialsShouldCompleteSuccessfully()
        {
            var user = new User
            {
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword123!"),
                IsSuspended = false,
                IsDeleted = false,
                IsEmailVerified = true
            };
            var loginDto = new LoginDto { Password = "CorrectPassword123!" };

            var task = AuthUtilities.ValidateLoginInput(user, loginDto);

            await task;
            Assert.True(task.IsCompletedSuccessfully);
        }

        [Fact]
        public async Task TestValidateRegisterInputWhenEmailExistsShouldThrowSpectrumBusinessException()
        {
            var dto = new RegisterDto { Email = "test@test.com", Username = "newuser" };
            _userRepositoryMock.Setup(repo => repo.EmailExistsAsync(dto.Email)).ReturnsAsync(true);

            var exception = await Assert.ThrowsAsync<SpectrumBusinessException>(() =>
                AuthUtilities.ValidateRegisterInput(dto, _userRepositoryMock.Object));
            Assert.Equal(Constants.ErrorMessages.EmailAlreadyRegistered, exception.Message);
        }

        [Fact]
        public async Task TestValidateRegisterInputWhenUsernameExistsShouldThrowSpectrumBusinessException()
        {
            var dto = new RegisterDto { Email = "test@test.com", Username = "existinguser" };
            _userRepositoryMock.Setup(repo => repo.EmailExistsAsync(dto.Email)).ReturnsAsync(false);
            _userRepositoryMock.Setup(repo => repo.UsernameExistsAsync(dto.Username)).ReturnsAsync(true);

            var exception = await Assert.ThrowsAsync<SpectrumBusinessException>(() =>
                AuthUtilities.ValidateRegisterInput(dto, _userRepositoryMock.Object));

            Assert.Equal(Constants.ErrorMessages.UsernameAlreadyTaken, exception.Message);
        }

        [Fact]
        public async Task TestValidateRegisterAdminInputWhenInvalidMasterKeyShouldThrowSpectrumUnauthorizedException()
        {
            var dto = new RegisterAdminDto { AdminSecretKey = "WrongKey" };
            var masterKey = "CorrectMasterKey";

            var exception = await Assert.ThrowsAsync<SpectrumUnauthorizedException>(() =>
                AuthUtilities.ValidateRegisterAdminInput(dto, _adminRepositoryMock.Object, masterKey));

            Assert.Equal(Constants.ErrorMessages.InvalidAdminKey, exception.Message);
        }

        [Theory]
        [InlineData("", "LastName", "1234567890", "Address", "RFC123")]
        [InlineData("FirstName", "", "1234567890", "Address", "RFC123")]
        [InlineData("FirstName", "LastName", "  ", "Address", "RFC123")]
        [InlineData("FirstName", "LastName", "1234567890", null, "RFC123")]
        [InlineData("FirstName", "LastName", "1234567890", "Address", "")]
        public async Task TestValidateRegisterAdminInputWhenMissingRequiredFieldsShouldThrowSpectrumBusinessException(
            string firstName, string lastName, string phone, string? address, string rfc)
        {
            var masterKey = "ValidKey";
            var dto = new RegisterAdminDto
            {
                AdminSecretKey = masterKey,
                FirstName = firstName,
                LastName = lastName,
                PhoneNumber = phone,
                Address = address ?? string.Empty,
                Rfc = rfc
            };

            var exception = await Assert.ThrowsAsync<SpectrumBusinessException>(() =>
                AuthUtilities.ValidateRegisterAdminInput(dto, _adminRepositoryMock.Object, masterKey));

            Assert.Equal(Constants.ErrorMessages.MissingRequiredParameter, exception.Message);
        }

        [Fact]
        public void TestGenerateJwtTokenWhenValidParametersShouldReturnValidJwtString()
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@test.com",
                Username = "testuser",
                Role = Constants.Roles.Reviewer
            };

            _configMock.Setup(c => c["JwtSettings:Secret"]).Returns("SuperSecretKeyThatIsAtLeast32BytesLongForSHA256");
            _configMock.Setup(c => c["JwtSettings:Issuer"]).Returns("TestIssuer");
            _configMock.Setup(c => c["JwtSettings:Audience"]).Returns("TestAudience");

            var tokenString = AuthUtilities.GenerateJwtToken(user, _configMock.Object);

            Assert.False(string.IsNullOrWhiteSpace(tokenString));

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(tokenString);

            Assert.Equal("TestIssuer", jwtToken.Issuer);
            Assert.Contains(jwtToken.Claims, c => c.Type == JwtRegisteredClaimNames.Email && c.Value == user.Email);
        }
    }
}
