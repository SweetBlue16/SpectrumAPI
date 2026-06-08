using Microsoft.EntityFrameworkCore;
using Spectrum.API.Data;
using Spectrum.API.Dtos.Analytics;
using Spectrum.API.Repositories;
using Spectrum.API.Services.Votes;
using Spectrum.API.Utilities;

namespace Spectrum.API.Services.Analytics
{
    public interface IAnalyticsService
    {
        Task<GlobalMetricsDto> GetGlobalMetricsAsync(string period, DateTime? anchorDate, CancellationToken cancellationToken = default);
        Task<WeeklyTrendsDto> GetWeeklyTrendsAsync(Guid? currentUserId = null, CancellationToken cancellationToken = default);
        Task<TrendsDashboardDto> GetTrendsDashboardAsync(Guid? currentUserId = null, CancellationToken cancellationToken = default);
        Task<CryptDashboardDto> GetCryptDashboardAsync(CancellationToken cancellationToken = default);
        Task<PagedResult<WeeklyReviewDto>> GetWeeklyClipsAsync(int page, int pageSize, Guid? currentUserId = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<WeeklyReviewDto>> GetMonthlyTopClipsAsync(Guid? currentUserId = null, CancellationToken cancellationToken = default);
    }

    public class AnalyticsService : IAnalyticsService
    {
        private const int TopGamesLimit = 3;
        private const int ReviewsPerTrendGame = 3;
        private const string PeriodMonth = "month";

        private readonly SpectrumDbContext _context;
        private readonly IGameRepository _gameRepository;
        private readonly ICommentAnalyticsService _commentAnalyticsService;
        private readonly IVoteService _voteService;

        public AnalyticsService(
            SpectrumDbContext context,
            IGameRepository gameRepository,
            ICommentAnalyticsService commentAnalyticsService,
            IVoteService voteService
        )
        {
            _context = context;
            _gameRepository = gameRepository;
            _commentAnalyticsService = commentAnalyticsService;
            _voteService = voteService;
        }

        public async Task<GlobalMetricsDto> GetGlobalMetricsAsync(
            string period,
            DateTime? anchorDate,
            CancellationToken cancellationToken = default
        )
        {
            var window = ResolveWindow(period, anchorDate ?? DateTime.UtcNow);
            var users = await _context.Users
                .AsNoTracking()
                .Where(user => user.CreatedAt >= window.Start && user.CreatedAt < window.End)
                .Select(user => user.CreatedAt)
                .ToListAsync(cancellationToken);

            var reviews = await _context.Reviews
                .AsNoTracking()
                .Where(review => review.CreatedAt >= window.Start && review.CreatedAt < window.End)
                .Select(review => new { review.CreatedAt, review.GameId })
                .ToListAsync(cancellationToken);

            var topGames = reviews
                .GroupBy(review => review.GameId)
                .Select(group => MapTopGame(group.Key, group.Count()))
                .OrderByDescending(game => game.Count)
                .ThenBy(game => game.GameTitle)
                .Take(5)
                .ToList();

            return new GlobalMetricsDto
            {
                WindowStart = window.Start,
                WindowEnd = window.End,
                NewUsers = BuildSeries(users, period),
                NewReviews = BuildSeries(reviews.Select(review => review.CreatedAt), period),
                MostSearchedGames = topGames
            };
        }

        public async Task<WeeklyTrendsDto> GetWeeklyTrendsAsync(Guid? currentUserId = null, CancellationToken cancellationToken = default)
        {
            var window = ResolveWeeklyWindow(DateTime.UtcNow);
            var topGames = await _context.Reviews
                .AsNoTracking()
                .Where(review => review.CreatedAt >= window.Start && review.CreatedAt < window.End)
                .GroupBy(review => review.GameId)
                .Select(group => new { GameId = group.Key, ReviewsCount = group.Count() })
                .OrderByDescending(group => group.ReviewsCount)
                .ThenBy(group => group.GameId)
                .Take(TopGamesLimit)
                .ToListAsync(cancellationToken);

            if (topGames.Count == 0)
            {
                return new WeeklyTrendsDto
                {
                    WeekStart = window.Start,
                    WeekEnd = window.End,
                    Games = []
                };
            }

            var gameIds = topGames.Select(game => game.GameId).ToArray();
            var reviews = await _context.Reviews
                .AsNoTracking()
                .Include(review => review.User)
                .Where(review => gameIds.Contains(review.GameId) &&
                                 review.CreatedAt >= window.Start &&
                                 review.CreatedAt < window.End)
                .OrderByDescending(review => review.LikesCount)
                .ThenByDescending(review => review.CreatedAt)
                .ToListAsync(cancellationToken);
            var userVotes = await ResolveReviewVotesAsync(reviews.Select(review => review.Id), currentUserId, cancellationToken);

            var rank = 1;
            var games = topGames
                .Select(game =>
                {
                    var metadata = _gameRepository.GetById(game.GameId);
                    return new WeeklyTrendGameDto
                    {
                        Rank = rank++,
                        GameId = game.GameId,
                        GameTitle = metadata?.Title ?? $"Game {game.GameId}",
                        CoverImageUrl = metadata?.CoverImageUrl ?? string.Empty,
                        ReviewsCount = game.ReviewsCount,
                        Reviews = reviews
                            .Where(review => review.GameId == game.GameId)
                            .Take(ReviewsPerTrendGame)
                            .Select(review => MapWeeklyReview(
                                review,
                                metadata?.Title,
                                metadata?.CoverImageUrl,
                                currentUserId: currentUserId,
                                currentUserVote: userVotes.GetValueOrDefault(review.Id)
                            ))
                            .ToList()
                    };
                })
                .ToList();

            return new WeeklyTrendsDto
            {
                WeekStart = window.Start,
                WeekEnd = window.End,
                Games = games
            };
        }

        public async Task<TrendsDashboardDto> GetTrendsDashboardAsync(Guid? currentUserId = null, CancellationToken cancellationToken = default)
        {
            var week = ResolveWeeklyWindow(DateTime.UtcNow);
            var month = ResolveWindow(PeriodMonth, DateTime.UtcNow);

            var weeklyReviews = await _context.Reviews
                .AsNoTracking()
                .Include(review => review.User)
                .Where(review => review.CreatedAt >= week.Start && review.CreatedAt < week.End)
                .ToListAsync(cancellationToken);

            var monthlyReviews = await _context.Reviews
                .AsNoTracking()
                .Include(review => review.User)
                .Where(review => review.CreatedAt >= month.Start && review.CreatedAt < month.End)
                .ToListAsync(cancellationToken);

            var weeklyCommentCounts = await _commentAnalyticsService.GetCommentCountsAsync(
                weeklyReviews.Select(review => review.Id),
                week.Start,
                week.End,
                cancellationToken
            );
            var weeklyUserVotes = await ResolveReviewVotesAsync(
                weeklyReviews.Select(review => review.Id),
                currentUserId,
                cancellationToken
            );

            var monthlyPlatformPreferences = await _context.Users
                .AsNoTracking()
                .Where(user => user.CreatedAt >= month.Start && user.CreatedAt < month.End)
                .SelectMany(user => user.Platforms)
                .GroupBy(platform => new { platform.Id, platform.Name })
                .Select(group => new NamedMetricDto
                {
                    Id = group.Key.Id.ToString(),
                    Label = group.Key.Name,
                    Count = group.Count(),
                    PlatformName = group.Key.Name,
                    ConsoleName = group.Key.Name,
                    IconUrl = null,
                    PlatformIconUrl = null
                })
                .OrderByDescending(metric => metric.Count)
                .Take(5)
                .ToListAsync(cancellationToken);

            return new TrendsDashboardDto
            {
                WeekStart = week.Start,
                WeekEnd = week.End,
                MonthStart = month.Start,
                MonthEnd = month.End,
                WeeklyInteractions = BuildGameInteractionMetrics(weeklyReviews, weeklyCommentCounts, TopGamesLimit),
                WeeklyDiscussions = weeklyReviews
                    .OrderByDescending(review => GetCommentCount(weeklyCommentCounts, review.Id))
                    .ThenByDescending(review => review.LikesCount)
                    .ThenByDescending(review => review.CreatedAt)
                    .Take(3)
                    .Select(review =>
                    {
                        var game = _gameRepository.GetById(review.GameId);
                        return MapWeeklyReview(
                            review,
                            game?.Title,
                            game?.CoverImageUrl,
                            GetCommentCount(weeklyCommentCounts, review.Id),
                            currentUserId,
                            weeklyUserVotes.GetValueOrDefault(review.Id)
                        );
                    })
                    .ToList(),
                WorstOfWeek = BuildRatingMetrics(weeklyReviews, takeWorst: true, limit: 3),
                BestOfWeek = BuildRatingMetrics(weeklyReviews, takeWorst: false, limit: 3),
                ConsoleOfMonth = monthlyPlatformPreferences,
                TopReviewersOfMonth = monthlyReviews
                    .GroupBy(review => new
                    {
                        review.UserId,
                        Username = review.User?.Username ?? "Usuario Spectrum",
                        ProfilePicture = review.User?.ProfilePicture ?? string.Empty
                    })
                    .Select(group => new NamedMetricDto
                    {
                        Id = group.Key.UserId.ToString(),
                        Label = group.Key.Username,
                        Count = group.Count(),
                        Score = group.Sum(review => review.LikesCount),
                        UserId = group.Key.UserId,
                        Username = group.Key.Username,
                        ProfileImageUrl = string.IsNullOrWhiteSpace(group.Key.ProfilePicture) ? null : group.Key.ProfilePicture,
                        UserProfileImageUrl = string.IsNullOrWhiteSpace(group.Key.ProfilePicture) ? null : group.Key.ProfilePicture
                    })
                    .OrderByDescending(metric => metric.Score)
                    .ThenBy(metric => metric.Label)
                    .Take(5)
                    .ToList(),
                GenresOfMonth = BuildGenreMetrics(monthlyReviews)
            };
        }

        public async Task<CryptDashboardDto> GetCryptDashboardAsync(CancellationToken cancellationToken = default)
        {
            var month = ResolveWindow(PeriodMonth, DateTime.UtcNow);
            var monthlyReviews = await _context.Reviews
                .AsNoTracking()
                .Where(review => review.CreatedAt >= month.Start && review.CreatedAt < month.End)
                .ToListAsync(cancellationToken);

            var reviewedGameIds = monthlyReviews
                .Select(review => review.GameId)
                .Distinct()
                .ToHashSet();

            return new CryptDashboardDto
            {
                MonthStart = month.Start,
                MonthEnd = month.End,
                WorstGames = BuildRatingMetrics(monthlyReviews, takeWorst: true, limit: 5),
                GamesWithoutReviews = _gameRepository.GetAll()
                    .Where(game => game.RawgId > 0 && !reviewedGameIds.Contains(game.RawgId))
                    .OrderByDescending(game => game.ReleaseDate ?? DateTime.MinValue)
                    .Take(5)
                    .Select(game => new NamedMetricDto
                    {
                        Id = game.RawgId.ToString(),
                        Label = game.Title,
                        ImageUrl = game.CoverImageUrl,
                        GameId = game.RawgId,
                        GameTitle = game.Title,
                        CoverImageUrl = game.CoverImageUrl,
                        Count = 0,
                        Score = 0
                    })
                    .ToList()
            };
        }

        public async Task<PagedResult<WeeklyReviewDto>> GetWeeklyClipsAsync(
            int page,
            int pageSize,
            Guid? currentUserId = null,
            CancellationToken cancellationToken = default
        )
        {
            var normalizedPage = Math.Max(1, page);
            var normalizedPageSize = Math.Clamp(pageSize, 1, 25);
            var window = ResolveWeeklyWindow(DateTime.UtcNow);

            var reviewQuery = _context.Reviews
                .AsNoTracking()
                .Include(review => review.User)
                .Where(review => review.CreatedAt >= window.Start &&
                                 review.CreatedAt < window.End &&
                                 review.MediaType != null &&
                                 review.MediaType.ToLower() == "video" &&
                                 review.ImageUrl != null &&
                                 review.ImageUrl != string.Empty);

            var reviewClips = await reviewQuery
                .OrderByDescending(review => review.LikesCount)
                .ThenByDescending(review => review.CreatedAt)
                .ToListAsync(cancellationToken);

            var uploadedClips = await _context.GameClips
                .AsNoTracking()
                .Include(clip => clip.User)
                .Include(clip => clip.Game)
                .Where(clip => clip.CreatedAt >= window.Start && clip.CreatedAt < window.End)
                .OrderByDescending(clip => clip.CreatedAt)
                .ToListAsync(cancellationToken);
            var uploadedClipCounts = await GetClipVoteCountsAsync(uploadedClips.Select(clip => clip.Id), cancellationToken);
            var uploadedClipUserVotes = await GetClipUserVotesAsync(uploadedClips.Select(clip => clip.Id), currentUserId, cancellationToken);
            var reviewUserVotes = await ResolveReviewVotesAsync(reviewClips.Select(review => review.Id), currentUserId, cancellationToken);

            var allClips = reviewClips
                .Select(review =>
                {
                    var game = _gameRepository.GetById(review.GameId);
                    return MapWeeklyReview(
                        review,
                        game?.Title,
                        game?.CoverImageUrl,
                        currentUserId: currentUserId,
                        currentUserVote: reviewUserVotes.GetValueOrDefault(review.Id)
                    );
                })
                .Concat(uploadedClips.Select(clip => MapWeeklyClip(clip, uploadedClipCounts, uploadedClipUserVotes, currentUserId)))
                .OrderByDescending(clip => clip.LikesCount)
                .ThenByDescending(clip => clip.CreatedAt)
                .ToList();

            return new PagedResult<WeeklyReviewDto>
            {
                Items = allClips
                    .Skip((normalizedPage - 1) * normalizedPageSize)
                    .Take(normalizedPageSize)
                    .ToList(),
                TotalCount = allClips.Count,
                Page = normalizedPage,
                PageSize = normalizedPageSize
            };
        }

        public async Task<IReadOnlyList<WeeklyReviewDto>> GetMonthlyTopClipsAsync(Guid? currentUserId = null, CancellationToken cancellationToken = default)
        {
            var window = ResolveWindow(PeriodMonth, DateTime.UtcNow);
            var reviews = await _context.Reviews
                .AsNoTracking()
                .Include(review => review.User)
                .Where(review => review.CreatedAt >= window.Start &&
                                 review.CreatedAt < window.End &&
                                 review.MediaType != null &&
                                 review.MediaType.ToLower() == "video" &&
                                 review.ImageUrl != null &&
                                 review.ImageUrl != string.Empty)
                .OrderByDescending(review => review.LikesCount)
                .ThenByDescending(review => review.CreatedAt)
                .ToListAsync(cancellationToken);

            var uploadedClips = await _context.GameClips
                .AsNoTracking()
                .Include(clip => clip.User)
                .Include(clip => clip.Game)
                .Where(clip => clip.CreatedAt >= window.Start && clip.CreatedAt < window.End)
                .ToListAsync(cancellationToken);
            var uploadedClipCounts = await GetClipVoteCountsAsync(uploadedClips.Select(clip => clip.Id), cancellationToken);
            var uploadedClipUserVotes = await GetClipUserVotesAsync(uploadedClips.Select(clip => clip.Id), currentUserId, cancellationToken);
            var reviewUserVotes = await ResolveReviewVotesAsync(reviews.Select(review => review.Id), currentUserId, cancellationToken);

            return reviews.Select(review =>
                {
                    var game = _gameRepository.GetById(review.GameId);
                    return MapWeeklyReview(
                        review,
                        game?.Title,
                        game?.CoverImageUrl,
                        currentUserId: currentUserId,
                        currentUserVote: reviewUserVotes.GetValueOrDefault(review.Id)
                    );
                })
                .Concat(uploadedClips.Select(clip => MapWeeklyClip(clip, uploadedClipCounts, uploadedClipUserVotes, currentUserId)))
                .OrderByDescending(clip => clip.LikesCount)
                .ThenByDescending(clip => clip.CreatedAt)
                .Take(3)
                .ToList();
        }

        private TopGameMetricDto MapTopGame(int gameId, int count)
        {
            var game = _gameRepository.GetById(gameId);
            return new TopGameMetricDto
            {
                GameId = gameId,
                GameTitle = game?.Title ?? $"Game {gameId}",
                CoverImageUrl = game?.CoverImageUrl ?? string.Empty,
                Count = count
            };
        }

        private static int GetCommentCount(IReadOnlyDictionary<Guid, int> counts, Guid reviewId)
        {
            return counts.TryGetValue(reviewId, out var count) ? count : 0;
        }

        private IReadOnlyList<NamedMetricDto> BuildGameInteractionMetrics(
            IEnumerable<Models.Review> reviews,
            IReadOnlyDictionary<Guid, int> commentCounts,
            int limit
        )
        {
            return reviews
                .GroupBy(review => review.GameId)
                .Select(group =>
                {
                    var game = _gameRepository.GetById(group.Key);
                    var interactions = group.Count() + group.Sum(review => review.LikesCount + GetCommentCount(commentCounts, review.Id));
                    var coverImageUrl = game?.CoverImageUrl;
                    return new NamedMetricDto
                    {
                        Id = group.Key.ToString(),
                        Label = game?.Title ?? $"Game {group.Key}",
                        Count = interactions,
                        Score = interactions,
                        ImageUrl = coverImageUrl,
                        GameId = group.Key,
                        GameTitle = game?.Title ?? $"Game {group.Key}",
                        CoverImageUrl = coverImageUrl
                    };
                })
                .OrderByDescending(metric => metric.Count)
                .ThenBy(metric => metric.Label)
                .Take(limit)
                .ToList();
        }

        private IReadOnlyList<NamedMetricDto> BuildRatingMetrics(IEnumerable<Models.Review> reviews, bool takeWorst, int limit)
        {
            var query = reviews
                .GroupBy(review => review.GameId)
                .Select(group =>
                {
                    var game = _gameRepository.GetById(group.Key);
                    var average = group.Average(review => review.Rating);
                    var coverImageUrl = game?.CoverImageUrl;
                    return new NamedMetricDto
                    {
                        Id = group.Key.ToString(),
                        Label = game?.Title ?? $"Game {group.Key}",
                        Count = group.Count(),
                        Score = Math.Round(average, 2),
                        ImageUrl = coverImageUrl,
                        GameId = group.Key,
                        GameTitle = game?.Title ?? $"Game {group.Key}",
                        CoverImageUrl = coverImageUrl
                    };
                });

            return (takeWorst
                    ? query.OrderBy(metric => metric.Score).ThenBy(metric => metric.Label)
                    : query.OrderByDescending(metric => metric.Score).ThenBy(metric => metric.Label))
                .Take(limit)
                .ToList();
        }

        private IReadOnlyList<NamedMetricDto> BuildGenreMetrics(IEnumerable<Models.Review> reviews)
        {
            return reviews
                .SelectMany(review =>
                {
                    var game = _gameRepository.GetById(review.GameId);
                    return (game?.GenreIds ?? Enumerable.Empty<int>())
                        .Select(genreId => new { GenreId = genreId, Interactions = 1 + review.LikesCount + review.DislikesCount });
                })
                .GroupBy(item => item.GenreId)
                .Select(group => new NamedMetricDto
                {
                    Id = group.Key.ToString(),
                    Label = ResolveGenreName(group.Key),
                    Count = group.Sum(item => item.Interactions),
                    Score = group.Sum(item => item.Interactions)
                })
                .OrderByDescending(metric => metric.Count)
                .Take(5)
                .ToList();
        }

        private static string ResolveGenreName(int genreId)
        {
            return genreId switch
            {
                1 => "Racing",
                2 => "Shooter",
                3 => "Adventure",
                4 => "Action",
                5 => "RPG",
                6 => "Fighting",
                7 => "Puzzle",
                10 => "Strategy",
                11 => "Arcade",
                14 => "Simulation",
                15 => "Sports",
                19 => "Family",
                28 => "Board Games",
                34 => "Educational",
                40 => "Casual",
                51 => "Indie",
                59 => "Massively Multiplayer",
                83 => "Platformer",
                _ => "Genre unavailable"
            };
        }

        private static WeeklyReviewDto MapWeeklyReview(
            Models.Review review,
            string? gameTitle,
            string? gameCoverUrl,
            int commentsCount = 0,
            Guid? currentUserId = null,
            string? currentUserVote = null
        )
        {
            var profileImageUrl = review.User?.ProfilePicture ?? string.Empty;
            var resolvedGameTitle = gameTitle ?? $"Game {review.GameId}";
            var resolvedGameCoverUrl = gameCoverUrl ?? string.Empty;
            var isOwnContent = currentUserId.HasValue && review.UserId == currentUserId.Value;
            var resolvedUserVote = isOwnContent ? null : currentUserVote;

            return new WeeklyReviewDto
            {
                ReviewId = review.Id,
                UserId = review.UserId,
                Username = review.User?.Username ?? string.Empty,
                UserProfileImageUrl = profileImageUrl,
                ProfileImageUrl = profileImageUrl,
                GameId = review.GameId,
                GameTitle = resolvedGameTitle,
                GameCoverUrl = resolvedGameCoverUrl,
                CoverImageUrl = resolvedGameCoverUrl,
                ReviewTitle = review.Title,
                ReviewContent = review.Content,
                ReviewDate = review.CreatedAt,
                Rating = review.Rating,
                Score = review.Rating,
                Title = review.Title,
                Content = review.Content,
                AttachmentUrl = review.ImageUrl ?? string.Empty,
                AttachmentType = review.MediaType ?? string.Empty,
                LikesCount = review.LikesCount,
                DislikesCount = review.DislikesCount,
                CommentsCount = commentsCount,
                SourceType = "REVIEW",
                UserVote = resolvedUserVote,
                CurrentUserVote = resolvedUserVote,
                MyVote = resolvedUserVote,
                IsOwnContent = isOwnContent,
                CreatedAt = review.CreatedAt
            };
        }

        private static WeeklyReviewDto MapWeeklyClip(
            Models.GameClip clip,
            IReadOnlyDictionary<Guid, (int Likes, int Dislikes)> counts,
            IReadOnlyDictionary<Guid, string> userVotes,
            Guid? currentUserId
        )
        {
            var count = counts.TryGetValue(clip.Id, out var resolvedCount) ? resolvedCount : (Likes: 0, Dislikes: 0);
            var profileImageUrl = clip.User?.ProfilePicture ?? string.Empty;
            var coverImageUrl = clip.Game?.CoverImageUrl ?? string.Empty;
            var userVote = userVotes.TryGetValue(clip.Id, out var vote) ? vote : null;
            return new WeeklyReviewDto
            {
                ReviewId = clip.Id,
                UserId = clip.UserId,
                Username = clip.User?.Username ?? string.Empty,
                UserProfileImageUrl = profileImageUrl,
                ProfileImageUrl = profileImageUrl,
                GameId = 0,
                GameTitle = clip.Game?.Title ?? string.Empty,
                GameCoverUrl = coverImageUrl,
                CoverImageUrl = coverImageUrl,
                ReviewTitle = clip.Title,
                ReviewContent = clip.Description ?? string.Empty,
                ReviewDate = clip.CreatedAt,
                Rating = 0,
                Score = 0,
                Title = clip.Title,
                Content = clip.Description ?? string.Empty,
                AttachmentUrl = clip.Url,
                AttachmentType = "video",
                LikesCount = count.Likes,
                DislikesCount = count.Dislikes,
                CommentsCount = 0,
                SourceType = "GAME_CLIP",
                UserVote = userVote,
                CurrentUserVote = userVote,
                MyVote = userVote,
                IsOwnContent = currentUserId.HasValue && clip.UserId == currentUserId.Value,
                CreatedAt = clip.CreatedAt
            };
        }

        private async Task<IReadOnlyDictionary<Guid, string>> ResolveReviewVotesAsync(
            IEnumerable<Guid> reviewIds,
            Guid? currentUserId,
            CancellationToken cancellationToken
        )
        {
            return await _voteService.GetCurrentReviewVotesAsync(reviewIds, currentUserId, cancellationToken);
        }

        private async Task<IReadOnlyDictionary<Guid, (int Likes, int Dislikes)>> GetClipVoteCountsAsync(
            IEnumerable<Guid> clipIds,
            CancellationToken cancellationToken
        )
        {
            var ids = clipIds.Distinct().ToArray();
            if (ids.Length == 0)
            {
                return new Dictionary<Guid, (int Likes, int Dislikes)>();
            }

            return await _context.GameClipVotes
                .AsNoTracking()
                .Where(vote => ids.Contains(vote.ClipId))
                .GroupBy(vote => vote.ClipId)
                .Select(group => new
                {
                    ClipId = group.Key,
                    Likes = group.Count(vote => vote.IsPositive),
                    Dislikes = group.Count(vote => !vote.IsPositive)
                })
                .ToDictionaryAsync(item => item.ClipId, item => (item.Likes, item.Dislikes), cancellationToken);
        }

        private async Task<IReadOnlyDictionary<Guid, string>> GetClipUserVotesAsync(
            IEnumerable<Guid> clipIds,
            Guid? userId,
            CancellationToken cancellationToken
        )
        {
            var ids = clipIds.Distinct().ToArray();
            if (!userId.HasValue || ids.Length == 0)
            {
                return new Dictionary<Guid, string>();
            }

            return await _context.GameClipVotes
                .AsNoTracking()
                .Where(vote => vote.UserId == userId.Value && ids.Contains(vote.ClipId))
                .ToDictionaryAsync(
                    vote => vote.ClipId,
                    vote => vote.IsPositive ? "like" : "dislike",
                    cancellationToken
                );
        }

        private static IReadOnlyList<MetricPointDto> BuildSeries(IEnumerable<DateTime> values, string period)
        {
            return values
                .GroupBy(value => Bucket(value, period))
                .OrderBy(group => group.Key)
                .Select(group => new MetricPointDto
                {
                    Date = group.Key,
                    Label = BuildLabel(group.Key, period),
                    Count = group.Count()
                })
                .ToList();
        }

        private static DateTime Bucket(DateTime value, string period)
        {
            var utcValue = value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
                : value.ToUniversalTime();

            return period.Equals("day", StringComparison.OrdinalIgnoreCase)
                ? new DateTime(utcValue.Year, utcValue.Month, utcValue.Day, utcValue.Hour, 0, 0, DateTimeKind.Utc)
                : utcValue.Date;
        }

        private static string BuildLabel(DateTime value, string period)
        {
            return period.Equals("day", StringComparison.OrdinalIgnoreCase)
                ? value.ToString("HH:00")
                : value.ToString("yyyy-MM-dd");
        }

        private static (DateTime Start, DateTime End) ResolveWindow(string period, DateTime anchorDate)
        {
            var anchorUtc = anchorDate.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(anchorDate, DateTimeKind.Utc)
                : anchorDate.ToUniversalTime();

            return period.ToLowerInvariant() switch
            {
                "day" => (anchorUtc.Date, anchorUtc.Date.AddDays(1)),
                "month" => (new DateTime(anchorUtc.Year, anchorUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(anchorUtc.Year, anchorUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1)),
                _ => ResolveWeeklyWindow(anchorUtc)
            };
        }

        private static (DateTime Start, DateTime End) ResolveWeeklyWindow(DateTime anchorDate)
        {
            var anchorUtc = anchorDate.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(anchorDate, DateTimeKind.Utc)
                : anchorDate.ToUniversalTime();
            var daysFromMonday = ((int)anchorUtc.DayOfWeek + 6) % 7;
            var start = anchorUtc.Date.AddDays(-daysFromMonday);

            return (start, start.AddDays(7));
        }
    }
}
