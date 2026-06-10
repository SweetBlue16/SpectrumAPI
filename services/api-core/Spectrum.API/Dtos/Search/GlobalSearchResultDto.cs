namespace Spectrum.API.Dtos.Search
{
    /// <summary>
    /// Aggregated results returned by the global search endpoint.
    /// </summary>
    public class GlobalSearchResultDto
    {
        /// <summary>
        /// Matching videogame results.
        /// </summary>
        public IReadOnlyList<GlobalSearchItemDto> Games { get; set; } = [];

        /// <summary>
        /// Matching user profile results.
        /// </summary>
        public IReadOnlyList<GlobalSearchItemDto> Users { get; set; } = [];
    }
}