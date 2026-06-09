using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spectrum.API.Dtos.Home;
using Spectrum.API.Services.Home;
using Spectrum.API.Utilities;
using System.Security.Claims;

namespace Spectrum.API.Controllers
{
    /// <summary>
    /// Provides aggregated dashboard information displayed on the application's home screen,
    /// including recent games, popular reviews, and active drop events.
    /// </summary>
    [ApiController]
    [Route("api/home")]
    [Authorize]
    public class HomeController : ControllerBase
    {
        private readonly IHomeDashboardService _homeDashboardService;

        /// <summary>
        /// Initializes a new instance of the <see cref="HomeController"/> class.
        /// </summary>
        /// <param name="homeDashboardService">
        /// Service responsible for assembling dashboard data for the home page.
        /// </param>
        public HomeController(IHomeDashboardService homeDashboardService)
        {
            _homeDashboardService = homeDashboardService;
        }

        /// <summary>
        /// Retrieves the dashboard data displayed on the application's home screen.
        /// </summary>
        /// <returns>
        /// A collection of recent games, trending reviews, and current or upcoming drops.
        /// </returns>
        /// <response code="200">
        /// Dashboard information was successfully retrieved.
        /// </response>
        /// <response code="401">
        /// Authentication is required to access this resource.
        /// </response>
        [HttpGet("dashboard")]
        [ProducesResponseType(typeof(HomeDashboardDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
        {
            return Ok(await _homeDashboardService.GetDashboardAsync(
                GetCurrentUserIdOrDefault(),
                HttpContext?.User?.IsInRole(Constants.Roles.Admin) == true,
                cancellationToken
            ));
        }

        private Guid? GetCurrentUserIdOrDefault()
        {
            var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                              User?.FindFirst("sub")?.Value ??
                              User?.FindFirst("userId")?.Value;

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }
}
