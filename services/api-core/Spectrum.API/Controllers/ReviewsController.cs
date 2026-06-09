using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spectrum.API.Dtos.Reviews;
using Spectrum.API.Dtos.Votes;
using Spectrum.API.Exceptions;
using Spectrum.API.Services.Reviews;
using Spectrum.API.Services.Votes;
using Spectrum.API.Utilities;
using System.Security.Claims;

namespace Spectrum.API.Controllers
{
    /// <summary>
    /// Controller responsible for managing game reviews, comments, and community voting interactions.
    /// Provides endpoints for creating, updating, retrieving, deleting, and moderating review-related content.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ReviewsController : ControllerBase
    {
        private readonly IReviewService _reviewService;
        private readonly IReviewCommentService _reviewCommentService;
        private readonly IVoteService _voteService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReviewsController"/> class.
        /// </summary>
        /// <param name="reviewService">The service responsible for review lifecycle operations.</param>
        /// <param name="reviewCommentService">The service responsible for review comment management.</param>
        /// <param name="voteService">The service responsible for processing review votes.</param>
        public ReviewsController(
            IReviewService reviewService,
            IReviewCommentService reviewCommentService,
            IVoteService voteService
        )
        {
            _reviewService = reviewService;
            _reviewCommentService = reviewCommentService;
            _voteService = voteService;
        }

        /// <summary>
        /// Creates a new review for a game on behalf of the authenticated user.
        /// </summary>
        /// <param name="dto">The review creation payload.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The newly created review resource.</returns>
        /// <response code="201">The review was successfully created.</response>
        /// <response code="400">The submitted review data is invalid.</response>
        /// <response code="401">The user is not authenticated.</response>
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<ReviewResponseDto>> Create(
            [FromBody] CreateReviewDto dto,
            CancellationToken cancellationToken
        )
        {
            var userId = GetCurrentUserId();
            var review = await _reviewService.CreateAsync(dto, userId, cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { reviewId = review.Id },
                review
            );
        }

        /// <summary>
        /// Updates an existing review owned by the authenticated user.
        /// </summary>
        /// <param name="reviewId">The unique identifier of the review.</param>
        /// <param name="dto">The updated review content.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A 204 No Content response if the update succeeds.</returns>
        /// <response code="204">The review was successfully updated.</response>
        /// <response code="401">The user is not authenticated.</response>
        /// <response code="403">The user is not authorized to modify the review.</response>
        /// <response code="404">The specified review does not exist.</response>
        [HttpPut("{reviewId:guid}")]
        [Authorize]
        public async Task<IActionResult> Update(
            Guid reviewId,
            [FromBody] UpdateReviewDto dto,
            CancellationToken cancellationToken
        )
        {
            var userId = GetCurrentUserId();

            await _reviewService.UpdateAsync(reviewId, dto, userId, cancellationToken);

            return NoContent();
        }

        /// <summary>
        /// Deletes a review owned by the current user or managed by an administrator.
        /// </summary>
        /// <param name="reviewId">The unique identifier of the review to remove.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A 204 No Content response if deletion succeeds.</returns>
        /// <response code="204">The review was successfully deleted.</response>
        /// <response code="401">The user is not authenticated.</response>
        /// <response code="403">The user lacks permission to delete the review.</response>
        /// <response code="404">The specified review was not found.</response>
        [HttpDelete("{reviewId:guid}")]
        [Authorize]
        public async Task<IActionResult> Delete(Guid reviewId, CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            var isAdmin = IsCurrentUserAdmin();

            await _reviewService.DeleteAsync(reviewId, userId, isAdmin, cancellationToken: cancellationToken);

            return NoContent();
        }

        /// <summary>
        /// Retrieves the complete details of a specific review.
        /// </summary>
        /// <param name="reviewId">The unique identifier of the review.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The requested review.</returns>
        /// <response code="200">The review was successfully retrieved.</response>
        /// <response code="404">The review does not exist.</response>
        [HttpGet("{reviewId:guid}")]
        public async Task<ActionResult<ReviewResponseDto>> GetById(
            Guid reviewId,
            CancellationToken cancellationToken
        )
        {
            var review = await _reviewService.GetByIdAsync(
                reviewId,
                GetCurrentUserIdOrDefault(),
                cancellationToken
            );

            return Ok(review);
        }

        /// <summary>
        /// Retrieves all reviews associated with a specific game.
        /// </summary>
        /// <param name="gameId">The RAWG identifier of the game.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A collection of reviews for the specified game.</returns>
        /// <response code="200">The reviews were successfully retrieved.</response>
        [HttpGet("game/{gameId:int}")]
        public async Task<ActionResult<IReadOnlyList<ReviewResponseDto>>> GetByGame(
            int gameId,
            CancellationToken cancellationToken
        )
        {
            var reviews = await _reviewService.GetByGameIdAsync(
                gameId,
                GetCurrentUserIdOrDefault(),
                IsCurrentUserAdmin(),
                cancellationToken
            );

            return Ok(reviews);
        }

        /// <summary>
        /// Retrieves all reviews authored by the currently authenticated user.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A collection of the user's reviews.</returns>
        /// <response code="200">The reviews were successfully retrieved.</response>
        /// <response code="401">The user is not authenticated.</response>
        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<IReadOnlyList<ReviewResponseDto>>> GetMine(
            CancellationToken cancellationToken
        )
        {
            var userId = GetCurrentUserId();
            var reviews = await _reviewService.GetByUserIdAsync(userId, userId, cancellationToken);

            return Ok(reviews);
        }

        /// <summary>
        /// Retrieves all public reviews authored by a specific user.
        /// </summary>
        /// <param name="userId">The unique identifier of the review author.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A collection of reviews published by the specified user.</returns>
        /// <response code="200">The reviews were successfully retrieved.</response>
        /// <response code="401">The user is not authenticated.</response>
        [HttpGet("users/{userId:guid}")]
        [Authorize]
        public async Task<ActionResult<IReadOnlyList<ReviewResponseDto>>> GetByUser(
            Guid userId,
            CancellationToken cancellationToken
        )
        {
            var reviews = await _reviewService.GetByUserIdAsync(
                userId,
                GetCurrentUserIdOrDefault(),
                cancellationToken
            );

            return Ok(reviews);
        }

        /// <summary>
        /// Casts a positive or negative vote on a review.
        /// </summary>
        /// <param name="reviewId">The unique identifier of the review.</param>
        /// <param name="dto">The vote payload indicating whether the vote is positive.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The updated voting statistics for the review.</returns>
        /// <response code="200">The vote was successfully recorded.</response>
        /// <response code="401">The user is not authenticated.</response>
        /// <response code="403">The user is not allowed to vote on reviews.</response>
        /// <response code="404">The specified review was not found.</response>
        [HttpPost("{reviewId:guid}/vote")]
        [Authorize]
        public async Task<ActionResult<VoteResultDto>> Vote(
            Guid reviewId,
            [FromBody] CastReviewVoteDto dto,
            CancellationToken cancellationToken
        )
        {
            var userId = GetCurrentUserId();
            if (IsCurrentUserAdmin())
            {
                throw new SpectrumForbiddenException("Los administradores no pueden votar resenas.");
            }

            var result = await _voteService.CastReviewVoteAsync(
                reviewId,
                userId,
                dto.IsPositive,
                cancellationToken
            );

            return Ok(result);
        }

        /// <summary>
        /// Creates a new comment on an existing review.
        /// </summary>
        /// <param name="reviewId">The unique identifier of the review.</param>
        /// <param name="dto">The comment creation payload.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The newly created review comment.</returns>
        /// <response code="200">The comment was successfully created.</response>
        /// <response code="401">The user is not authenticated.</response>
        /// <response code="404">The target review does not exist.</response>
        [HttpPost("{reviewId:guid}/comments")]
        [Authorize]
        public async Task<ActionResult<ReviewCommentResponseDto>> CreateComment(
            Guid reviewId,
            [FromBody] CreateReviewCommentDto dto,
            CancellationToken cancellationToken
        )
        {
            var userId = GetCurrentUserId();

            var comment = await _reviewCommentService.CreateAsync(
                reviewId,
                dto,
                userId,
                cancellationToken
            );

            return Ok(comment);
        }

        /// <summary>
        /// Retrieves a paginated collection of comments associated with a review.
        /// </summary>
        /// <param name="reviewId">The unique identifier of the review.</param>
        /// <param name="page">The requested page number.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A collection of review comments.</returns>
        /// <response code="200">The comments were successfully retrieved.</response>
        [HttpGet("{reviewId:guid}/comments")]
        public async Task<ActionResult<IReadOnlyList<ReviewCommentResponseDto>>> GetComments(
            Guid reviewId,
            [FromQuery] int page,
            CancellationToken cancellationToken
        )
        {
            var comments = await _reviewCommentService.GetByReviewAsync(
                reviewId,
                GetCurrentUserIdOrDefault(),
                IsCurrentUserAdmin(),
                page,
                cancellationToken
            );

            return Ok(comments);
        }

        /// <summary>
        /// Deletes a review comment owned by the current user or moderated by an administrator.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A 204 No Content response if deletion succeeds.</returns>
        /// <response code="204">The comment was successfully deleted.</response>
        /// <response code="401">The user is not authenticated.</response>
        /// <response code="403">The user lacks permission to delete the comment.</response>
        /// <response code="404">The specified comment was not found.</response>
        [HttpDelete("comments/{commentId}")]
        [Authorize]
        public async Task<IActionResult> DeleteComment(
            string commentId,
            CancellationToken cancellationToken
        )
        {
            var userId = GetCurrentUserId();

            await _reviewCommentService.DeleteAsync(
                commentId,
                userId,
                IsCurrentUserAdmin(),
                cancellationToken
            );

            return NoContent();
        }

        /// <summary>
        /// Retrieves the authenticated user's identifier from the current security context.
        /// </summary>
        /// <returns>The authenticated user's unique identifier.</returns>
        /// <exception cref="SpectrumUnauthorizedException">
        /// Thrown when the user identifier claim is missing or invalid.
        /// </exception>
        private Guid GetCurrentUserId()
        {
            var userIdClaim = GetCurrentUserIdClaim();

            if (string.IsNullOrWhiteSpace(userIdClaim))
            {
                throw new SpectrumUnauthorizedException("No se encontro el identificador del usuario autenticado.");
            }

            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                throw new SpectrumUnauthorizedException("El identificador del usuario autenticado no es valido.");
            }

            return userId;
        }

        /// <summary>
        /// Attempts to retrieve the authenticated user's identifier from the current security context.
        /// </summary>
        /// <returns>
        /// The authenticated user's identifier if available; otherwise <c>null</c>.
        /// </returns>
        private Guid? GetCurrentUserIdOrDefault()
        {
            var userIdClaim = GetCurrentUserIdClaim();

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        /// <summary>
        /// Retrieves the raw user identifier claim from the authenticated principal.
        /// </summary>
        /// <returns>
        /// The claim value if present; otherwise <c>null</c>.
        /// </returns>
        private string? GetCurrentUserIdClaim()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                   User.FindFirst("sub")?.Value ??
                   User.FindFirst("userId")?.Value;
        }

        /// <summary>
        /// Determines whether the currently authenticated user belongs to the administrator role.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the current user is an administrator; otherwise <c>false</c>.
        /// </returns>
        private bool IsCurrentUserAdmin()
        {
            return User.Identity?.IsAuthenticated == true &&
                   User.IsInRole(Constants.Roles.Admin);
        }
    }
}
