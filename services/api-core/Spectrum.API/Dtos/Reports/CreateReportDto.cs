using Spectrum.API.Utilities;
using System.ComponentModel.DataAnnotations;

namespace Spectrum.API.Dtos.Reports
{
    public class CreateReportDto
    {
        public required Guid TargetId { get; set; }

        [Required]
        [RegularExpression("^(REVIEW|COMMENT|USER|GAME_CLIP)$", ErrorMessage = "TargetType must be REVIEW, COMMENT, USER or GAME_CLIP.")]
        public string TargetType { get; set; } = string.Empty;

        [Required]
        [MaxLength(InputValidationLimits.ShortText)]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(InputValidationLimits.ReportDescription)]
        public string? Description { get; set; }
    }
}
