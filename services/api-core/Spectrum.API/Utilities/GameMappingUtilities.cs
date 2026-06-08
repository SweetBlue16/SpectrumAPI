namespace Spectrum.API.Utilities
{
    using Spectrum.API.Dtos.External;
    using Spectrum.API.Models;
    using System.Globalization;

    /// <summary>
    /// Provides helper methods to transform external data structures into internal models.
    /// </summary>
    public static class GameMappingUtilities
    {
        /// <summary>
        /// Maps a RawgGameDto object to a Game model instance.
        /// </summary>
        /// <param name="externalGame">The data received from the RAWG API.</param>
        /// <returns>A new instance of the Game model with mapped properties.</returns>
        public static Game MapToInternalModel(RawgGameDto externalGame)
        {
            return new Game
            {
                Id = GenerateDeterministicGuid(externalGame.Id),
                RawgId = externalGame.Id,
                Title = externalGame.Name,
                ReleaseDate = string.IsNullOrEmpty(externalGame.Released)
                    ? null
                    : DateTime.Parse(externalGame.Released, CultureInfo.InvariantCulture),
                CoverImageUrl = externalGame.BackgroundImage,
                InternalRating = 0.0,
                GenreIds = externalGame.Genres?.Select(g => g.Id).ToList() ?? new List<int>(),
                PlatformIds = externalGame.ParentPlatforms?.Select(p => p.Platform.Id).ToList() ?? new List<int>()
            };
        }

        /// <summary>
        /// Generates a stable, deterministic GUID derived from a RAWG game ID using a SHA-256 hash
        /// seeded with a fixed namespace. This ensures the same game always maps to the same GUID
        /// across sync cycles, preserving referential integrity in related tables.
        /// </summary>
        /// <param name="rawgId">The unique numeric identifier from the RAWG API.</param>
        /// <returns>A stable <see cref="Guid"/> that is consistent across application restarts and re-syncs.</returns>
        public static Guid GenerateDeterministicGuid(int rawgId)
        {
            var namespaceBytes = "spectrum-games-namespace"u8.ToArray();
            var idBytes = BitConverter.GetBytes(rawgId);
            var input = namespaceBytes.Concat(idBytes).ToArray();

            var hash = System.Security.Cryptography.SHA256.HashData(input);
            var guidBytes = hash[..16];
            return new Guid(guidBytes);
        }
    }
}