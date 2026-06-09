using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Spectrum.API.Data;
using Spectrum.API.Dtos.Reviews;
using Spectrum.API.Repositories;
using Spectrum.API.Services.Reviews;
using Spectrum.API.Utilities;
using System.Security.Claims;

namespace Spectrum.API.Controllers
{
    /// <summary>
    /// Administrative controller responsible for reviewing, searching, and removing user reviews.
    /// Access is restricted to users with the Admin role.
    /// </summary>
    [ApiController]
    [Route("api/admin/reviews")]
    [Authorize(Roles = Constants.Roles.Admin)]
    public class AdminReviewsController : ControllerBase
    {
        private readonly SpectrumDbContext _context;
        private readonly IGameRepository _gameRepository;
        private readonly IReviewService _reviewService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminReviewsController"/> class.
        /// </summary>
        /// <param name="context">Database context used to query reviews.</param>
        /// <param name="gameRepository">Repository used to retrieve game information.</param>
        /// <param name="reviewService">Service responsible for review management operations.</param>
        public AdminReviewsController(
            SpectrumDbContext context,
            IGameRepository gameRepository,
            IReviewService reviewService
        )
        {
            _context = context;
            _gameRepository = gameRepository;
            _reviewService = reviewService;
        }

        /// <summary>
        /// Searches and retrieves reviews for moderation purposes.
        /// Supports filtering by game identifier, game title, review title,
        /// username, sorting, and pagination.
        /// </summary>
        /// <param name="gameId">
        /// Optional game identifier used to filter reviews belonging to a specific game.
        /// </param>
        /// <param name="gameQuery">
        /// Optional game title search term used to locate matching games.
        /// </param>
        /// <param name="search">
        /// Optional search term used to filter reviews by title or author username.
        /// </param>
        /// <param name="sort">
        /// Sorting criteria. Supported values:
        /// date_desc, date_asc, likes, dislikes, alpha.
        /// </param>
        /// <param name="page">Requested page number.</param>
        /// <param name="pageSize">Number of records per page.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// A paginated collection of reviews that match the specified criteria.
        /// Returns an empty result when neither <paramref name="gameId"/> nor
        /// <paramref name="gameQuery"/> is provided.
        /// </returns>
        /// <response code="200">
        /// Reviews were retrieved successfully.
        /// </response>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<ReviewResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Search(
            [FromQuery] int? gameId = null,
            [FromQuery] string? gameQuery = null,
            [FromQuery] string? search = null,
            [FromQuery] string sort = "date_desc",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken cancellationToken = default
        )
        {
            var normalizedPage = Math.Max(1, page);
            var normalizedPageSize = Math.Clamp(pageSize, 1, 50);
            var query = _context.Reviews
                .AsNoTracking()
                .Include(review => review.User)
                .AsQueryable();

            if (gameId.HasValue)
            {
                query = query.Where(review => review.GameId == gameId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(gameQuery))
            {
                var matchingGameIds = _gameRepository.GetAll()
                    .Where(game => game.Title.Contains(gameQuery, StringComparison.OrdinalIgnoreCase))
                    .Select(game => game.RawgId)
                    .Take(25)
                    .ToArray();

                query = query.Where(review => matchingGameIds.Contains(review.GameId));
            }
            else
            {
                return Ok(new PagedResult<ReviewResponseDto>
                {
                    Items = [],
                    TotalCount = 0,
                    Page = normalizedPage,
                    PageSize = normalizedPageSize
                });
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalizedSearch = search.Trim().ToLower();
                query = query.Where(review =>
                    review.Title.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    review.User != null && review.User.Username.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
            }

            query = sort.ToLowerInvariant() switch
            {
                "date_asc" => query.OrderBy(review => review.CreatedAt),
                "likes" => query.OrderByDescending(review => review.LikesCount),
                "dislikes" => query.OrderByDescending(review => review.DislikesCount),
                "alpha" => query.OrderBy(review => review.Title),
                _ => query.OrderByDescending(review => review.CreatedAt)
            };

            var total = await query.CountAsync(cancellationToken);
            var reviews = await query
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToListAsync(cancellationToken);

            var items = reviews.Select(review =>
            {
                var game = _gameRepository.GetById(review.GameId);
                return new ReviewResponseDto
                {
                    Id = review.Id,
                    UserId = review.UserId,
                    Username = review.User?.Username ?? string.Empty,
                    UserProfileImageUrl = review.User?.ProfilePicture ?? string.Empty,
                    ProfilePicture = review.User?.ProfilePicture ?? string.Empty,
                    GameId = review.GameId,
                    GameTitle = game?.Title ?? string.Empty,
                    GameCoverUrl = game?.CoverImageUrl ?? string.Empty,
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
                    CanDelete = true
                };
            }).ToList();

            return Ok(new PagedResult<ReviewResponseDto>
            {
                Items = items,
                TotalCount = total,
                Page = normalizedPage,
                PageSize = normalizedPageSize
            });
        }

        /// <summary>
        /// Deletes a review as an administrative moderation action.
        /// The deletion reason is recorded and may be used for notifications.
        /// </summary>
        /// <param name="reviewId">Identifier of the review to delete.</param>
        /// <param name="dto">
        /// Request containing the administrative deletion reason.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// <see cref="NoContentResult"/> when the review is successfully deleted.
        /// </returns>
        /// <response code="204">
        /// Review deleted successfully.
        /// </response>
        /// <response code="400">
        /// The deletion request is invalid.
        /// </response>
        /// <response code="404">
        /// The specified review was not found.
        /// </response>
        /// <response code="403">
        /// The current user is not authorized to perform this action.
        /// </response>
        [HttpDelete("{reviewId:guid}")]
        public async Task<IActionResult> Delete(
            Guid reviewId,
            [FromBody] AdminDeleteReviewDto dto,
            CancellationToken cancellationToken
        )
        {
            var adminId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedAdminId)
                ? parsedAdminId
                : Guid.Empty;
            await _reviewService.DeleteAsync(reviewId, adminId, isAdmin: true, deletionReason: dto.Reason, cancellationToken);
            return NoContent();
        }
    }
}
