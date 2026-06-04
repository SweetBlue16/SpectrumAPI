using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Spectrum.API.Controllers;
using Spectrum.API.Dtos.Analytics;
using Spectrum.API.Dtos.Home;
using Spectrum.API.Dtos.Search;
using Spectrum.API.Services.Analytics;
using Spectrum.API.Services.Home;
using Spectrum.API.Services.Search;

namespace Spectrum.Tests.UnitTests.Controllers
{
    public class HomeSearchAnalyticsControllerTests
    {
        [Fact]
        public async Task TestHomeDashboardShouldReturnBannerRecentGamesReviewsAndDrops()
        {
            var serviceMock = new Mock<IHomeDashboardService>();
            serviceMock
                .Setup(service => service.GetDashboardAsync(It.IsAny<CancellationToken>(), It.IsAny<Guid?>()))
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
                .Setup(service => service.GetTrendsDashboardAsync(It.IsAny<CancellationToken>(), It.IsAny<Guid?>()))
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
    }
}
