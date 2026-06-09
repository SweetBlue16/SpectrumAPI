using Spectrum.API.Dtos.External;
using Spectrum.API.Utilities;

namespace Spectrum.Tests.UnitTests.Utilities
{
    public class GameMappingUtilitiesTests
    {
        [Fact]
        public void MapToInternalModelWhenOptionalCollectionsExistShouldMapAllValues()
        {
            var dto = new RawgGameDto
            {
                Id = 42,
                Name = "Mapped Game",
                Released = "2026-06-09",
                BackgroundImage = "cover.jpg",
                Genres = [new RawgGenreDto { Id = 1 }, new RawgGenreDto { Id = 2 }],
                ParentPlatforms =
                [
                    new RawgPlatformWrapperDto { Platform = new RawgPlatformDto { Id = 4 } },
                    new RawgPlatformWrapperDto { Platform = new RawgPlatformDto { Id = 5 } }
                ]
            };

            var result = GameMappingUtilities.MapToInternalModel(dto);

            Assert.Equal(42, result.RawgId);
            Assert.Equal("Mapped Game", result.Title);
            Assert.Equal(new DateTime(2026, 6, 9), result.ReleaseDate);
            Assert.Equal([1, 2], result.GenreIds);
            Assert.Equal([4, 5], result.PlatformIds);
            Assert.Equal(GameMappingUtilities.GenerateDeterministicGuid(42), result.Id);
        }

        [Fact]
        public void MapToInternalModelWhenOptionalValuesAreMissingShouldUseNullAndEmptyCollections()
        {
            var dto = new RawgGameDto
            {
                Id = 7,
                Name = "Minimal Game",
                Released = string.Empty,
                Genres = null,
                ParentPlatforms = null
            };

            var result = GameMappingUtilities.MapToInternalModel(dto);

            Assert.Null(result.ReleaseDate);
            Assert.Empty(result.GenreIds);
            Assert.Empty(result.PlatformIds);
        }
    }
}
