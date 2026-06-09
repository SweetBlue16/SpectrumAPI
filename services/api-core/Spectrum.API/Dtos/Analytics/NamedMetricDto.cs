namespace Spectrum.API.Dtos.Analytics
{
    /// <summary>
    /// Visual metric contract used by analytics dashboards and ranking cards.
    /// Domain-specific aliases are maintained for backwards compatibility.
    /// </summary>
    public class NamedMetricDto
    {
        /// <summary>
        /// Internal metric identifier.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Display label shown to users.
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Total occurrences associated with the metric.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Calculated score used for ranking.
        /// </summary>
        public double Score { get; set; }

        public string? ImageUrl { get; set; }

        public int? GameId { get; set; }

        public string? GameTitle { get; set; }

        public string? CoverImageUrl { get; set; }

        public Guid? UserId { get; set; }

        public string? Username { get; set; }

        public string? ProfileImageUrl { get; set; }

        public string? UserProfileImageUrl { get; set; }

        public string? PlatformName { get; set; }

        public string? ConsoleName { get; set; }

        public string? IconUrl { get; set; }

        public string? PlatformIconUrl { get; set; }
    }
}
