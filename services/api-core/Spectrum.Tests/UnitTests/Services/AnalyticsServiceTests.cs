using Moq;
using Spectrum.API.Dtos.Analytics;
using Spectrum.API.Models;
using Spectrum.API.Repositories;
using Spectrum.API.Services.Analytics;
using Spectrum.API.Services.Votes;
using Spectrum.Tests.Helpers;

namespace Spectrum.Tests.UnitTests.Services
{
    public class AnalyticsServiceTests
    {
        private readonly Mock<IGameRepository> _gameRepositoryMock = new();
        private readonly Mock<ICommentAnalyticsService> _commentAnalyticsServiceMock = new();
        private readonly Mock<IVoteService> _voteServiceMock = new();

        [Fact]
        public async Task GetGlobalMetricsAsyncShouldBuildWindowSeriesAndTopGames()
        {
            await using var context = TestDbContextFactory.CreateContext();
            var anchor = new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc);
            await context.Users.AddRangeAsync(
                CreateUser("alice", createdAt: new DateTime(2026, 6, 9, 8, 30, 0, DateTimeKind.Utc)),
                CreateUser("old", createdAt: anchor.AddDays(-2))
            );
            await context.Reviews.AddRangeAsync(
                CreateReview(gameId: 10, createdAt: new DateTime(2026, 6, 9, 9, 15, 0, DateTimeKind.Utc)),
                CreateReview(gameId: 10, createdAt: new DateTime(2026, 6, 9, 10, 15, 0, DateTimeKind.Utc)),
                CreateReview(gameId: 20, createdAt: new DateTime(2026, 6, 9, 11, 15, 0, DateTimeKind.Utc)),
                CreateReview(gameId: 30, createdAt: anchor.AddDays(-1))
            );
            await context.SaveChangesAsync();
            SetupGame(10, "Halo", "halo.jpg", genres: [2]);
            SetupGame(20, "Apex", "apex.jpg", genres: [1]);
            var service = CreateService(context);

            var result = await service.GetGlobalMetricsAsync("day", anchor);

            Assert.Equal(anchor.Date, result.WindowStart);
            Assert.Equal(anchor.Date.AddDays(1), result.WindowEnd);
            Assert.Single(result.NewUsers);
            Assert.Equal("08:00", result.NewUsers[0].Label);
            Assert.Equal(3, result.NewReviews.Sum(point => point.Count));
            Assert.Equal(10, result.MostSearchedGames[0].GameId);
            Assert.Equal("Halo", result.MostSearchedGames[0].GameTitle);
        }

        [Fact]
        public async Task GetGlobalMetricsAsyncWhenPeriodIsMonthShouldBucketByDay()
        {
            await using var context = TestDbContextFactory.CreateContext();
            var anchor = new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc);
            await context.Users.AddRangeAsync(
                CreateUser("first", createdAt: new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc)),
                CreateUser("second", createdAt: new DateTime(2026, 6, 8, 8, 0, 0, DateTimeKind.Utc)),
                CreateUser("next-month", createdAt: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc))
            );
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var result = await service.GetGlobalMetricsAsync("month", anchor);

            Assert.Equal(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), result.WindowStart);
            Assert.Equal(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), result.WindowEnd);
            Assert.Equal(["2026-06-01", "2026-06-08"], result.NewUsers.Select(point => point.Label));
        }

        [Fact]
        public async Task GetWeeklyTrendsAsyncWhenThereAreNoReviewsShouldReturnEmptyGames()
        {
            await using var context = TestDbContextFactory.CreateContext();
            var service = CreateService(context);

            var result = await service.GetWeeklyTrendsAsync(Guid.NewGuid());

            Assert.Empty(result.Games);
            _voteServiceMock.Verify(
                service => service.GetCurrentReviewVotesAsync(
                    It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task GetWeeklyTrendsAsyncShouldRankGamesAndHideOwnVote()
        {
            await using var context = TestDbContextFactory.CreateContext();
            var currentUserId = Guid.NewGuid();
            var otherUser = CreateUser("other", createdAt: DateTime.UtcNow.AddDays(-10));
            var ownUser = CreateUser("owner", currentUserId, DateTime.UtcNow.AddDays(-10));
            var firstReview = CreateReview(gameId: 10, user: otherUser, createdAt: CurrentWeek().AddDays(1), likes: 5);
            var ownReview = CreateReview(gameId: 10, user: ownUser, createdAt: CurrentWeek().AddDays(2), likes: 4);
            await context.Reviews.AddRangeAsync(firstReview, ownReview, CreateReview(gameId: 20, createdAt: CurrentWeek().AddDays(1), likes: 1));
            await context.SaveChangesAsync();
            SetupGame(10, "Halo", "halo.jpg", genres: [2]);
            SetupGame(20, "Apex", "apex.jpg", genres: [1]);
            _voteServiceMock
                .Setup(service => service.GetCurrentReviewVotesAsync(
                    It.IsAny<IEnumerable<Guid>>(),
                    currentUserId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string>
                {
                    [firstReview.Id] = "like",
                    [ownReview.Id] = "dislike"
                });
            var service = CreateService(context);

            var result = await service.GetWeeklyTrendsAsync(currentUserId);

            Assert.Equal(2, result.Games.Count);
            Assert.Equal(1, result.Games[0].Rank);
            Assert.Equal("Halo", result.Games[0].GameTitle);
            Assert.Equal("like", result.Games[0].Reviews.Single(review => review.ReviewId == firstReview.Id).CurrentUserVote);
            Assert.Null(result.Games[0].Reviews.Single(review => review.ReviewId == ownReview.Id).CurrentUserVote);
            Assert.True(result.Games[0].Reviews.Single(review => review.ReviewId == ownReview.Id).IsOwnContent);
        }

        [Fact]
        public async Task GetTrendsDashboardAsyncShouldAggregateInteractionsRatingsReviewersAndGenres()
        {
            await using var context = TestDbContextFactory.CreateContext();
            var platform = new Platform { Id = 1, Name = "PC" };
            var reviewer = CreateUser("reviewer", createdAt: CurrentMonth().AddDays(1));
            reviewer.ProfilePicture = "avatar.png";
            reviewer.Platforms.Add(platform);
            var firstReview = CreateReview(gameId: 10, user: reviewer, createdAt: CurrentWeek().AddDays(1), rating: 9, likes: 5);
            var secondReview = CreateReview(gameId: 20, createdAt: CurrentWeek().AddDays(2), rating: 5, likes: 1);
            await context.Users.AddAsync(reviewer);
            await context.Reviews.AddRangeAsync(firstReview, secondReview);
            await context.SaveChangesAsync();
            SetupGame(10, "Shooter", "shooter.jpg", genres: [2]);
            SetupGame(20, "Racing", "racing.jpg", genres: [1]);
            _commentAnalyticsServiceMock
                .Setup(service => service.GetCommentCountsAsync(
                    It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, int> { [firstReview.Id] = 3 });
            _voteServiceMock
                .Setup(service => service.GetCurrentReviewVotesAsync(
                    It.IsAny<IEnumerable<Guid>>(),
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string>());
            var service = CreateService(context);

            var result = await service.GetTrendsDashboardAsync();

            Assert.Equal("Shooter", result.WeeklyInteractions[0].GameTitle);
            Assert.Equal(9, result.WeeklyInteractions[0].Count);
            Assert.Equal("Shooter", result.BestOfWeek[0].GameTitle);
            Assert.Equal("Racing", result.WorstOfWeek[0].GameTitle);
            Assert.Equal("PC", result.ConsoleOfMonth[0].Label);
            Assert.Equal("reviewer", result.TopReviewersOfMonth[0].Username);
            Assert.Contains(result.GenresOfMonth, genre => genre.Label == "Shooter");
        }

        [Fact]
        public async Task GetTrendsDashboardAsyncShouldResolveAllKnownGenreNamesAndFallback()
        {
            await using var context = TestDbContextFactory.CreateContext();
            var genreIds = new[] { 1, 2, 3, 4, 5, 6, 7, 10, 11, 14, 15, 19, 28, 34, 40, 51, 59, 83, 999 };
            for (var index = 0; index < genreIds.Length; index++)
            {
                var gameId = 1000 + index;
                await context.Reviews.AddAsync(CreateReview(
                    gameId: gameId,
                    createdAt: CurrentMonth().AddDays(1),
                    likes: genreIds.Length - index));
                SetupGame(gameId, $"Game {genreIds[index]}", $"{genreIds[index]}.jpg", [genreIds[index]]);
            }

            await context.SaveChangesAsync();
            _commentAnalyticsServiceMock
                .Setup(service => service.GetCommentCountsAsync(
                    It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, int>());
            _voteServiceMock
                .Setup(service => service.GetCurrentReviewVotesAsync(
                    It.IsAny<IEnumerable<Guid>>(),
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string>());
            var service = CreateService(context);

            var result = await service.GetTrendsDashboardAsync();

            Assert.Contains(result.GenresOfMonth, genre => genre.Label == "Racing");
            Assert.Contains(result.GenresOfMonth, genre => genre.Label == "Shooter");
            Assert.Contains(result.GenresOfMonth, genre => genre.Label == "Adventure");
            Assert.Contains(result.GenresOfMonth, genre => genre.Label == "Action");
            Assert.Contains(result.GenresOfMonth, genre => genre.Label == "RPG");
        }

        [Fact]
        public async Task GetCryptDashboardAsyncShouldReturnWorstGamesAndUnreviewedCatalogItems()
        {
            await using var context = TestDbContextFactory.CreateContext();
            await context.Reviews.AddRangeAsync(
                CreateReview(gameId: 10, createdAt: CurrentMonth().AddDays(1), rating: 5),
                CreateReview(gameId: 20, createdAt: CurrentMonth().AddDays(2), rating: 9)
            );
            await context.SaveChangesAsync();
            SetupGame(10, "Reviewed Low", "low.jpg", genres: [1]);
            SetupGame(20, "Reviewed High", "high.jpg", genres: [2]);
            _gameRepositoryMock
                .Setup(repository => repository.GetAll())
                .Returns(new[]
                {
                    CreateGame(10, "Reviewed Low", "low.jpg", [1]),
                    CreateGame(99, "Fresh Game", "fresh.jpg", [3], releaseDate: new DateTime(2026, 1, 1))
                });
            var service = CreateService(context);

            var result = await service.GetCryptDashboardAsync();

            Assert.Equal("Reviewed Low", result.WorstGames[0].GameTitle);
            Assert.Equal("Fresh Game", result.GamesWithoutReviews[0].GameTitle);
        }

        [Fact]
        public async Task GetWeeklyClipsAsyncShouldMergeReviewVideosAndUploadedClipsWithVotes()
        {
            await using var context = TestDbContextFactory.CreateContext();
            var currentUserId = Guid.NewGuid();
            var clipId = Guid.NewGuid();
            var review = CreateReview(gameId: 10, createdAt: CurrentWeek().AddDays(1), likes: 7);
            review.MediaType = "video";
            review.ImageUrl = "https://cdn.test/review.mp4";
            var clip = new GameClip
            {
                Id = clipId,
                Title = "Uploaded clip",
                Description = null,
                Url = "https://cdn.test/clip.mp4",
                UserId = currentUserId,
                User = CreateUser("clipper", currentUserId, DateTime.UtcNow.AddDays(-3)),
                GameId = Guid.NewGuid(),
                Game = CreateGame(99, "Clip Game", "clip.jpg", [2]),
                CreatedAt = CurrentWeek().AddDays(2)
            };
            await context.Reviews.AddAsync(review);
            await context.GameClips.AddAsync(clip);
            await context.GameClipVotes.AddRangeAsync(
                new GameClipVote { Id = Guid.NewGuid(), ClipId = clipId, UserId = Guid.NewGuid(), IsPositive = true },
                new GameClipVote { Id = Guid.NewGuid(), ClipId = clipId, UserId = Guid.NewGuid(), IsPositive = false },
                new GameClipVote { Id = Guid.NewGuid(), ClipId = clipId, UserId = currentUserId, IsPositive = true }
            );
            await context.SaveChangesAsync();
            SetupGame(10, "Review Game", "review.jpg", genres: [1]);
            _voteServiceMock
                .Setup(service => service.GetCurrentReviewVotesAsync(
                    It.IsAny<IEnumerable<Guid>>(),
                    currentUserId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string> { [review.Id] = "dislike" });
            var service = CreateService(context);

            var result = await service.GetWeeklyClipsAsync(page: 0, pageSize: 99, currentUserId);

            Assert.Equal(2, result.TotalCount);
            Assert.Equal(1, result.Page);
            Assert.Equal(25, result.PageSize);
            Assert.Contains(result.Items, item => item.SourceType == "REVIEW" && item.CurrentUserVote == "dislike");
            var uploaded = result.Items.Single(item => item.SourceType == "GAME_CLIP");
            Assert.Equal(2, uploaded.LikesCount);
            Assert.Equal(1, uploaded.DislikesCount);
            Assert.Equal("like", uploaded.CurrentUserVote);
            Assert.True(uploaded.IsOwnContent);
        }

        [Fact]
        public async Task GetMonthlyTopClipsAsyncShouldReturnTopThreeAcrossReviewAndUploadedClips()
        {
            await using var context = TestDbContextFactory.CreateContext();
            var currentUserId = Guid.NewGuid();
            var month = CurrentMonth();
            var review = CreateReview(gameId: 10, createdAt: month.AddDays(1), likes: 4);
            review.MediaType = "video";
            review.ImageUrl = "https://cdn.test/review.mp4";
            var clip = new GameClip
            {
                Id = Guid.NewGuid(),
                Title = "Monthly clip",
                Description = "Clip description",
                Url = "https://cdn.test/monthly.mp4",
                UserId = Guid.NewGuid(),
                User = CreateUser("clipper", createdAt: month.AddDays(1)),
                GameId = Guid.NewGuid(),
                Game = CreateGame(20, "Clip Game", "clip.jpg", [2]),
                CreatedAt = month.AddDays(2)
            };
            await context.Reviews.AddAsync(review);
            await context.GameClips.AddAsync(clip);
            await context.GameClipVotes.AddRangeAsync(
                new GameClipVote { Id = Guid.NewGuid(), ClipId = clip.Id, UserId = currentUserId, IsPositive = true },
                new GameClipVote { Id = Guid.NewGuid(), ClipId = clip.Id, UserId = Guid.NewGuid(), IsPositive = true }
            );
            await context.SaveChangesAsync();
            SetupGame(10, "Review Game", "review.jpg", genres: [1]);
            _voteServiceMock
                .Setup(service => service.GetCurrentReviewVotesAsync(
                    It.IsAny<IEnumerable<Guid>>(),
                    currentUserId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string> { [review.Id] = "like" });
            var service = CreateService(context);

            var result = await service.GetMonthlyTopClipsAsync(currentUserId);

            Assert.Equal(2, result.Count);
            Assert.Equal("REVIEW", result[0].SourceType);
            Assert.Equal("GAME_CLIP", result[1].SourceType);
            Assert.Equal("like", result[1].CurrentUserVote);
        }

        private AnalyticsService CreateService(Spectrum.API.Data.SpectrumDbContext context)
        {
            return new AnalyticsService(
                context,
                _gameRepositoryMock.Object,
                _commentAnalyticsServiceMock.Object,
                _voteServiceMock.Object);
        }

        private void SetupGame(int rawgId, string title, string cover, List<int> genres)
        {
            _gameRepositoryMock
                .Setup(repository => repository.GetById(rawgId))
                .Returns(CreateGame(rawgId, title, cover, genres));
        }

        private static User CreateUser(string username, DateTime createdAt)
        {
            return CreateUser(username, Guid.NewGuid(), createdAt);
        }

        private static User CreateUser(string username, Guid id, DateTime createdAt)
        {
            return new User
            {
                Id = id,
                Username = username,
                Email = $"{username}@spectrum.test",
                PasswordHash = "hash",
                CreatedAt = createdAt
            };
        }

        private static Review CreateReview(
            int gameId,
            DateTime createdAt,
            int rating = 8,
            int likes = 0,
            User? user = null)
        {
            var owner = user ?? CreateUser($"user-{Guid.NewGuid():N}", createdAt.AddDays(-1));
            return new Review
            {
                Id = Guid.NewGuid(),
                UserId = owner.Id,
                User = owner,
                GameId = gameId,
                Rating = rating,
                Title = $"Review {gameId}",
                Content = $"Content {gameId}",
                CreatedAt = createdAt,
                LikesCount = likes,
                DislikesCount = 1
            };
        }

        private static Game CreateGame(
            int rawgId,
            string title,
            string cover,
            List<int> genres,
            DateTime? releaseDate = null)
        {
            return new Game
            {
                Id = Guid.NewGuid(),
                RawgId = rawgId,
                Title = title,
                CoverImageUrl = cover,
                GenreIds = genres,
                ReleaseDate = releaseDate ?? DateTime.UtcNow
            };
        }

        private static DateTime CurrentWeek()
        {
            var now = DateTime.UtcNow;
            var daysFromMonday = ((int)now.DayOfWeek + 6) % 7;
            return now.Date.AddDays(-daysFromMonday);
        }

        private static DateTime CurrentMonth()
        {
            var now = DateTime.UtcNow;
            return new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        }
    }
}
