using System.ComponentModel.DataAnnotations;

namespace Spectrum.API.Dtos.Profile
{
    /// <summary>
    /// Data transfer object for secure password change operations.
    /// </summary>
    public class ChangePasswordDto
    {
        /// <summary>
        /// Current account password used for verification.
        /// </summary>
        [Required]
        public string CurrentPassword { get; set; } = string.Empty;

        /// <summary>
        /// New password that will replace the current password.
        /// </summary>
        [Required]
        [MinLength(8)]
        public string NewPassword { get; set; } = string.Empty;
    }
}