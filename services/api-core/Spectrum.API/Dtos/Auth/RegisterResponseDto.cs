namespace Spectrum.API.Dtos.Auth
{
    /// <summary>
    /// Represents the result of a successful user registration request.
    /// </summary>
    public class RegisterResponseDto
    {
        /// <summary>
        /// Email address associated with the newly created account.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether the account requires email verification before activation.
        /// </summary>
        public bool RequiresVerification { get; set; }

        /// <summary>
        /// Human-readable message describing the registration result.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
