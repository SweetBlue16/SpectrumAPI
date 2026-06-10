using Spectrum.API.Utilities;
using System.ComponentModel.DataAnnotations;

namespace Spectrum.API.Dtos.Drops
{
    /// <summary>
    /// Payload used to create a new giveaway event.
    /// </summary>
    public class CreateDropEventDto
    {
        [Required]
        [MaxLength(InputValidationLimits.ShortText)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(InputValidationLimits.MediumText)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(255)]
        public string ImageUrl { get; set; } = string.Empty;

        [Required]
        [MaxLength(InputValidationLimits.ShortText)]
        public string GameTitle { get; set; } = string.Empty;

        public int? RawgGameId { get; set; }

        [Required]
        [MaxLength(InputValidationLimits.ShortText)]
        public string Platform { get; set; } = string.Empty;

        /// <summary>
        /// Date and time when users can start joining the event.
        /// </summary>
        public required DateTime StartAt { get; set; }

        /// <summary>
        /// Registration deadline for participants.
        /// </summary>
        public required DateTime JoinDeadlineAt { get; set; }

        /// <summary>
        /// Date and time when winners can begin claiming rewards.
        /// </summary>
        public required DateTime RevealAt { get; set; }

        /// <summary>
        /// Date and time when the giveaway event ends.
        /// </summary>
        public required DateTime EndAt { get; set; }

        /// <summary>
        /// Maximum number of participants allowed.
        /// </summary>
        public required int TotalSlots { get; set; }

        /// <summary>
        /// Public challenge code required during reward claiming.
        /// </summary>
        [MaxLength(InputValidationLimits.ShortText)]
        public string PublicChallengeCode { get; set; } = string.Empty;

        /// <summary>
        /// Reward keys available for distribution.
        /// </summary>
        public List<string> AccessKeys { get; set; } = new();
    }
}
