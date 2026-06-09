using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spectrum.API.Dtos.Analytics;
using Spectrum.API.Services.Analytics;
using System.Security.Claims;

namespace Spectrum.API.Controllers
{
    /// <summary>
    /// Provides analytics and trend discovery endpoints for the Spectrum platform.
    /// Supplies personalized and global trend information based on user activity and engagement metrics.
    /// </summary>
    [ApiController]
    [Route("api/trends")]
    [Authorize]
    public class TrendsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;

        /// <summary>
        /// Initializes a new instance of the <see cref="TrendsController"/> class.
        /// </summary>
        /// <param name="analyticsService">
        /// The service responsible for generating analytics and trend reports.
        /// </param>
        public TrendsController(IAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        /// <summary>
        /// Retrieves the weekly trends feed for the authenticated user.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token used to cancel the request.
        /// </param>
        /// <returns>
        /// A collection of trending games, reviews, clips, and recommendations for the current week.
        /// </returns>
        /// <response code="200">
        /// Weekly trend data was successfully retrieved.
        /// </response>
        /// <response code="401">
        /// The user is not authenticated.
        /// </response>
        [HttpGet("weekly")]
        [ProducesResponseType(typeof(WeeklyTrendsDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetWeekly(CancellationToken cancellationToken)
        {
            var trends = await _analyticsService.GetWeeklyTrendsAsync(GetCurrentUserIdOrDefault(), cancellationToken);
            return Ok(trends);
        }

        /// <summary>
        /// Retrieves the analytics dashboard containing aggregated trend metrics.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token used to cancel the request.
        /// </param>
        /// <returns>
        /// A dashboard containing summarized trend indicators, rankings, and engagement statistics.
        /// </returns>
        /// <response code="200">
        /// Dashboard analytics were successfully generated.
        /// </response>
        /// <response code="401">
        /// The user is not authenticated.
        /// </response>
        [HttpGet("dashboard")]
        [ProducesResponseType(typeof(TrendsDashboardDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
        {
            var trends = await _analyticsService.GetTrendsDashboardAsync(GetCurrentUserIdOrDefault(), cancellationToken);
            return Ok(trends);
        }

        /// <summary>
        /// Attempts to retrieve the current authenticated user's identifier.
        /// </summary>
        /// <returns>
        /// The authenticated user's identifier if available; otherwise <c>null</c>.
        /// </returns>
        private Guid? GetCurrentUserIdOrDefault()
        {
            var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                              User?.FindFirst("sub")?.Value ??
                              User?.FindFirst("userId")?.Value;

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }
}
