using System.ComponentModel.DataAnnotations;

namespace Spectrum.API.Dtos.Profile
{
    /// <summary>
    /// Request used by administrators when applying moderation actions to a user account.
    /// </summary>
    public class AdminModerationActionDto
    {
        /// <summary>
        /// Optional reason explaining why the moderation action was performed.
        /// </summary>
        [MaxLength(300)]
        public string? Reason { get; set; }
    }
}
