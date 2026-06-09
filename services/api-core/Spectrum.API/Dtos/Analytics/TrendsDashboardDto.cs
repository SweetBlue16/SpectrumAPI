namespace Spectrum.API.Dtos.Analytics
{
    /// <summary>
    /// Represents the analytics dashboard displayed in the Trends section.
    /// </summary>
    public class TrendsDashboardDto
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
        /// Start date of the analyzed month.
        /// </summary>
        public DateTime MonthStart { get; set; }

        /// <summary>
        /// End date of the analyzed month.
        /// </summary>
        public DateTime MonthEnd { get; set; }

        /// <summary>
        /// Most active community interactions during the week.
        /// </summary>
        public IReadOnlyList<NamedMetricDto> WeeklyInteractions { get; set; } = [];

        /// <summary>
        /// Featured discussions and reviews from the week.
        /// </summary>
        public IReadOnlyList<WeeklyReviewDto> WeeklyDiscussions { get; set; } = [];

        /// <summary>
        /// Lowest-rated games of the week.
        /// </summary>
        public IReadOnlyList<NamedMetricDto> WorstOfWeek { get; set; } = [];

        /// <summary>
        /// Highest-rated games of the week.
        /// </summary>
        public IReadOnlyList<NamedMetricDto> BestOfWeek { get; set; } = [];

        /// <summary>
        /// Most popular gaming platforms during the month.
        /// </summary>
        public IReadOnlyList<NamedMetricDto> ConsoleOfMonth { get; set; } = [];

        /// <summary>
        /// Top contributors during the month.
        /// </summary>
        public IReadOnlyList<NamedMetricDto> TopReviewersOfMonth { get; set; } = [];

        /// <summary>
        /// Most popular genres during the month.
        /// </summary>
        public IReadOnlyList<NamedMetricDto> GenresOfMonth { get; set; } = [];
    }
}
