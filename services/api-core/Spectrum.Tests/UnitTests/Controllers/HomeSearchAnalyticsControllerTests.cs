using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;
using Spectrum.API.Controllers;
using Spectrum.API.Dtos.Analytics;
using Spectrum.API.Dtos.Home;
using Spectrum.API.Dtos.Search;
using Spectrum.API.Services.Analytics;
using Spectrum.API.Services.Home;
using Spectrum.API.Services.Search;
using Spectrum.API.Utilities;

namespace Spectrum.Tests.UnitTests.Controllers
{
    public class HomeSearchAnalyticsControllerTests
    {
        [Fact]
        public async Task TestHomeDashboardShouldReturnBannerRecentGamesReviewsAndDrops()
        {
            var serviceMock = new Mock<IHomeDashboardService>();
            serviceMock
                .Setup(service => service.GetDashboardAsync(It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HomeDashboardDto
                {
                    BannerTitle = "SPECTRUM",
                    RecentGames = [new HomeGameDto { GameId = 1, Title = "Game" }],
                    PopularReviewsToday = [new HomeReviewDto { ReviewId = Guid.NewGuid(), Title = "Review" }]
                });

            var controller = new HomeController(serviceMock.Object);

            var result = await controller.GetDashboard(CancellationToken.None);

            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            var dto = ok.Value.Should().BeOfType<HomeDashboardDto>().Subject;
            dto.BannerTitle.Should().Be("SPECTRUM");
            dto.RecentGames.Should().HaveCount(1);
            dto.PopularReviewsToday.Should().HaveCount(1);
        }

        [Theory]
        [InlineData("nameid")]
        [InlineData("sub")]
        [InlineData("userId")]
        [InlineData("invalid")]
        public async Task HomeDashboardShouldResolveUserClaimsAndAdminRole(string claimType)
        {
            var expectedUserId = Guid.NewGuid();
            Guid? capturedUserId = null;
            bool? capturedIsAdmin = null;
            var serviceMock = new Mock<IHomeDashboardService>();
            serviceMock
                .Setup(service => service.GetDashboardAsync(It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Callback<Guid?, bool, CancellationToken>((userId, isAdmin, _) =>
                {
                    capturedUserId = userId;
                    capturedIsAdmin = isAdmin;
                })
                .ReturnsAsync(new HomeDashboardDto());
            var controller = new HomeController(serviceMock.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = BuildPrincipal(claimType, expectedUserId, includeAdminRole: true)
                    }
                }
            };

            await controller.GetDashboard(CancellationToken.None);

            capturedIsAdmin.Should().BeTrue();
            if (claimType == "invalid")
            {
                capturedUserId.Should().BeNull();
            }
            else
            {
                capturedUserId.Should().Be(expectedUserId);
            }
        }

        [Fact]
        public async Task TestGlobalSearchShouldReturnGamesAndUsers()
        {
            var serviceMock = new Mock<IGlobalSearchService>();
            serviceMock
                .Setup(service => service.SearchAsync("halo", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GlobalSearchResultDto
                {
                    Games = [new GlobalSearchItemDto { Type = "game", Id = "1", Title = "Halo" }],
                    Users = [new GlobalSearchItemDto { Type = "user", Id = Guid.NewGuid().ToString(), Title = "halofan" }]
                });

            var controller = new SearchController(serviceMock.Object);

            var result = await controller.Search("halo", CancellationToken.None);

            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            var dto = ok.Value.Should().BeOfType<GlobalSearchResultDto>().Subject;
            dto.Games.Should().ContainSingle();
            dto.Users.Should().ContainSingle();
        }

        [Fact]
        public async Task TestTrendsDashboardShouldReturnAggregatedSections()
        {
            var serviceMock = new Mock<IAnalyticsService>();
            serviceMock
                .Setup(service => service.GetTrendsDashboardAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TrendsDashboardDto
                {
                    WeeklyInteractions = [new NamedMetricDto { Id = "1", Label = "Game", Count = 3 }],
                    BestOfWeek = [new NamedMetricDto { Id = "1", Label = "Game", Score = 9 }]
                });

            var controller = new TrendsController(serviceMock.Object);

            var result = await controller.GetDashboard(CancellationToken.None);

            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            var dto = ok.Value.Should().BeOfType<TrendsDashboardDto>().Subject;
            dto.WeeklyInteractions.Should().ContainSingle();
            dto.BestOfWeek.Should().ContainSingle();
        }

        [Theory]
        [InlineData("nameid")]
        [InlineData("sub")]
        [InlineData("userId")]
        [InlineData("invalid")]
        public async Task TrendsEndpointsShouldResolveUserClaims(string claimType)
        {
            var expectedUserId = Guid.NewGuid();
            var capturedIds = new List<Guid?>();
            var serviceMock = new Mock<IAnalyticsService>();
            serviceMock
                .Setup(service => service.GetWeeklyTrendsAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .Callback<Guid?, CancellationToken>((userId, _) => capturedIds.Add(userId))
                .ReturnsAsync(new WeeklyTrendsDto());
            serviceMock
                .Setup(service => service.GetTrendsDashboardAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .Callback<Guid?, CancellationToken>((userId, _) => capturedIds.Add(userId))
                .ReturnsAsync(new TrendsDashboardDto());
            var controller = new TrendsController(serviceMock.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = BuildPrincipal(claimType, expectedUserId, includeAdminRole: false)
                    }
                }
            };

            await controller.GetWeekly(CancellationToken.None);
            await controller.GetDashboard(CancellationToken.None);

            capturedIds.Should().HaveCount(2);
            if (claimType == "invalid")
            {
                capturedIds.Should().OnlyContain(id => id == null);
            }
            else
            {
                capturedIds.Should().OnlyContain(id => id == expectedUserId);
            }
        }

        [Fact]
        public async Task TestCryptDashboardShouldReturnWorstAndInactiveGames()
        {
            var serviceMock = new Mock<IAnalyticsService>();
            serviceMock
                .Setup(service => service.GetCryptDashboardAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CryptDashboardDto
                {
                    WorstGames = [new NamedMetricDto { Id = "1", Label = "Bad Game", Score = 5 }],
                    GamesWithoutReviews = [new NamedMetricDto { Id = "2", Label = "Silent Game" }]
                });

            var controller = new CryptController(serviceMock.Object);

            var result = await controller.GetDashboard(CancellationToken.None);

            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            var dto = ok.Value.Should().BeOfType<CryptDashboardDto>().Subject;
            dto.WorstGames.Should().ContainSingle();
            dto.GamesWithoutReviews.Should().ContainSingle();
        }

        private static ClaimsPrincipal BuildPrincipal(string claimType, Guid userId, bool includeAdminRole)
        {
            var claims = new List<Claim>();
            var value = claimType == "invalid" ? "not-a-guid" : userId.ToString();
            claims.Add(claimType switch
            {
                "sub" => new Claim("sub", value),
                "userId" => new Claim("userId", value),
                _ => new Claim(ClaimTypes.NameIdentifier, value)
            });

            if (includeAdminRole)
            {
                claims.Add(new Claim(ClaimTypes.Role, Constants.Roles.Admin));
            }

            return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        }
    }
}
