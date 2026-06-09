using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Spectrum.API.Dtos.External;
using Spectrum.API.Models;
using Spectrum.API.Repositories;

namespace Spectrum.Tests.UnitTests.Repositories
{
    public class GameRepositoryTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"spectrum-games-{Guid.NewGuid():N}");

        [Fact]
        public void ConstructorWhenSnapshotIsMissingShouldExposeEmptyCatalog()
        {
            var repository = CreateRepository();

            Assert.Empty(repository.GetAll());
            Assert.Null(repository.GetById(1));
        }

        [Fact]
        public void SearchShouldFilterOrderAndPaginateLoadedSnapshot()
        {
            WriteSnapshot(
                CreateGame(1, "Zeta Quest", new DateTime(2026, 1, 1), 8.1),
                CreateGame(2, "Alpha Quest", new DateTime(2025, 1, 1), 9.5),
                CreateGame(3, "Beta Quest", new DateTime(2024, 1, 1), 7.2)
            );
            var repository = CreateRepository();

            var result = repository.Search(new GameQueryDto
            {
                Search = "quest",
                Ordering = "name",
                Page = 1,
                PageSize = 2
            });

            Assert.Equal(3, result.TotalCount);
            Assert.Equal(["Alpha Quest", "Beta Quest"], result.Items.Select(game => game.Title));
            Assert.Equal("Alpha Quest", repository.GetById(2)!.Title);
            Assert.NotNull(repository.GetByGuid(result.Items.First().Id));
        }

        [Theory]
        [InlineData("-name", "Zeta Quest")]
        [InlineData("released", "Beta Quest")]
        [InlineData("-released", "Zeta Quest")]
        [InlineData("rating", "Beta Quest")]
        [InlineData("-rating", "Alpha Quest")]
        [InlineData("", "Zeta Quest")]
        public void SearchShouldApplyOrderingOptions(string ordering, string expectedFirst)
        {
            WriteSnapshot(
                CreateGame(1, "Zeta Quest", new DateTime(2026, 1, 1), 8.1),
                CreateGame(2, "Alpha Quest", new DateTime(2025, 1, 1), 9.5),
                CreateGame(3, "Beta Quest", new DateTime(2024, 1, 1), 7.2)
            );
            var repository = CreateRepository();

            var result = repository.Search(new GameQueryDto { Ordering = ordering, Page = 1, PageSize = 10 });

            Assert.Equal(expectedFirst, result.Items.First().Title);
        }

        private GameRepository CreateRepository()
        {
            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(env => env.ContentRootPath).Returns(_root);

            return new GameRepository(environment.Object, Mock.Of<ILogger<GameRepository>>());
        }

        private void WriteSnapshot(params Game[] games)
        {
            var dataDirectory = Path.Combine(_root, "Data");
            Directory.CreateDirectory(dataDirectory);
            File.WriteAllText(Path.Combine(dataDirectory, "games_snapshot.json"), JsonSerializer.Serialize(games));
        }

        private static Game CreateGame(int rawgId, string title, DateTime released, double rating)
        {
            return new Game
            {
                Id = Guid.NewGuid(),
                RawgId = rawgId,
                Title = title,
                ReleaseDate = released,
                InternalRating = rating
            };
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
