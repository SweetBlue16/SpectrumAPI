using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spectrum.API.Dtos.Analytics;
using Spectrum.API.Services.Analytics;

namespace Spectrum.API.Controllers
{
    /// <summary>
    /// Provides endpoints for retrieving analytics and dashboard information
    /// related to the Spectrum Crypt system.
    /// </summary>
    /// <remarks>
    /// This controller exposes authenticated endpoints that return aggregated
    /// analytics data used by dashboards and monitoring views.
    /// </remarks>
    [ApiController]
    [Route("api/crypt")]
    [Authorize]
    public class CryptController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;

        /// <summary>
        /// Initializes a new instance of the <see cref="CryptController"/> class.
        /// </summary>
        /// <param name="analyticsService">
        /// Service responsible for generating analytics and dashboard data.
        /// </param>
        public CryptController(IAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        /// <summary>
        /// Retrieves the analytics dashboard data for the Crypt module.
        /// </summary>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <returns>
        /// A <see cref="CryptDashboardDto"/> containing aggregated dashboard metrics.
        /// </returns>
        /// <response code="200">
        /// Dashboard information was successfully retrieved.
        /// </response>
        [HttpGet("dashboard")]
        [ProducesResponseType(typeof(CryptDashboardDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
        {
            return Ok(await _analyticsService.GetCryptDashboardAsync(cancellationToken));
        }
    }
}
