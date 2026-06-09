namespace Spectrum.API.Dtos.Analytics
{
    /// <summary>
    /// Represents the weekly trending games report generated from review activity and community engagement.
    /// </summary>
    public class WeeklyTrendsDto
    {
        /// <summary>
        /// Start date of the analyzed week.
        /// </summary>
        public DateTime WeekStart { get; set; }

        /// <summary>
        /// End date of the analyzed week.
        /// </summary>
        public DateTime WeekEnd { get; set; }

        /// <summary>
        /// Ranked list of games that generated the highest engagement during the week.
        /// </summary>
        public IReadOnlyList<WeeklyTrendGameDto> Games { get; set; } = [];
    }
}
