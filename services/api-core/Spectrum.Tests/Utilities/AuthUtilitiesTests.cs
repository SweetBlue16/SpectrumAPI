using FluentAssertions;
using Spectrum.API.Dtos.Auth;
using Spectrum.API.Exceptions;
using Spectrum.API.Models;
using Spectrum.API.Utilities;

namespace Spectrum.Tests.Utilities
{
    public class AuthUtilitiesTests
    {
        [Fact]
        public void ValidateLoginInput_WithValidCredentials_ShouldNotThrowException()
        {
            var password = "SecurePassword123!";
            var user = new User
            {
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                IsSuspended = false
            };

            var loginDto = new LoginDto
            {
                Email = "test@test.com",
                Password = password
            };

            var action = () => AuthUtilities.ValidateLoginInput(user, loginDto);
            action.Should().NotThrow();
        }

        [Fact]
        public void ValidateLoginInput_WithSuspendedUser_ShouldThrowSpectrumBusinessException()
        {
            var password = "TuPasswordSegura123!";
            var user = new User
            {
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                IsSuspended = true
            };

            var loginDto = new LoginDto
            {
                Email = "test@test.com",
                Password = password
            };

            var action = () => AuthUtilities.ValidateLoginInput(user, loginDto);

            action.Should().Throw<SpectrumUnauthorizedException>()
                  .WithMessage("accountSuspended");
        }
    }
}
