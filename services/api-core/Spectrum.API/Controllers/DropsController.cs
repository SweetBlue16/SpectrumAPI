using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spectrum.API.Dtos.Drops;
using Spectrum.API.Exceptions;
using Spectrum.API.Services.Drops;
using Spectrum.API.Utilities;
using System.Security.Claims;

namespace Spectrum.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DropsController : ControllerBase
    {
        private readonly IDropsService _dropsService;

        public DropsController(IDropsService dropsService)
        {
            _dropsService = dropsService;
        }

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

        [HttpGet("event/{eventId}")]
        public async Task<IActionResult> GetStatus(string eventId, CancellationToken cancellationToken)
        {
            var status = await _dropsService.GetEventStatusAsync(eventId, exposeChallengeCode: false, cancellationToken, GetCurrentUserId());
            return Ok(status);
        }

        [HttpPost("event/{eventId}/join")]
        public async Task<IActionResult> Join(string eventId, CancellationToken cancellationToken)
        {
            EnsureCurrentUserCanParticipate();
            var userId = GetCurrentUserId();
            var result = await _dropsService.JoinEventAsync(userId, eventId, cancellationToken);
            return Ok(result);
        }

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

        [HttpGet("my-keys")]
        public async Task<IActionResult> GetMyKeys(CancellationToken ct)
        {
            var userId = GetCurrentUserId();
            var keys = await _dropsService.GetUserWonKeysAsync(userId, ct);
            return Ok(keys);
        }

        private Guid GetCurrentUserId()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.Unauthorized);
            }

            return userId;
        }

        private void EnsureCurrentUserCanParticipate()
        {
            if (User.IsInRole(Constants.Roles.Admin))
            {
                throw new SpectrumForbiddenException(Constants.ErrorMessages.InsufficientPermissions);
            }
        }
    }
}
