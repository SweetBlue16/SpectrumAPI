namespace Spectrum.API.Dtos.External
{
    /// <summary>
    /// Wrapper for the platform object in the RAWG nested JSON structure.
    /// </summary>
    public class RawgPlatformWrapperDto
    {
        public RawgPlatformDto Platform { get; set; } = new();
    }

    /// <summary>
    /// Basic identification data for a gaming platform.
    /// </summary>
    public class RawgPlatformDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Basic metadata for a game genre.
    /// </summary>
    public class RawgGenreDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
