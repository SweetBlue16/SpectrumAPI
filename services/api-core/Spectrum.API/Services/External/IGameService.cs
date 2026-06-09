using Spectrum.API.Dtos.External;
using Spectrum.API.Exceptions;
using Spectrum.API.Dtos.Reviews;
using Spectrum.API.Models;
using Spectrum.API.Services.Cache;
using Spectrum.API.Repositories;
using Spectrum.API.Utilities;

namespace Spectrum.API.Services.External
{
    /// <summary>
    /// Defines the contract for interacting with the video games catalog.
    /// Now powered by an internal memory cache for high-performance access.
    /// </summary>
    public interface IGameService
    {
        /// <summary>
        /// Searches for video games within the local memory cache based on specified filters.
        /// </summary>
        /// <param name="queryDto">The data transfer object containing search terms and pagination parameters.</param>
        /// <returns>A paged result of internal game entities matching the criteria.</returns>
        Task<PagedResult<Game>> SearchGamesAsync(GameQueryDto queryDto);

        /// <summary>
        /// Retrieves detailed information for a specific video game from the local catalog.
        /// </summary>
        /// <param name="externalGameId">The unique numeric identifier assigned by RAWG.</param>
        /// <returns>The detailed metadata profile of the requested game.</returns>
        Task<Game> GetGameDetailsAsync(int externalGameId);

        /// <summary>
        /// Retrieves a game together with all associated reviews and moderation metadata.
        /// </summary>
        /// <param name="externalGameId">
        /// The RAWG identifier of the game whose review information should be loaded.
        /// </param>
        /// <param name="currentUserId">
        /// Optional identifier of the authenticated user used to determine ownership permissions.
        /// </param>
        /// <param name="isAdmin">
        /// Indicates whether the current user has administrative privileges.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation token.
        /// </param>
        /// <returns>
        /// A detailed game review summary including game metadata, review list,
        /// aggregate rating, and review count.
        /// </returns>
        Task<GameReviewDetailDto> GetGameReviewDetailAsync(
            int externalGameId,
            Guid? currentUserId = null,
            bool isAdmin = false,
            CancellationToken cancellationToken = default
        );
    }

    /// <summary>
    /// Service implementation that orchestrates queries against the in-memory game catalog.
    /// This implementation bypasses external API calls to provide near-instant response times.
    /// </summary>
    public class GameService : IGameService
    {
        private readonly IGameRepository _gameRepo;
        private readonly IReviewRepository _reviewRepo;
        private readonly ILogger<GameService> _logger;

        public GameService(IGameRepository gameRepo, IReviewRepository reviewRepo, ILogger<GameService> logger)
        {
            _gameRepo = gameRepo;
            _reviewRepo = reviewRepo;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<Game> GetGameDetailsAsync(int externalGameId)
        {
            var game = _gameRepo.GetById(externalGameId);

            if (game == null)
            {
                _logger.LogWarning("Game with external ID {Id} not found in memory cache.", externalGameId);
                throw new SpectrumNotFoundException(Constants.ErrorMessages.ResourceNotFound);
            }

            var reviews = await _reviewRepo.GetByGameIdAsync(externalGameId);

            if (reviews.Any())
            {
                game.InternalRating = reviews.Average(r => r.Rating);
            }

            return await Task.FromResult(game);
        }

        /// <inheritdoc />
        public async Task<GameReviewDetailDto> GetGameReviewDetailAsync(
            int externalGameId,
            Guid? currentUserId = null,
            bool isAdmin = false,
            CancellationToken cancellationToken = default
        )
        {
            var game = await GetGameDetailsAsync(externalGameId);
            var reviews = await _reviewRepo.GetByGameIdAsync(externalGameId, cancellationToken);
            var reviewDtos = reviews.Select(review =>
            {
                var profilePicture = review.User?.ProfilePicture ?? string.Empty;
                var isOwnReview = currentUserId.HasValue && review.UserId == currentUserId.Value;

                return new ReviewResponseDto
                {
                    Id = review.Id,
                    UserId = review.UserId,
                    Username = review.User?.Username ?? string.Empty,
                    UserProfileImageUrl = profilePicture,
                    ProfilePicture = profilePicture,
                    GameId = review.GameId,
                    GameTitle = game.Title,
                    GameCoverUrl = game.CoverImageUrl ?? string.Empty,
                    Rating = review.Rating,
                    Title = review.Title,
                    Content = review.Content,
                    ImageUrl = review.ImageUrl ?? string.Empty,
                    AttachmentUrl = review.ImageUrl ?? string.Empty,
                    AttachmentType = review.MediaType ?? string.Empty,
                    CreatedAt = review.CreatedAt,
                    UpdatedAt = review.UpdatedAt,
                    LikesCount = review.LikesCount,
                    DislikesCount = review.DislikesCount,
                    IsOwnReview = isOwnReview,
                    CanDelete = isOwnReview || isAdmin
                };
            }).ToList();

            return new GameReviewDetailDto
            {
                Game = game,
                Reviews = reviewDtos,
                AverageRating = reviewDtos.Count > 0 ? reviewDtos.Average(review => review.Rating) : null,
                ReviewsCount = reviewDtos.Count
            };
        }

        /// <inheritdoc />
        public async Task<PagedResult<Game>> SearchGamesAsync(GameQueryDto queryDto)
        {
            _logger.LogInformation("Searching games locally with query: {SearchTerm}", queryDto.Search);

            var (items, filteredCount) = _gameRepo.Search(queryDto);

            var spectrumRatings = await _reviewRepo.GetAverageRatingsAsync();

            foreach (var game in items)
            {
                if (spectrumRatings.TryGetValue(game.RawgId, out var avgRating))
                {
                    game.InternalRating = avgRating;
                }
            }

            var finalItems = items.ToList();
            if (queryDto.Ordering == "-rating")
            {
                finalItems = finalItems.OrderByDescending(g => g.InternalRating).ToList();
            }

            return new PagedResult<Game>
            {
                Items = finalItems,
                TotalCount = filteredCount,
                Page = queryDto.Page,
                PageSize = queryDto.PageSize ?? 20
            };
        }
    }
}
