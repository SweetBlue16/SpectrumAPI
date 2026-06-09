using System.ComponentModel.DataAnnotations;

namespace Spectrum.API.Dtos.Auth
{
    /// <summary>
    /// Represents the information required to verify a newly registered account.
    /// </summary>
    public class VerifyRegistrationCodeDto
    {
        /// <summary>
        /// Email address associated with the account being verified.
        /// </summary>
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Six-digit verification code sent to the user's email address.
        /// </summary>
        [Required]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Verification code must contain 6 digits.")]
        public string Code { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a request to resend an account verification code.
    /// </summary>
    public class ResendRegistrationCodeDto
    {
        /// <summary>
        /// Email address associated with the pending account.
        /// </summary>
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a password recovery request.
    /// </summary>
    public class ForgotPasswordDto
    {
        /// <summary>
        /// Email address associated with the account requesting password recovery.
        /// </summary>
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents the information required to validate a password recovery code.
    /// </summary>
    public class VerifyPasswordCodeDto
    {
        /// <summary>
        /// Email address associated with the password recovery request.
        /// </summary>
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Six-digit password recovery code sent to the user's email address.
        /// </summary>
        [Required]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Verification code must contain 6 digits.")]
        public string Code { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents the result of a successful password recovery code verification.
    /// </summary>
    public class PasswordCodeVerifiedDto
    {
        /// <summary>
        /// Temporary token authorizing the password reset operation.
        /// </summary>
        public string VerificationToken { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable message describing the verification result.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents the information required to complete a password reset operation.
    /// </summary>
    public class ResetPasswordDto
    {
        /// <summary>
        /// Email address associated with the account.
        /// </summary>
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Temporary token obtained after successful verification of the recovery code.
        /// </summary>
        [Required]
        public string VerificationToken { get; set; } = string.Empty;

        /// <summary>
        /// New password that will replace the current account password.
        /// </summary>
        [Required]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$",
            ErrorMessage = "Password must be at least 8 characters long and contain one uppercase letter, one lowercase letter, and one number.")]
        public string NewPassword { get; set; } = string.Empty;
    }
}
