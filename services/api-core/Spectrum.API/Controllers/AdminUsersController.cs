using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spectrum.API.Dtos.Profile;
using Spectrum.API.Exceptions;
using Spectrum.API.Services.Profile;
using Spectrum.API.Utilities;
using System.Security.Claims;

namespace Spectrum.API.Controllers
{
    [ApiController]
    [Route("api/admin/users")]
    [Produces("application/json")]
    [Authorize(Roles = Constants.Roles.Admin)]
    public class AdminUsersController : ControllerBase
    {
        private readonly IUserModerationService _moderationService;

        public AdminUsersController(IUserModerationService moderationService)
        {
            _moderationService = moderationService;
        }

        [HttpPatch("{userId:guid}/suspend")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SuspendUser(Guid userId, [FromBody] AdminModerationActionDto? dto = null, CancellationToken cancellationToken = default)
        {
            await _moderationService.ToggleSuspensionAsync(
                userId,
                suspend: true,
                requesterId: GetCurrentAdminIdOrDefault(),
                reason: dto?.Reason,
                cancellationToken: cancellationToken);
            return Ok(new { Message = "User has been suspended successfully." });
        }

        [HttpPatch("{userId:guid}/reactivate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ReactivateUser(Guid userId, CancellationToken cancellationToken)
        {
            await _moderationService.ReactivateUserAsync(userId, GetCurrentAdminIdOrDefault(), cancellationToken);
            return Ok(new { Message = "User has been reactivated successfully." });
        }

        [HttpPatch("{userId:guid}/ban")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> BanUser(Guid userId, [FromBody] AdminModerationActionDto? dto, CancellationToken cancellationToken)
        {
            await _moderationService.ToggleBanAsync(
                userId,
                ban: true,
                requesterId: GetCurrentAdminIdOrDefault(),
                reason: dto?.Reason,
                cancellationToken: cancellationToken);
            return Ok(new { Message = "User has been banned successfully." });
        }

        [HttpPatch("{userId:guid}/unban")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> UnbanUser(Guid userId, CancellationToken cancellationToken)
        {
            await _moderationService.ToggleBanAsync(
                userId,
                ban: false,
                requesterId: GetCurrentAdminIdOrDefault(),
                reason: null,
                cancellationToken: cancellationToken);
            return Ok(new { Message = "User has been unbanned successfully." });
        }

        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<UserModerationDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            CancellationToken cancellationToken = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 50) pageSize = 10;

            var result = await _moderationService.GetUsersForModerationAsync(page, pageSize, search, status, cancellationToken);

            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetUserDetail(Guid id, CancellationToken cancellationToken)
        {
            var userDetail = await _moderationService.GetUserDetailAsync(id, cancellationToken);
            return Ok(userDetail);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
        {
            await _moderationService.DeleteUserAsync(id, GetCurrentAdminIdOrDefault(), reason: null, cancellationToken: cancellationToken);
            return NoContent();
        }

        private Guid? GetCurrentAdminIdOrDefault()
        {
            if (User is null)
            {
                return null;
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return null;
            }

            return userId;
        }
    }
}
