using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spectrum.API.Dtos.Profile;
using Spectrum.API.Services.Profile;
using Spectrum.API.Utilities;
using System.Security.Claims;

namespace Spectrum.API.Controllers
{
    /// <summary>
    /// Administrative controller responsible for user moderation and account management operations.
    /// </summary>
    /// <remarks>
    /// This controller provides endpoints that allow administrators to:
    /// - Suspend user accounts.
    /// - Reactivate suspended, banned, or deleted accounts.
    /// - Ban and unban users.
    /// - Search and paginate moderated users.
    /// - Retrieve detailed moderation information for a specific user.
    /// - Soft-delete user accounts.
    /// All endpoints require administrator privileges.
    /// </remarks>
    [ApiController]
    [Route("api/admin/users")]
    [Produces("application/json")]
    [Authorize(Roles = Constants.Roles.Admin)]
    public class AdminUsersController : ControllerBase
    {
        private readonly IUserModerationService _moderationService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminUsersController"/> class.
        /// </summary>
        /// <param name="moderationService">
        /// Service responsible for user moderation and account management operations.
        /// </param>
        public AdminUsersController(IUserModerationService moderationService)
        {
            _moderationService = moderationService;
        }

        /// <summary>
        /// Suspends a user account.
        /// </summary>
        /// <param name="userId">Identifier of the user to suspend.</param>
        /// <param name="dto">
        /// Optional moderation payload containing the administrative reason for the suspension.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <returns>
        /// A success message when the account is suspended.
        /// </returns>
        /// <response code="200">
        /// User was suspended successfully.
        /// </response>
        /// <response code="403">
        /// The action is not permitted, such as attempting to moderate an administrator.
        /// </response>
        /// <response code="404">
        /// The specified user was not found.
        /// </response>
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

        /// <summary>
        /// Reactivates a suspended, banned, or deleted user account.
        /// </summary>
        /// <param name="userId">Identifier of the user to reactivate.</param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <returns>
        /// A success message when the account is reactivated.
        /// </returns>
        /// <response code="200">
        /// User account was reactivated successfully.
        /// </response>
        [HttpPatch("{userId:guid}/reactivate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ReactivateUser(Guid userId, CancellationToken cancellationToken)
        {
            await _moderationService.ReactivateUserAsync(userId, GetCurrentAdminIdOrDefault(), cancellationToken);
            return Ok(new { Message = "User has been reactivated successfully." });
        }

        /// <summary>
        /// Permanently bans a user account.
        /// </summary>
        /// <param name="userId">Identifier of the user to ban.</param>
        /// <param name="dto">
        /// Optional moderation payload containing the administrative reason for the ban.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <returns>
        /// A success message when the account is banned.
        /// </returns>
        /// <response code="200">
        /// User account was banned successfully.
        /// </response>
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

        /// <summary>
        /// Removes the ban from a user account.
        /// </summary>
        /// <param name="userId">Identifier of the user to unban.</param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <returns>
        /// A success message when the account is unbanned.
        /// </returns>
        /// <response code="200">
        /// User account was unbanned successfully.
        /// </response>
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

        /// <summary>
        /// Retrieves a paginated list of users available for moderation.
        /// </summary>
        /// <param name="page">
        /// Requested page number. Values lower than one are normalized.
        /// </param>
        /// <param name="pageSize">
        /// Number of records per page. Allowed range is 1 to 50.
        /// </param>
        /// <param name="search">
        /// Optional search term used to filter users by username or other searchable attributes.
        /// </param>
        /// <param name="status">
        /// Optional moderation status filter such as ACTIVE, SUSPENDED, BANNED, or DELETED.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <returns>
        /// A paginated collection of users with moderation information.
        /// </returns>
        /// <response code="200">
        /// Returns the filtered moderation user list.
        /// </response>
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

        /// <summary>
        /// Retrieves detailed moderation information for a specific user.
        /// </summary>
        /// <param name="id">
        /// Identifier of the user whose moderation details will be retrieved.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <returns>
        /// Detailed moderation information for the requested user.
        /// </returns>
        /// <response code="200">
        /// Returns the user's moderation details.
        /// </response>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetUserDetail(Guid id, CancellationToken cancellationToken)
        {
            var userDetail = await _moderationService.GetUserDetailAsync(id, cancellationToken);
            return Ok(userDetail);
        }

        /// <summary>
        /// Soft-deletes a user account.
        /// </summary>
        /// <remarks>
        /// The user account remains in the system but is marked as deleted
        /// and can no longer be used normally until reactivated.
        /// </remarks>
        /// <param name="id">
        /// Identifier of the user account to delete.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <returns>
        /// No content when the operation completes successfully.
        /// </returns>
        /// <response code="204">
        /// User account was deleted successfully.
        /// </response>
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
