using System.ComponentModel.DataAnnotations;

namespace Spectrum.API.Dtos.Profile
{
    /// <summary>
    /// Request used to validate the one-time verification code sent to the user.
    /// </summary>
    public class VerifyPasswordChangeCodeDto
    {
        /// <summary>
        /// Six-digit verification code.
        /// </summary>
        [Required]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Verification code must contain 6 digits.")]
        public string Code { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request used to finalize a password change after verification.
    /// </summary>
    public class ConfirmPasswordChangeDto
    {
        /// <summary>
        /// Temporary verification token generated after code validation.
        /// </summary>
        [Required]
        public string VerificationToken { get; set; } = string.Empty;

        /// <summary>
        /// New account password.
        /// </summary>
        [Required]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$",
            ErrorMessage = "Password must be at least 8 characters long and contain one uppercase letter, one lowercase letter, and one number.")]
        public string NewPassword { get; set; } = string.Empty;
    }
}
