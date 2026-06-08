using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spectrum.API.Dtos.Home;
using Spectrum.API.Services.Home;
using Spectrum.API.Utilities;
using System.Security.Claims;

namespace Spectrum.API.Controllers
{
    [ApiController]
    [Route("api/home")]
    [Authorize]
    public class HomeController : ControllerBase
    {
        private readonly IHomeDashboardService _homeDashboardService;

        public HomeController(IHomeDashboardService homeDashboardService)
        {
            _homeDashboardService = homeDashboardService;
        }

        [HttpGet("dashboard")]
        [ProducesResponseType(typeof(HomeDashboardDto), StatusCodes.Status200OK)]
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
