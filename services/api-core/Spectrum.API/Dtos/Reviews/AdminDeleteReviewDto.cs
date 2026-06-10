using System.ComponentModel.DataAnnotations;

namespace Spectrum.API.Dtos.Reviews
{
    /// <summary>
    /// Request used by administrators to delete a review and record the moderation reason.
    /// </summary>
    public class AdminDeleteReviewDto
    {
        /// <summary>
        /// Reason explaining why the review is being removed.
        /// </summary>
        [Required]
        [MinLength(10)]
        [MaxLength(300)]
        public string Reason { get; set; } = string.Empty;
    }
}
