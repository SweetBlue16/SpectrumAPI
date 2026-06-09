namespace Spectrum.API.Dtos.Home
{
    /// <summary>
    /// Represents a game displayed in the home dashboard.
    /// </summary>
    public class HomeGameDto
    {
        /// <summary>
        /// RAWG identifier associated with the game.
        /// </summary>
        public int GameId { get; set; }

        /// <summary>
        /// Display title of the game.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// URL of the game's cover image.
        /// </summary>
        public string CoverImageUrl { get; set; } = string.Empty;

        /// <summary>
        /// Official release date of the game.
        /// </summary>
        public DateTime? ReleaseDate { get; set; }
    }
}
