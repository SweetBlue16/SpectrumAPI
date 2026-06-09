namespace Spectrum.API.Dtos.Analytics
{
    /// <summary>
    /// Represents the dashboard used by the Crypt analytics section.
    /// </summary>
    public class CryptDashboardDto
    {
        /// <summary>
        /// Start date of the analyzed month.
        /// </summary>
        public DateTime MonthStart { get; set; }

        /// <summary>
        /// End date of the analyzed month.
        /// </summary>
        public DateTime MonthEnd { get; set; }

        /// <summary>
        /// Lowest-rated games during the month.
        /// </summary>
        public IReadOnlyList<NamedMetricDto> WorstGames { get; set; } = [];

        /// <summary>
        /// Games that have not yet received reviews.
        /// </summary>
        public IReadOnlyList<NamedMetricDto> GamesWithoutReviews { get; set; } = [];
    }
}
