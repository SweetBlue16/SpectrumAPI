using Spectrum.API.Utilities;
using System.ComponentModel.DataAnnotations;

namespace Spectrum.API.Dtos.Drops
{
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

        public required DateTime StartAt { get; set; }
        public required DateTime JoinDeadlineAt { get; set; }
        public required DateTime RevealAt { get; set; }
        public required DateTime EndAt { get; set; }
        public int TotalSlots { get; set; }

        [MaxLength(InputValidationLimits.ShortText)]
        public string PublicChallengeCode { get; set; } = string.Empty;

        public List<string> AccessKeys { get; set; } = new();
    }
}
