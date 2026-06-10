using Spectrum.API.Utilities;
using System.ComponentModel.DataAnnotations;

namespace Spectrum.API.Dtos.Reports
{
    /// <summary>
    /// Request used to submit a moderation report against a review, comment,
    /// user profile or game clip.
    /// </summary>
    public class CreateReportDto
    {
        /// <summary>
        /// Identifier of the entity being reported.
        /// </summary>
        /// <example>550e8400-e29b-41d4-a716-446655440000</example>
        public required Guid TargetId { get; set; }

        /// <summary>
        /// Type of entity being reported.
        /// Allowed values: REVIEW, COMMENT, USER, GAME_CLIP.
        /// </summary>
        /// <example>REVIEW</example>
        [Required]
        [RegularExpression(
            "^(REVIEW|COMMENT|USER|GAME_CLIP)$",
            ErrorMessage = "TargetType must be REVIEW, COMMENT, USER or GAME_CLIP."
        )]
        public string TargetType { get; set; } = string.Empty;

        /// <summary>
        /// Primary reason for the report.
        /// </summary>
        /// <example>Contenido ofensivo</example>
        [Required]
        [MaxLength(InputValidationLimits.ShortText)]
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Optional detailed explanation that helps moderators review the report.
        /// </summary>
        /// <example>El contenido contiene lenguaje ofensivo y ataques personales.</example>
        [MaxLength(InputValidationLimits.ReportDescription)]
        public string? Description { get; set; }
    }
}