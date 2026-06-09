using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spectrum.API.Dtos.Drops;
using Spectrum.API.Exceptions;
using Spectrum.API.Services.Drops;
using Spectrum.API.Utilities;
using System.Security.Claims;

namespace Spectrum.API.Controllers
{
    /// <summary>
    /// Exposes authenticated endpoints for participating in giveaway events ("Drops"),
    /// including event discovery, participation, reward claiming, and retrieval of won keys.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DropsController : ControllerBase
    {
        private readonly IDropsService _dropsService;

        /// <summary>
        /// Initializes a new instance of the <see cref="DropsController"/> class.
        /// </summary>
        /// <param name="dropsService">
        /// Service responsible for managing giveaway events, participation,
        /// winner selection, and reward distribution.
        /// </param>
        public DropsController(IDropsService dropsService)
        {
            _dropsService = dropsService;
        }

        /// <summary>
        /// Retrieves a paginated list of giveaway events available to the current user.
        /// </summary>
        /// <param name="scope">
        /// Event scope filter. Typical values include CURRENT, UPCOMING, and PAST.
        /// </param>
        /// <param name="page">The requested page number.</param>
        /// <param name="pageSize">The number of records per page.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A paginated collection of giveaway events.</returns>
        /// <response code="200">Events were retrieved successfully.</response>
        /// <response code="401">The client is not authenticated.</response>
        [HttpGet("events")]
        public async Task<IActionResult> ListEvents(
            [FromQuery] string scope = "CURRENT",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken cancellationToken = default
        )
        {
            var currentUserId = GetCurrentUserId();
            var events = await _dropsService.ListEventsAsync(
                scope,
                page,
                pageSize,
                includeDrafts: false,
                cancellationToken,
                currentUserId
            );
            return Ok(events);
        }

        /// <summary>
        /// Retrieves the current status and details of a specific giveaway event.
        /// </summary>
        /// <param name="eventId">The unique event identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The current state of the requested event.</returns>
        /// <response code="200">Event information was retrieved successfully.</response>
        /// <response code="401">The client is not authenticated.</response>
        /// <response code="404">The specified event was not found.</response>
        [HttpGet("event/{eventId}")]
        public async Task<IActionResult> GetStatus(string eventId, CancellationToken cancellationToken)
        {
            var status = await _dropsService.GetEventStatusAsync(eventId, exposeChallengeCode: false, cancellationToken, GetCurrentUserId());
            return Ok(status);
        }

        /// <summary>
        /// Registers the current authenticated reviewer as a participant in a giveaway event.
        /// </summary>
        /// <param name="eventId">The unique event identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The participation result.</returns>
        /// <response code="200">The user joined the event successfully.</response>
        /// <response code="401">The client is not authenticated.</response>
        /// <response code="403">Administrators cannot participate in giveaway events.</response>
        /// <response code="409">The user has already joined or the event cannot accept participants.</response>
        [HttpPost("event/{eventId}/join")]
        public async Task<IActionResult> Join(string eventId, CancellationToken cancellationToken)
        {
            EnsureCurrentUserCanParticipate();
            var userId = GetCurrentUserId();
            var result = await _dropsService.JoinEventAsync(userId, eventId, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Claims a reward key associated with a completed giveaway event.
        /// </summary>
        /// <param name="eventId">The unique event identifier.</param>
        /// <param name="dto">Claim request containing the required claim information.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The claimed reward details.</returns>
        /// <response code="200">The reward key was claimed successfully.</response>
        /// <response code="401">The client is not authenticated.</response>
        /// <response code="403">Administrators cannot claim rewards.</response>
        /// <response code="404">The event or reward was not found.</response>
        /// <response code="409">The reward has already been claimed or is unavailable.</response>
        [HttpPost("claim/{eventId}")]
        public async Task<IActionResult> Claim(
            string eventId,
            [FromBody] ClaimDropDto dto,
            CancellationToken cancellationToken
        )
        {
            EnsureCurrentUserCanParticipate();
            var userId = GetCurrentUserId();
            var result = await _dropsService.ClaimAccessKeyAsync(userId, eventId, dto, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Retrieves all access keys won by the currently authenticated user.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A collection of claimed access keys.</returns>
        /// <response code="200">The keys were retrieved successfully.</response>
        /// <response code="401">The client is not authenticated.</response>
        [HttpGet("my-keys")]
        public async Task<IActionResult> GetMyKeys(CancellationToken ct)
        {
            var userId = GetCurrentUserId();
            var keys = await _dropsService.GetUserWonKeysAsync(userId, ct);
            return Ok(keys);
        }

        /// <summary>
        /// Extracts the authenticated user's identifier from the JWT claims principal.
        /// </summary>
        /// <returns>The authenticated user's unique identifier.</returns>
        /// <exception cref="SpectrumUnauthorizedException">
        /// Thrown when the user identifier claim is missing or invalid.
        /// </exception>
        private Guid GetCurrentUserId()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.Unauthorized);
            }

            return userId;
        }

        /// <summary>
        /// Ensures that the current authenticated user is eligible to participate in giveaway events.
        /// </summary>
        /// <exception cref="SpectrumForbiddenException">
        /// Thrown when an administrator attempts to participate in reviewer-only functionality.
        /// </exception>
        private void EnsureCurrentUserCanParticipate()
        {
            if (User.IsInRole(Constants.Roles.Admin))
            {
                throw new SpectrumForbiddenException(Constants.ErrorMessages.InsufficientPermissions);
            }
        }
    }
}
