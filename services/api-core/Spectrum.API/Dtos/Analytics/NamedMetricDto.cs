namespace Spectrum.API.Dtos.Analytics
{
    /// <summary>
    /// Visual metric contract used by Trends cards. Domain-specific aliases keep older clients compatible.
    /// </summary>
    public class NamedMetricDto
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
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
