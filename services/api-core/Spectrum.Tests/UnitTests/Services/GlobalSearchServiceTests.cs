using Microsoft.EntityFrameworkCore;
using Moq;
using Spectrum.API.Data;
using Spectrum.API.Dtos.External;
using Spectrum.API.Models;
using Spectrum.API.Repositories;
using Spectrum.API.Services.Search;
using Spectrum.API.Utilities;

namespace Spectrum.Tests.UnitTests.Services
{
    public class GlobalSearchServiceTests : IDisposable
    {
        private readonly SpectrumDbContext _context;
        private readonly Mock<IGameRepository> _gameRepositoryMock;
        private readonly GlobalSearchService _globalSearchService;

        public GlobalSearchServiceTests()
        {
            var options = new DbContextOptionsBuilder<SpectrumDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new SpectrumDbContext(options);
            _gameRepositoryMock = new Mock<IGameRepository>();

            _globalSearchService = new GlobalSearchService(_context, _gameRepositoryMock.Object);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("a")]
        [InlineData(" ")]
        public async Task TestSearchAsyncWhenQueryIsTooShortShouldReturnEmptyResult(string invalidQuery)
        {
            var result = await _globalSearchService.SearchAsync(invalidQuery);

            Assert.NotNull(result);
            Assert.Empty(result.Games);
            Assert.Empty(result.Users);

            _gameRepositoryMock.Verify(repo => repo.Search(It.IsAny<GameQueryDto>()), Times.Never);
        }

        [Fact]
        public async Task TestSearchAsyncWhenQueryIsValidShouldReturnMatchingGamesAndUsers()
        {
            var query = "spectrum";

            var matchingUser = new User { Id = Guid.NewGuid(), Username = "SpectrumGamer", Email = "gamer@test.com" };
            var nonMatchingUser = new User { Id = Guid.NewGuid(), Username = "PlayerOne", Email = "player@test.com" };

            await _context.Users.AddRangeAsync(matchingUser, nonMatchingUser);
            await _context.SaveChangesAsync();

            var mockGameResult = (
                Items: (IEnumerable<Game>)new List<Game>
                {
                    new Game
                    {
                        RawgId = 100,
                        Title = "Spectrum Protocol",
                        CoverImageUrl = "http://image.url",
                        ReleaseDate = new DateTime(2026, 1, 1)
                    }
                },
                TotalCount: 1
            );

            _gameRepositoryMock.Setup(repo => repo.Search(It.Is<GameQueryDto>(q => q.Search == query)))
                               .Returns(mockGameResult);

            var result = await _globalSearchService.SearchAsync(query);

            Assert.NotNull(result);

            Assert.Single(result.Users);
            var userItem = result.Users.First();
            Assert.Equal("user", userItem.Type);
            Assert.Equal("SpectrumGamer", userItem.Title);
            Assert.Equal("Perfil de jugador", userItem.Subtitle);

            Assert.Single(result.Games);
            var gameItem = result.Games.First();
            Assert.Equal("game", gameItem.Type);
            Assert.Equal("Spectrum Protocol", gameItem.Title);
            Assert.Equal("2026", gameItem.Subtitle);
        }

        [Fact]
        public async Task TestSearchAsyncShouldLimitResultsAndOrderByUsername()
        {
            var query = "user";

            var users = Enumerable.Range(1, 10).Select(i => new User
            {
                Id = Guid.NewGuid(),
                Username = $"user{i:D2}",
                Email = $"test{i}@domain.com"
            }).ToList();

            users.Reverse();

            await _context.Users.AddRangeAsync(users);
            await _context.SaveChangesAsync();

            var emptyGameResult = (
                Items: Enumerable.Empty<Game>(),
                TotalCount: 0
            );
            _gameRepositoryMock.Setup(repo => repo.Search(It.IsAny<GameQueryDto>()))
                               .Returns(emptyGameResult);

            var result = await _globalSearchService.SearchAsync(query);

            Assert.NotNull(result);

            Assert.Equal(5, result.Users.Count);

            Assert.Equal("user01", result.Users.First().Title);
            Assert.Equal("user05", result.Users.Last().Title);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}
