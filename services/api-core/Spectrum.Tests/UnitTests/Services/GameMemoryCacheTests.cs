using System.Text.Json;
using Spectrum.API.Dtos.External;
using Spectrum.API.Models;
using Spectrum.API.Services.Cache;

namespace Spectrum.Tests.UnitTests.Services
{
    public class GameMemoryCacheTests : IDisposable
    {
        private readonly string _snapshotPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");

        [Fact]
        public void InitializeWhenFileDoesNotExistShouldKeepCatalogEmpty()
        {
            var cache = new GameMemoryCache();

            cache.Initialize(_snapshotPath);

            Assert.Empty(cache.Search(new GameQueryDto { Page = 1 }).Items);
            Assert.Null(cache.GetByRawgId(42));
        }

        [Fact]
        public void SearchShouldFilterByTextGenresPlatformsAndOrder()
        {
            WriteSnapshot(
                CreateGame(1, "Zelda", rating: 9.1, released: new DateTime(2026, 1, 1), genres: [3], platforms: [4]),
                CreateGame(2, "Apex Legends", rating: 8.5, released: new DateTime(2025, 1, 1), genres: [2], platforms: [1]),
                CreateGame(3, "Apex Racing", rating: 7.0, released: new DateTime(2024, 1, 1), genres: [1, 2], platforms: [1, 4])
            );
            var cache = new GameMemoryCache();

            cache.Initialize(_snapshotPath);
            var result = cache.Search(new GameQueryDto
            {
                Search = "apex",
                Genres = "bad,2",
                Platforms = "1",
                Ordering = "name",
                Page = 1,
                PageSize = 1
            });

            Assert.Equal(2, result.TotalCount);
            Assert.Equal(1, result.Page);
            Assert.Equal(1, result.PageSize);
            Assert.Single(result.Items);
            Assert.Equal("Apex Legends", result.Items.First().Title);
            Assert.Equal("Apex Legends", cache.GetByRawgId(2)!.Title);
        }

        [Theory]
        [InlineData("-name", "Zelda")]
        [InlineData("released", "Apex Racing")]
        [InlineData("-released", "Zelda")]
        [InlineData("rating", "Apex Racing")]
        [InlineData("-rating", "Zelda")]
        [InlineData("", "Zelda")]
        public void SearchShouldApplySupportedOrderingOptions(string ordering, string expectedFirst)
        {
            WriteSnapshot(
                CreateGame(1, "Zelda", rating: 9.1, released: new DateTime(2026, 1, 1), genres: [3], platforms: [4]),
                CreateGame(2, "Apex Legends", rating: 8.5, released: new DateTime(2025, 1, 1), genres: [2], platforms: [1]),
                CreateGame(3, "Apex Racing", rating: 7.0, released: new DateTime(2024, 1, 1), genres: [1, 2], platforms: [1, 4])
            );
            var cache = new GameMemoryCache();

            cache.Initialize(_snapshotPath);
            var result = cache.Search(new GameQueryDto { Ordering = ordering, Page = 1, PageSize = 10 });

            Assert.Equal(expectedFirst, result.Items.First().Title);
        }

        private void WriteSnapshot(params Game[] games)
        {
            File.WriteAllText(_snapshotPath, JsonSerializer.Serialize(games));
        }

        private static Game CreateGame(
            int rawgId,
            string title,
            double rating,
            DateTime released,
            List<int> genres,
            List<int> platforms)
        {
            return new Game
            {
                Id = Guid.NewGuid(),
                RawgId = rawgId,
                Title = title,
                InternalRating = rating,
                ReleaseDate = released,
                GenreIds = genres,
                PlatformIds = platforms
            };
        }

        public void Dispose()
        {
            if (File.Exists(_snapshotPath))
            {
                File.Delete(_snapshotPath);
            }
        }
    }
}
