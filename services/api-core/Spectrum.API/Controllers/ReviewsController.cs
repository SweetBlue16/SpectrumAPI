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
    [ApiController]
    [Route("api/[controller]")]
    public class ReviewsController : ControllerBase
    {
        private readonly IReviewService _reviewService;
        private readonly IReviewCommentService _reviewCommentService;
        private readonly IVoteService _voteService;

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

        [HttpDelete("{reviewId:guid}")]
        [Authorize]
        public async Task<IActionResult> Delete(Guid reviewId, CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            var isAdmin = IsCurrentUserAdmin();

            await _reviewService.DeleteAsync(reviewId, userId, isAdmin, cancellationToken: cancellationToken);

            return NoContent();
        }

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

        private Guid? GetCurrentUserIdOrDefault()
        {
            var userIdClaim = GetCurrentUserIdClaim();

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }

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

        private bool IsCurrentUserAdmin()
        {
            return User.Identity?.IsAuthenticated == true &&
                   User.IsInRole(Constants.Roles.Admin);
        }
    }
}
