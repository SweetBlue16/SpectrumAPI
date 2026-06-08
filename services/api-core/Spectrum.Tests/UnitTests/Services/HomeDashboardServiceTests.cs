using Moq;
using Spectrum.API.Dtos.Drops;
using Spectrum.API.Models;
using Spectrum.API.Repositories;
using Spectrum.API.Services.Analytics;
using Spectrum.API.Services.Drops;
using Spectrum.API.Services.Home;
using Spectrum.API.Services.Votes;
using Spectrum.API.Utilities;
using Spectrum.Tests.Helpers;

namespace Spectrum.Tests.UnitTests.Services
{
    public class HomeDashboardServiceTests
    {
        private static readonly int[] ExpectedGameOrder = { 20, 10 };

        [Fact]
        public async Task GetDashboardAsyncShouldComposeRecentGamesReviewsVotesAndWeeklyDrops()
        {
            using var context = TestDbContextFactory.CreateContext();
            var currentUserId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var today = DateTime.UtcNow.Date;
            var ownReview = CreateReview(currentUserId, gameId: 10, likes: 1, createdAt: today.AddHours(9));
            var commentedReview = CreateReview(otherUserId, gameId: 20, likes: 2, createdAt: today.AddHours(10));
            await context.Users.AddRangeAsync(ownReview.User!, commentedReview.User!);
            await context.Reviews.AddRangeAsync(ownReview, commentedReview);
            await context.SaveChangesAsync();

            var games = new[]
            {
                new Game { Id = Guid.NewGuid(), RawgId = 10, Title = "Older", ReleaseDate = today.AddDays(-5), CoverImageUrl = "older.jpg" },
                new Game { Id = Guid.NewGuid(), RawgId = 20, Title = "Newer", ReleaseDate = today.AddDays(-1), CoverImageUrl = "newer.jpg" }
            };
            var gameRepository = new Mock<IGameRepository>();
            gameRepository.Setup(repository => repository.GetAll()).Returns(games);
            gameRepository.Setup(repository => repository.GetById(10)).Returns(games[0]);
            gameRepository.Setup(repository => repository.GetById(20)).Returns(games[1]);

            var dropsService = new Mock<IDropsService>();
            var visibleDrop = CreateDrop("drop-visible", today.AddDays(1), today.AddDays(2));
            var oldDrop = CreateDrop("drop-old", today.AddDays(-10), today.AddDays(-9));
            dropsService
                .Setup(service => service.ListEventsAsync("CURRENT", 1, 8, false, false, It.IsAny<CancellationToken>(), currentUserId))
                .ReturnsAsync(new PagedResult<EventStatusDto> { Items = new[] { oldDrop }, TotalCount = 1, Page = 1, PageSize = 8 });
            dropsService
                .Setup(service => service.ListEventsAsync("UPCOMING", 1, 8, false, false, It.IsAny<CancellationToken>(), currentUserId))
                .ReturnsAsync(new PagedResult<EventStatusDto> { Items = new[] { visibleDrop }, TotalCount = 1, Page = 1, PageSize = 8 });

            var analytics = new Mock<ICommentAnalyticsService>();
            analytics
                .Setup(service => service.GetCommentCountsAsync(
                    It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, int>
                {
                    [ownReview.Id] = 0,
                    [commentedReview.Id] = 5
                });
            var voteService = new Mock<IVoteService>();
            voteService
                .Setup(service => service.GetCurrentReviewVotesAsync(
                    It.IsAny<IEnumerable<Guid>>(),
                    currentUserId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string>
                {
                    [commentedReview.Id] = "like",
                    [ownReview.Id] = "dislike"
                });

            var service = new HomeDashboardService(
                context,
                gameRepository.Object,
                dropsService.Object,
                analytics.Object,
                voteService.Object
            );

            var dashboard = await service.GetDashboardAsync(CancellationToken.None, currentUserId);


            Assert.Equal(ExpectedGameOrder, dashboard.RecentGames.Select(game => game.GameId));
            Assert.Equal(commentedReview.Id, dashboard.PopularReviewsToday[0].ReviewId);
            Assert.Equal(5, dashboard.PopularReviewsToday[0].CommentsCount);
            Assert.Equal("like", dashboard.PopularReviewsToday[0].CurrentUserVote);
            Assert.True(dashboard.PopularReviewsToday.Single(review => review.ReviewId == ownReview.Id).IsOwnReview);
            Assert.False(dashboard.PopularReviewsToday.Single(review => review.ReviewId == ownReview.Id).CanVote);
            Assert.Null(dashboard.PopularReviewsToday.Single(review => review.ReviewId == ownReview.Id).CurrentUserVote);
            Assert.Single(dashboard.WeeklyDrops);
            Assert.Equal("drop-visible", dashboard.WeeklyDrops[0].EventId);
        }

        [Fact]
        public async Task GetDashboardAsyncWhenRequesterIsAdminShouldDisableVoting()
        {
            using var context = TestDbContextFactory.CreateContext();
            var adminId = Guid.NewGuid();
            var review = CreateReview(Guid.NewGuid(), gameId: 77, likes: 4, createdAt: DateTime.UtcNow.Date.AddHours(11));
            await context.Users.AddAsync(review.User!);
            await context.Reviews.AddAsync(review);
            await context.SaveChangesAsync();

            var gameRepository = new Mock<IGameRepository>();
            gameRepository.Setup(repository => repository.GetAll()).Returns(Array.Empty<Game>());
            gameRepository.Setup(repository => repository.GetById(77)).Returns(new Game { RawgId = 77, Title = "Admin Game" });

            var dropsService = new Mock<IDropsService>();
            dropsService
                .Setup(service => service.ListEventsAsync(It.IsAny<string>(), 1, 8, false, false, It.IsAny<CancellationToken>(), adminId))
                .ReturnsAsync(new PagedResult<EventStatusDto> { Items = Array.Empty<EventStatusDto>(), Page = 1, PageSize = 8 });

            var analytics = new Mock<ICommentAnalyticsService>();
            analytics
                .Setup(service => service.GetCommentCountsAsync(
                    It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, int>());
            var voteService = new Mock<IVoteService>();
            voteService
                .Setup(service => service.GetCurrentReviewVotesAsync(
                    It.IsAny<IEnumerable<Guid>>(),
                    adminId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string> { [review.Id] = "like" });

            var service = new HomeDashboardService(
                context,
                gameRepository.Object,
                dropsService.Object,
                analytics.Object,
                voteService.Object
            );

            var dashboard = await service.GetDashboardAsync(CancellationToken.None, adminId, isAdmin: true);

            var card = Assert.Single(dashboard.PopularReviewsToday);
            Assert.False(card.IsOwnReview);
            Assert.False(card.CanVote);
            Assert.Equal("like", card.CurrentUserVote);
        }

        private static Review CreateReview(Guid userId, int gameId, int likes, DateTime createdAt)
        {
            return new Review
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GameId = gameId,
                Rating = 8,
                Title = "Review",
                Content = "Solid game",
                LikesCount = likes,
                DislikesCount = 0,
                CreatedAt = createdAt,
                User = new User
                {
                    Id = userId,
                    Username = $"user-{gameId}",
                    Email = $"user-{gameId}@spectrum.test",
                    ProfilePicture = $"profile-{gameId}.png"
                }
            };
        }

        private static EventStatusDto CreateDrop(string eventId, DateTime startAt, DateTime endAt)
        {
            return new EventStatusDto
            {
                EventId = eventId,
                Title = eventId,
                GameTitle = "Halo",
                Platform = "PC",
                StartAt = startAt,
                JoinDeadlineAt = startAt.AddHours(1),
                RevealAt = startAt.AddHours(2),
                EndAt = endAt,
                TotalSlots = 10,
                RewardCodesTotal = 1,
                RewardCodesAvailable = 1,
                Status = "UPCOMING"
            };
        }
    }
}
