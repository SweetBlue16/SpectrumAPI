namespace Spectrum.API.Dtos.Search
{
    /// <summary>
    /// Represents a single result returned by the global search engine.
    /// </summary>
    public class GlobalSearchItemDto
    {
        /// <summary>
        /// Type of result returned (e.g. game or user).
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Unique identifier of the matched entity.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Primary display text shown in search results.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Optional secondary information displayed below the title.
        /// </summary>
        public string? Subtitle { get; set; }

        /// <summary>
        /// Optional image associated with the result.
        /// </summary>
        public string? ImageUrl { get; set; }
    }
}