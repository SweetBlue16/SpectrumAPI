using Spectrum.API.Models;

namespace Spectrum.API.Dtos.Reviews
{
    /// <summary>
    /// Aggregated review information for a specific game.
    /// </summary>
    public class GameReviewDetailDto
    {
        /// <summary>
        /// Game information.
        /// </summary>
        public Game Game { get; set; } = new();

        /// <summary>
        /// Reviews published for the game.
        /// </summary>
        public IReadOnlyList<ReviewResponseDto> Reviews { get; set; } = Array.Empty<ReviewResponseDto>();

        /// <summary>
        /// Average rating calculated from all reviews.
        /// </summary>
        public double? AverageRating { get; set; }

        /// <summary>
        /// Total number of reviews.
        /// </summary>
        public int ReviewsCount { get; set; }
    }
}
