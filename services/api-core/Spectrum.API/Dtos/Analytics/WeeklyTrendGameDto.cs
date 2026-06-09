namespace Spectrum.API.Dtos.Analytics
{
    /// <summary>
    /// Represents a game included in the weekly trends ranking.
    /// </summary>
    public class WeeklyTrendGameDto
    {
        /// <summary>
        /// Position within the weekly ranking.
        /// </summary>
        public int Rank { get; set; }

        /// <summary>
        /// RAWG game identifier.
        /// </summary>
        public int GameId { get; set; }

        /// <summary>
        /// Display title of the game.
        /// </summary>
        public string GameTitle { get; set; } = string.Empty;

        /// <summary>
        /// Cover image associated with the game.
        /// </summary>
        public string CoverImageUrl { get; set; } = string.Empty;

        /// <summary>
        /// Number of reviews published for the game during the analyzed period.
        /// </summary>
        public int ReviewsCount { get; set; }

        /// <summary>
        /// Featured reviews contributing to the game's trend score.
        /// </summary>
        public IReadOnlyList<WeeklyReviewDto> Reviews { get; set; } = [];
    }
}
