using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spectrum.API.Dtos.Analytics;
using Spectrum.API.Services.Analytics;
using Spectrum.API.Utilities;

namespace Spectrum.API.Controllers
{
    /// <summary>
    /// Provides administrative analytics and reporting endpoints.
    /// Access is restricted to users with the Administrator role.
    /// </summary>
    [ApiController]
    [Route("api/admin/metrics")]
    [Authorize(Roles = Constants.Roles.Admin)]
    public class AdminMetricsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminMetricsController"/> class.
        /// </summary>
        /// <param name="analyticsService">
        /// Service responsible for generating platform analytics and metrics.
        /// </param>
        public AdminMetricsController(IAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        /// <summary>
        /// Retrieves aggregated platform metrics for the specified reporting period.
        /// </summary>
        /// <param name="period">
        /// Reporting period to analyze. Supported values include
        /// <c>week</c> and <c>month</c>.
        /// </param>
        /// <param name="anchorDate">
        /// Optional reference date used to calculate the reporting window.
        /// If omitted, the current UTC date is used.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <returns>
        /// Aggregated platform metrics for the requested period.
        /// </returns>
        /// <response code="200">
        /// Metrics were successfully generated.
        /// </response>
        /// <response code="400">
        /// The specified reporting period is invalid.
        /// </response>
        /// <response code="401">
        /// Authentication is required.
        /// </response>
        /// <response code="403">
        /// The authenticated user does not have administrator privileges.
        /// </response>
        [HttpGet("global")]
        [ProducesResponseType(typeof(GlobalMetricsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetGlobalMetrics(
            [FromQuery] string period = "week",
            [FromQuery] DateTime? anchorDate = null,
            CancellationToken cancellationToken = default
        )
        {
            var metrics = await _analyticsService.GetGlobalMetricsAsync(period, anchorDate, cancellationToken);
            return Ok(metrics);
        }
    }
}
