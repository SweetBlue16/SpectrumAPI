using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spectrum.API.Dtos.Analytics;
using Spectrum.API.Services.Analytics;
using Spectrum.API.Utilities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Spectrum.API.Controllers
{
    /// <summary>
    /// Provides access to weekly and monthly rankings of game clips.
    /// Rankings are generated using engagement and popularity metrics collected across the platform.
    /// </summary>
    [ApiController]
    [Route("api/clips/weekly")]
    [Authorize]
    public class WeeklyClipsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;

        /// <summary>
        /// Initializes a new instance of the <see cref="WeeklyClipsController"/> class.
        /// </summary>
        /// <param name="analyticsService">
        /// The service responsible for generating clip ranking analytics.
        /// </param>
        public WeeklyClipsController(IAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        /// <summary>
        /// Retrieves a paginated list of the most popular clips for the current week.
        /// </summary>
        /// <param name="page">
        /// The page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// The maximum number of clips returned per page.
        /// </param>
        /// <param name="cancellationToken">
        /// A token used to cancel the request.
        /// </param>
        /// <returns>
        /// A paginated ranking of weekly trending clips.
        /// </returns>
        /// <response code="200">
        /// Weekly clip rankings were successfully retrieved.
        /// </response>
        /// <response code="401">
        /// The user is not authenticated.
        /// </response>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<WeeklyReviewDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetWeeklyClips(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken cancellationToken = default
        )
        {
            var clips = await _analyticsService.GetWeeklyClipsAsync(page, pageSize, GetCurrentUserId(), cancellationToken);
            return Ok(clips);
        }

        /// <summary>
        /// Retrieves the highest-ranked clips from the current month.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token used to cancel the request.
        /// </param>
        /// <returns>
        /// A collection containing the top-performing clips of the month.
        /// </returns>
        /// <response code="200">
        /// Monthly clip rankings were successfully retrieved.
        /// </response>
        /// <response code="401">
        /// The user is not authenticated.
        /// </response>
        [HttpGet("monthly-top")]
        [ProducesResponseType(typeof(IReadOnlyList<WeeklyReviewDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMonthlyTopClips(CancellationToken cancellationToken = default)
        {
            return Ok(await _analyticsService.GetMonthlyTopClipsAsync(GetCurrentUserId(), cancellationToken));
        }

        /// <summary>
        /// Attempts to retrieve the current authenticated user's identifier.
        /// </summary>
        /// <returns>
        /// The authenticated user's identifier if available; otherwise <c>null</c>.
        /// </returns>
        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                             ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }
}
