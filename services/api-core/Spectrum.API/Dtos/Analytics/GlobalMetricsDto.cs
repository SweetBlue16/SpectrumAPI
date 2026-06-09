namespace Spectrum.API.Dtos.Analytics
{
    /// <summary>
    /// Aggregated platform metrics for a specific reporting window.
    /// </summary>
    public class GlobalMetricsDto
    {
        /// <summary>
        /// Start date of the analyzed period.
        /// </summary>
        public DateTime WindowStart { get; set; }

        /// <summary>
        /// End date of the analyzed period.
        /// </summary>
        public DateTime WindowEnd { get; set; }

        /// <summary>
        /// User registration metrics over time.
        /// </summary>
        public IReadOnlyList<MetricPointDto> NewUsers { get; set; } = [];

        /// <summary>
        /// Review creation metrics over time.
        /// </summary>
        public IReadOnlyList<MetricPointDto> NewReviews { get; set; } = [];

        /// <summary>
        /// Most searched games during the analyzed period.
        /// </summary>
        public IReadOnlyList<TopGameMetricDto> MostSearchedGames { get; set; } = [];
    }
}
