using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;
using Spectrum.API.Controllers;
using Spectrum.API.Dtos.External;
using Spectrum.API.Dtos.Reviews;
using Spectrum.API.Models;
using Spectrum.API.Services.External;
using Spectrum.API.Utilities;

namespace Spectrum.Tests.UnitTests.Controllers
{
    public class GamesControllerTests
    {
        private readonly Mock<IGameService> _gameServiceMock;
        private readonly GamesController _gamesController;

        public GamesControllerTests()
        {
            _gameServiceMock = new Mock<IGameService>();
            _gamesController = new GamesController(_gameServiceMock.Object);
        }

        [Fact]
        public async Task TestSearchWhenValidQueryShouldReturnOkWithGamesCollection()
        {
            var queryDto = new GameQueryDto { Search = "Halo", PageSize = 10 };
            var expectedGames = new List<Game>
            {
                new Game { Id = Guid.NewGuid(), RawgId = 1, Title = "Halo: Combat Evolved" },
                new Game { Id = Guid.NewGuid(), RawgId = 2, Title = "Halo 2" }
            };
            var expectedResult = new PagedResult<Game>
            {
                Items = expectedGames,
                TotalCount = expectedGames.Count,
                Page = 1,
                PageSize = 10
            };

            _gameServiceMock.Setup(s => s.SearchGamesAsync(queryDto)).ReturnsAsync(expectedResult);

            var result = await _gamesController.Search(queryDto);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

            var returnedResult = Assert.IsType<PagedResult<Game>>(okResult.Value);
            Assert.Equal(2, returnedResult.Items.Count());

            _gameServiceMock.Verify(s => s.SearchGamesAsync(queryDto), Times.Once);
        }

        [Fact]
        public async Task TestSearchWhenNoResultsShouldReturnOkWithEmptyCollection()
        {
            var queryDto = new GameQueryDto { Search = "NonExistentGame" };
            var expectedResult = new PagedResult<Game>
            {
                Items = Enumerable.Empty<Game>(),
                TotalCount = 0,
                Page = 1,
                PageSize = 20
            };

            _gameServiceMock.Setup(s => s.SearchGamesAsync(queryDto)).ReturnsAsync(expectedResult);

            var result = await _gamesController.Search(queryDto);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

            var returnedResult = Assert.IsType<PagedResult<Game>>(okResult.Value);
            Assert.Empty(returnedResult.Items);

            _gameServiceMock.Verify(s => s.SearchGamesAsync(queryDto), Times.Once);
        }

        [Fact]
        public async Task TestGetDetailsWhenGameExistsShouldReturnOkWithGameDetails()
        {
            int gameId = 3498;
            var expectedGame = new Game { Id = Guid.NewGuid(), RawgId = gameId, Title = "Grand Theft Auto V", InternalRating = 4.48 };

            _gameServiceMock.Setup(s => s.GetGameDetailsAsync(gameId)).ReturnsAsync(expectedGame);

            var result = await _gamesController.GetDetails(gameId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

            var returnedGame = Assert.IsType<Game>(okResult.Value);
            Assert.Equal(expectedGame.RawgId, returnedGame.RawgId);
            Assert.Equal(expectedGame.Title, returnedGame.Title);

            _gameServiceMock.Verify(s => s.GetGameDetailsAsync(gameId), Times.Once);
        }

        [Theory]
        [InlineData("anonymous", false)]
        [InlineData("nameid", false)]
        [InlineData("sub", false)]
        [InlineData("userId", true)]
        [InlineData("invalid", false)]
        public async Task GetReviewDetailShouldResolveCurrentUserAndAdminRole(string claimType, bool isAdmin)
        {
            var userId = Guid.NewGuid();
            Guid? capturedUserId = Guid.Empty;
            bool? capturedIsAdmin = null;
            _gameServiceMock
                .Setup(service => service.GetGameReviewDetailAsync(
                    42,
                    It.IsAny<Guid?>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Callback<int, Guid?, bool, CancellationToken>((_, currentUserId, admin, _) =>
                {
                    capturedUserId = currentUserId;
                    capturedIsAdmin = admin;
                })
                .ReturnsAsync(new GameReviewDetailDto());
            var controller = new GamesController(_gameServiceMock.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = BuildPrincipal(claimType, userId, isAdmin)
                    }
                }
            };

            var result = await controller.GetReviewDetail(42, CancellationToken.None);

            Assert.IsType<OkObjectResult>(result);
            Assert.Equal(isAdmin, capturedIsAdmin);
            if (claimType is "anonymous" or "invalid")
            {
                Assert.Null(capturedUserId);
            }
            else
            {
                Assert.Equal(userId, capturedUserId);
            }
        }

        private static ClaimsPrincipal BuildPrincipal(string claimType, Guid userId, bool isAdmin)
        {
            if (claimType == "anonymous")
            {
                return new ClaimsPrincipal(new ClaimsIdentity());
            }

            var claims = new List<Claim>();
            var value = claimType == "invalid" ? "not-a-guid" : userId.ToString();
            claims.Add(claimType switch
            {
                "sub" => new Claim("sub", value),
                "userId" => new Claim("userId", value),
                _ => new Claim(ClaimTypes.NameIdentifier, value)
            });

            if (isAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, Constants.Roles.Admin));
            }

            return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        }
    }
}
