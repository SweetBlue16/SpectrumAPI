using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spectrum.API.Dtos.Admin;
using Spectrum.API.Services.Admin;
using Spectrum.API.Utilities;

namespace Spectrum.API.Controllers
{
    /// <summary>
    /// Provides operations for retrieving and updating the authenticated administrator profile.
    /// </summary>
    [ApiController]
    [Route("api/admin/profile")]
    [Authorize(Roles = Constants.Roles.Admin)]
    public class AdminProfileController : ControllerBase
    {
        private readonly IAdminProfileService _profileService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminProfileController"/> class.
        /// </summary>
        /// <param name="profileService">
        /// Service responsible for administrator profile management.
        /// </param>
        public AdminProfileController(IAdminProfileService profileService)
        {
            _profileService = profileService;
        }

        /// <summary>
        /// Retrieves the profile information of the authenticated administrator.
        /// </summary>
        /// <param name="cancellationToken">
        /// Token used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// The administrator profile information.
        /// </returns>
        /// <response code="200">
        /// Profile information retrieved successfully.
        /// </response>
        /// <response code="400">
        /// The request is invalid.
        /// </response>
        /// <response code="404">
        /// The administrator profile was not found.
        /// </response>
        [HttpGet]
        [ProducesResponseType(typeof(AdminProfileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            var profile = await _profileService.GetProfileAsync(GetCurrentAdminId(), cancellationToken);
            return Ok(profile);
        }

        /// <summary>
        /// Updates the profile information of the authenticated administrator.
        /// </summary>
        /// <param name="dto">
        /// Updated administrator profile information.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// The updated administrator profile.
        /// </returns>
        /// <response code="200">
        /// Profile updated successfully.
        /// </response>
        /// <response code="400">
        /// The request payload failed validation.
        /// </response>
        [HttpPut]
        [ProducesResponseType(typeof(AdminProfileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Update([FromBody] UpdateAdminProfileDto dto, CancellationToken cancellationToken)
        {
            var profile = await _profileService.UpdateProfileAsync(GetCurrentAdminId(), dto, cancellationToken);
            return Ok(profile);
        }

        private Guid GetCurrentAdminId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
                ?? User.FindFirstValue("userId");

            return Guid.TryParse(userId, out var parsedUserId)
                ? parsedUserId
                : throw new UnauthorizedAccessException();
        }
    }
}
