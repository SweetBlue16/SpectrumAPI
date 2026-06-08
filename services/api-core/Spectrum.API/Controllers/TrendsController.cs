using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spectrum.API.Dtos.Analytics;
using Spectrum.API.Services.Analytics;
using System.Security.Claims;

namespace Spectrum.API.Controllers
{
    [ApiController]
    [Route("api/trends")]
    [Authorize]
    public class TrendsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;

        public TrendsController(IAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        [HttpGet("weekly")]
        [ProducesResponseType(typeof(WeeklyTrendsDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetWeekly(CancellationToken cancellationToken)
        {
            var trends = await _analyticsService.GetWeeklyTrendsAsync(GetCurrentUserIdOrDefault(), cancellationToken);
            return Ok(trends);
        }

        [HttpGet("dashboard")]
        [ProducesResponseType(typeof(TrendsDashboardDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
        {
            var trends = await _analyticsService.GetTrendsDashboardAsync(GetCurrentUserIdOrDefault(), cancellationToken);
            return Ok(trends);
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
