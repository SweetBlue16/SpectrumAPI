using Spectrum.API.Models;

namespace Spectrum.API.Services.Auth
{
    /// <summary>
    /// Provides functionality for generating, validating, and consuming verification codes
    /// used in account registration, password recovery, and other verification workflows.
    /// </summary>
    public interface IVerificationCodeService
    {
        /// <summary>
        /// Creates a new verification code for the specified purpose.
        /// </summary>
        /// <param name="purpose">The verification workflow for which the code is generated.</param>
        /// <param name="email">The email address associated with the verification request.</param>
        /// <param name="userId">The optional user identifier associated with the request.</param>
        /// <returns>The generated verification code in plain text.</returns>
        Task<string> CreateCodeAsync(VerificationPurpose purpose, string email, Guid? userId);

        /// <summary>
        /// Validates and consumes a verification code, preventing further reuse.
        /// </summary>
        /// <param name="purpose">The verification workflow associated with the code.</param>
        /// <param name="email">The email address associated with the verification request.</param>
        /// <param name="code">The verification code provided by the user.</param>
        /// <param name="userId">The optional user identifier associated with the request.</param>
        Task ConsumeCodeAsync(VerificationPurpose purpose, string email, string code, Guid? userId = null);

        /// <summary>
        /// Validates a verification code and creates a temporary verification session token.
        /// </summary>
        /// <param name="purpose">The verification workflow associated with the code.</param>
        /// <param name="email">The email address associated with the verification request.</param>
        /// <param name="code">The verification code provided by the user.</param>
        /// <param name="userId">The optional user identifier associated with the request.</param>
        /// <returns>A temporary verification session token.</returns>
        Task<string> VerifyCodeAndCreateSessionAsync(VerificationPurpose purpose, string email, string code, Guid? userId = null);

        /// <summary>
        /// Consumes a previously issued verification session token.
        /// </summary>
        /// <param name="purpose">The verification workflow associated with the session.</param>
        /// <param name="email">The email address associated with the verification request.</param>
        /// <param name="verificationToken">The verification session token.</param>
        /// <param name="userId">The optional user identifier associated with the request.</param>
        Task ConsumeSessionAsync(VerificationPurpose purpose, string email, string verificationToken, Guid? userId = null);
    }
}
