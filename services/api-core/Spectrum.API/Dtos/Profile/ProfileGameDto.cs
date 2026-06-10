namespace Spectrum.API.Dtos.Profile
{
    /// <summary>
    /// DTO that represents a game in the user's profile. 
    /// </summary>
    public class ProfileGameDto
    {
        /// <summary>
        /// Game identifier.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the game.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Cover image URL.
        /// </summary>
        public string? ImageUrl { get; set; }
    }
}