using Microsoft.Extensions.Options;
using Spectrum.API.Configuration;
using Spectrum.API.Exceptions;
using Spectrum.API.Models;
using Spectrum.API.Services.Auth;
using Spectrum.API.Utilities;
using Spectrum.Tests.Helpers;

namespace Spectrum.Tests.UnitTests.Services
{
    public class VerificationCodeServiceTests
    {
        [Fact]
        public async Task CreateCodeAsyncShouldNormalizeEmailAndInvalidateOlderCodes()
        {
            await using var context = TestDbContextFactory.CreateContext();
            var oldCode = new VerificationCode
            {
                Id = Guid.NewGuid(),
                Email = "user@test.com",
                Purpose = VerificationPurpose.RegisterVerification,
                CodeHash = BCrypt.Net.BCrypt.HashPassword("111111"),
                MaxAttempts = 3,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                ExpiresAt = DateTime.UtcNow.AddMinutes(10)
            };
            await context.VerificationCodes.AddAsync(oldCode);
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var code = await service.CreateCodeAsync(VerificationPurpose.RegisterVerification, " USER@Test.COM ", null);

            Assert.Equal(6, code.Length);
            Assert.True(code.All(char.IsDigit));
            Assert.NotNull(oldCode.UsedAt);
            Assert.Equal(2, context.VerificationCodes.Count());
            Assert.All(context.VerificationCodes, item => Assert.Equal("user@test.com", item.Email));
        }

        [Fact]
        public async Task CreateCodeAsyncWhenCooldownIsActiveShouldThrow()
        {
            await using var context = TestDbContextFactory.CreateContext();
            await context.VerificationCodes.AddAsync(new VerificationCode
            {
                Id = Guid.NewGuid(),
                Email = "user@test.com",
                Purpose = VerificationPurpose.PasswordReset,
                CodeHash = BCrypt.Net.BCrypt.HashPassword("111111"),
                MaxAttempts = 3,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10)
            });
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var exception = await Assert.ThrowsAsync<SpectrumBusinessException>(() =>
                service.CreateCodeAsync(VerificationPurpose.PasswordReset, "user@test.com", null));

            Assert.Equal(Constants.ErrorMessages.ResendCodeTooSoon, exception.Message);
        }

        [Fact]
        public async Task ConsumeCodeAsyncWhenCodeIsValidShouldMarkItUsed()
        {
            await using var context = TestDbContextFactory.CreateContext();
            await AddCodeAsync(context, VerificationPurpose.RegisterVerification, "user@test.com", "123456");
            var service = CreateService(context);

            await service.ConsumeCodeAsync(VerificationPurpose.RegisterVerification, "user@test.com", "123456");

            Assert.NotNull(context.VerificationCodes.Single().UsedAt);
        }

        [Fact]
        public async Task ConsumeCodeAsyncWhenCodeIsInvalidShouldIncrementAttempts()
        {
            await using var context = TestDbContextFactory.CreateContext();
            await AddCodeAsync(context, VerificationPurpose.RegisterVerification, "user@test.com", "123456");
            var service = CreateService(context);

            var exception = await Assert.ThrowsAsync<SpectrumUnauthorizedException>(() =>
                service.ConsumeCodeAsync(VerificationPurpose.RegisterVerification, "user@test.com", "000000"));

            Assert.Equal(Constants.ErrorMessages.VerificationCodeInvalid, exception.Message);
            Assert.Equal(1, context.VerificationCodes.Single().Attempts);
        }

        [Fact]
        public async Task ConsumeCodeAsyncWhenCodeIsExpiredShouldMarkItUsedAndThrow()
        {
            await using var context = TestDbContextFactory.CreateContext();
            await AddCodeAsync(context, VerificationPurpose.RegisterVerification, "user@test.com", "123456", expiresAt: DateTime.UtcNow.AddSeconds(-1));
            var service = CreateService(context);

            var exception = await Assert.ThrowsAsync<SpectrumUnauthorizedException>(() =>
                service.ConsumeCodeAsync(VerificationPurpose.RegisterVerification, "user@test.com", "123456"));

            Assert.Equal(Constants.ErrorMessages.VerificationCodeExpired, exception.Message);
            Assert.NotNull(context.VerificationCodes.Single().UsedAt);
        }

        [Fact]
        public async Task VerifyCodeAndConsumeSessionShouldCreateAndUseSessionToken()
        {
            await using var context = TestDbContextFactory.CreateContext();
            var userId = Guid.NewGuid();
            await AddCodeAsync(context, VerificationPurpose.PasswordChange, "user@test.com", "123456", userId: userId);
            var service = CreateService(context);

            var token = await service.VerifyCodeAndCreateSessionAsync(
                VerificationPurpose.PasswordChange,
                "user@test.com",
                "123456",
                userId);
            await service.ConsumeSessionAsync(VerificationPurpose.PasswordChange, "user@test.com", token, userId);

            var stored = context.VerificationCodes.Single();
            Assert.NotNull(stored.VerifiedAt);
            Assert.NotNull(stored.SessionTokenHash);
            Assert.NotNull(stored.UsedAt);
        }

        [Fact]
        public async Task ConsumeSessionAsyncWhenTokenDoesNotMatchShouldThrow()
        {
            await using var context = TestDbContextFactory.CreateContext();
            await AddCodeAsync(context, VerificationPurpose.PasswordReset, "user@test.com", "123456", sessionToken: "real-token");
            var service = CreateService(context);

            var exception = await Assert.ThrowsAsync<SpectrumUnauthorizedException>(() =>
                service.ConsumeSessionAsync(VerificationPurpose.PasswordReset, "user@test.com", "wrong-token"));

            Assert.Equal(Constants.ErrorMessages.VerificationCodeInvalid, exception.Message);
        }

        private static VerificationCodeService CreateService(Spectrum.API.Data.SpectrumDbContext context)
        {
            return new VerificationCodeService(context, Options.Create(new VerificationCodeOptions
            {
                CodeLength = 6,
                ExpirationMinutes = 10,
                MaxAttempts = 3,
                ResendCooldownSeconds = 60
            }));
        }

        private static async Task AddCodeAsync(
            Spectrum.API.Data.SpectrumDbContext context,
            VerificationPurpose purpose,
            string email,
            string code,
            DateTime? expiresAt = null,
            Guid? userId = null,
            string? sessionToken = null)
        {
            await context.VerificationCodes.AddAsync(new VerificationCode
            {
                Id = Guid.NewGuid(),
                Email = email,
                UserId = userId,
                Purpose = purpose,
                CodeHash = BCrypt.Net.BCrypt.HashPassword(code),
                SessionTokenHash = sessionToken is null ? null : BCrypt.Net.BCrypt.HashPassword(sessionToken),
                VerifiedAt = sessionToken is null ? null : DateTime.UtcNow,
                Attempts = 0,
                MaxAttempts = 3,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                ExpiresAt = expiresAt ?? DateTime.UtcNow.AddMinutes(10)
            });
            await context.SaveChangesAsync();
        }
    }
}
