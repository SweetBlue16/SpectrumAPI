using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Spectrum.API.Configuration;
using Spectrum.API.Data;
using Spectrum.API.Exceptions;
using Spectrum.API.Models;
using Spectrum.API.Utilities;
using System.Security.Cryptography;

namespace Spectrum.API.Services.Auth
{
    /// <summary>
    /// Manages the lifecycle of verification codes, including generation,
    /// validation, expiration, cooldown enforcement, and verification sessions.
    /// </summary>
    public class VerificationCodeService : IVerificationCodeService
    {
        private readonly SpectrumDbContext _context;
        private readonly VerificationCodeOptions _options;

        public VerificationCodeService(SpectrumDbContext context, IOptions<VerificationCodeOptions> options)
        {
            _context = context;
            _options = options.Value;
        }

        /// <inheritdoc />
        public async Task<string> CreateCodeAsync(VerificationPurpose purpose, string email, Guid? userId)
        {
            var normalizedEmail = NormalizeEmail(email);
            await EnforceCooldownAsync(purpose, normalizedEmail, userId);
            await InvalidateExistingCodesAsync(purpose, normalizedEmail, userId);

            var code = GenerateNumericCode(_options.CodeLength);
            var now = DateTime.UtcNow;
            var verificationCode = new VerificationCode
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                UserId = userId,
                Purpose = purpose,
                CodeHash = BCrypt.Net.BCrypt.HashPassword(code),
                Attempts = 0,
                MaxAttempts = _options.MaxAttempts,
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(_options.ExpirationMinutes)
            };

            await _context.VerificationCodes.AddAsync(verificationCode);
            await _context.SaveChangesAsync();
            return code;
        }

        /// <inheritdoc />
        public async Task ConsumeCodeAsync(VerificationPurpose purpose, string email, string code, Guid? userId = null)
        {
            var verificationCode = await ValidateCodeAsync(purpose, email, code, userId);
            verificationCode.UsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        /// <inheritdoc />
        public async Task<string> VerifyCodeAndCreateSessionAsync(VerificationPurpose purpose, string email, string code, Guid? userId = null)
        {
            var verificationCode = await ValidateCodeAsync(purpose, email, code, userId);
            var sessionToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            verificationCode.SessionTokenHash = BCrypt.Net.BCrypt.HashPassword(sessionToken);
            verificationCode.VerifiedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return sessionToken;
        }

        /// <inheritdoc />
        public async Task ConsumeSessionAsync(VerificationPurpose purpose, string email, string verificationToken, Guid? userId = null)
        {
            var normalizedEmail = NormalizeEmail(email);
            var now = DateTime.UtcNow;
            var query = ActiveCodeQuery(purpose, normalizedEmail, userId)
                .Where(code => code.VerifiedAt != null && code.SessionTokenHash != null && code.ExpiresAt > now);

            var candidates = await query
                .OrderByDescending(code => code.VerifiedAt)
                .ToListAsync();

            var verificationCode = candidates.FirstOrDefault(code =>
                BCrypt.Net.BCrypt.Verify(verificationToken, code.SessionTokenHash));

            if (verificationCode == null)
            {
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.VerificationCodeInvalid);
            }

            verificationCode.UsedAt = now;
            await _context.SaveChangesAsync();
        }

        private async Task<VerificationCode> ValidateCodeAsync(VerificationPurpose purpose, string email, string code, Guid? userId)
        {
            var normalizedEmail = NormalizeEmail(email);
            var verificationCode = await ActiveCodeQuery(purpose, normalizedEmail, userId)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync();

            if (verificationCode == null)
            {
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.VerificationCodeInvalid);
            }

            if (verificationCode.ExpiresAt <= DateTime.UtcNow)
            {
                verificationCode.UsedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.VerificationCodeExpired);
            }

            if (verificationCode.Attempts >= verificationCode.MaxAttempts)
            {
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.VerificationCodeTooManyAttempts);
            }

            if (!BCrypt.Net.BCrypt.Verify(code, verificationCode.CodeHash))
            {
                verificationCode.Attempts++;
                await _context.SaveChangesAsync();
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.VerificationCodeInvalid);
            }

            return verificationCode;
        }

        private IQueryable<VerificationCode> ActiveCodeQuery(VerificationPurpose purpose, string email, Guid? userId)
        {
            var query = _context.VerificationCodes
                .Where(code => code.Email == email && code.Purpose == purpose && code.UsedAt == null);

            if (userId.HasValue)
            {
                query = query.Where(code => code.UserId == userId.Value);
            }

            return query;
        }

        private async Task EnforceCooldownAsync(VerificationPurpose purpose, string email, Guid? userId)
        {
            var cooldownThreshold = DateTime.UtcNow.AddSeconds(-_options.ResendCooldownSeconds);
            var recentCodeExists = await ActiveCodeQuery(purpose, email, userId)
                .AnyAsync(code => code.CreatedAt > cooldownThreshold);

            if (recentCodeExists)
            {
                throw new SpectrumBusinessException(Constants.ErrorMessages.ResendCodeTooSoon);
            }
        }

        private async Task InvalidateExistingCodesAsync(VerificationPurpose purpose, string email, Guid? userId)
        {
            var existingCodes = await ActiveCodeQuery(purpose, email, userId).ToListAsync();
            foreach (var existingCode in existingCodes)
            {
                existingCode.UsedAt = DateTime.UtcNow;
            }
        }

        private static string NormalizeEmail(string email)
        {
            return email.Trim().ToLowerInvariant();
        }

        private static string GenerateNumericCode(int length)
        {
            var max = (int)Math.Pow(10, length);
            var value = RandomNumberGenerator.GetInt32(0, max);
            return value.ToString($"D{length}");
        }
    }
}
