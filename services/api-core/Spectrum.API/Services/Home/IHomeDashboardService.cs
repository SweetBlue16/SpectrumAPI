using Microsoft.EntityFrameworkCore;
using Spectrum.API.Data;
using Spectrum.API.Dtos.Drops;
using Spectrum.API.Dtos.Home;
using Spectrum.API.Repositories;
using Spectrum.API.Services.Analytics;
using Spectrum.API.Services.Drops;
using Spectrum.API.Services.Votes;

namespace Spectrum.API.Services.Home
{
    public interface IHomeDashboardService
    {
        Task<HomeDashboardDto> GetDashboardAsync(Guid? currentUserId = null, bool isAdmin = false, CancellationToken cancellationToken = default);
    }

    public class HomeDashboardService : IHomeDashboardService
    {
        private readonly SpectrumDbContext _context;
        private readonly IGameRepository _gameRepository;
        private readonly IDropsService _dropsService;
        private readonly ICommentAnalyticsService _commentAnalyticsService;
        private readonly IVoteService _voteService;

        public HomeDashboardService(
            SpectrumDbContext context,
            IGameRepository gameRepository,
            IDropsService dropsService,
            ICommentAnalyticsService commentAnalyticsService,
            IVoteService voteService
        )
        {
            _context = context;
            _gameRepository = gameRepository;
            _dropsService = dropsService;
            _commentAnalyticsService = commentAnalyticsService;
            _voteService = voteService;
        }

        public async Task<HomeDashboardDto> GetDashboardAsync(Guid? currentUserId = null, bool isAdmin = false, CancellationToken cancellationToken = default)
        {
            var today = DateTime.UtcNow.Date;
            var weekEnd = today.AddDays(7);

            var recentGames = _gameRepository.GetAll()
                .Where(game => game.RawgId > 0)
                .OrderByDescending(game => game.ReleaseDate ?? DateTime.MinValue)
                .Take(8)
                .Select(game => new HomeGameDto
                {
                    GameId = game.RawgId,
                    Title = game.Title,
                    CoverImageUrl = game.CoverImageUrl ?? string.Empty,
                    ReleaseDate = game.ReleaseDate
                })
                .ToList();

            var popularReviewsCandidates = await _context.Reviews
                .AsNoTracking()
                .Include(review => review.User)
                .Where(review => review.CreatedAt >= today && review.CreatedAt < today.AddDays(1))
                .OrderByDescending(review => review.LikesCount + review.DislikesCount)
                .ThenByDescending(review => review.CreatedAt)
                .Take(100)
                .ToListAsync(cancellationToken);

            var commentCounts = await _commentAnalyticsService.GetCommentCountsAsync(
                popularReviewsCandidates.Select(review => review.Id),
                today,
                today.AddDays(1),
                cancellationToken
            );

            var popularReviews = popularReviewsCandidates
                .OrderByDescending(review => review.LikesCount + GetCommentCount(commentCounts, review.Id))
                .ThenByDescending(review => review.CreatedAt)
                .Take(5)
                .ToList();
            var currentUserVotes = await _voteService.GetCurrentReviewVotesAsync(
                popularReviews.Select(review => review.Id),
                currentUserId,
                cancellationToken
            );

            var currentDrops = await _dropsService.ListEventsAsync(
                "CURRENT",
                page: 1,
                pageSize: 8,
                includeDrafts: false,
                exposeChallengeCode: false,
                cancellationToken,
                currentUserId
            );

            var upcomingDrops = await _dropsService.ListEventsAsync(
                "UPCOMING",
                page: 1,
                pageSize: 8,
                includeDrafts: false,
                exposeChallengeCode: false,
                cancellationToken,
                currentUserId
            );

            return new HomeDashboardDto
            {
                RecentGames = recentGames,
                PopularReviewsToday = popularReviews
                    .Select(review => MapReview(
                        review,
                        GetCommentCount(commentCounts, review.Id),
                        currentUserId,
                        isAdmin,
                        currentUserVotes.GetValueOrDefault(review.Id)
                    ))
                    .ToList(),
                WeeklyDrops = currentDrops.Items
                    .Concat(upcomingDrops.Items)
                    .Where(drop => drop.StartAt < weekEnd && drop.EndAt >= today)
                    .OrderBy(drop => drop.StartAt)
                    .Take(6)
                    .ToList()
            };
        }

        private static int GetCommentCount(IReadOnlyDictionary<Guid, int> counts, Guid reviewId)
        {
            return counts.TryGetValue(reviewId, out var count) ? count : 0;
        }

        private HomeReviewDto MapReview(Models.Review review, int commentsCount, Guid? currentUserId, bool isAdmin, string? currentUserVote)
        {
            var game = _gameRepository.GetById(review.GameId);
            var profileImageUrl = review.User?.ProfilePicture ?? string.Empty;
            var coverImageUrl = game?.CoverImageUrl ?? string.Empty;
            var isOwnReview = currentUserId.HasValue && currentUserId.Value == review.UserId;
            var resolvedUserVote = isOwnReview ? null : currentUserVote;
            return new HomeReviewDto
            {
                ReviewId = review.Id,
                UserId = review.UserId,
                Username = review.User?.Username ?? string.Empty,
                UserProfileImageUrl = profileImageUrl,
                ProfileImageUrl = profileImageUrl,
                GameId = review.GameId,
                GameTitle = game?.Title ?? $"Game {review.GameId}",
                GameCoverUrl = coverImageUrl,
                CoverImageUrl = coverImageUrl,
                Title = review.Title,
                Content = review.Content,
                Rating = review.Rating,
                LikesCount = review.LikesCount,
                DislikesCount = review.DislikesCount,
                CurrentUserVote = resolvedUserVote,
                UserVote = resolvedUserVote,
                MyVote = resolvedUserVote,
                IsOwnReview = isOwnReview,
                CanVote = !isOwnReview && !isAdmin,
                CommentsCount = commentsCount,
                CreatedAt = review.CreatedAt
            };
        }
    }
}
