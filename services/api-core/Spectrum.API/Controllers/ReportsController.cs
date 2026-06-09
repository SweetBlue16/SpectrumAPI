using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spectrum.API.Dtos.Reports;
using Spectrum.API.Services.Reports;
using Spectrum.API.Utilities;
using System.Security.Claims;

namespace Spectrum.API.Controllers
{
    /// <summary>
    /// Controller responsible for handling user-generated content reports and moderation workflows.
    /// Provides endpoints for report submission, retrieval, and status management.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReportsController"/> class.
        /// </summary>
        /// <param name="reportService">The service responsible for report processing and moderation operations.</param>
        public ReportsController(IReportService reportService)
        {
            _reportService = reportService;
        }

        /// <summary>
        /// Submits a new report against a user, review, or comment.
        /// The authenticated user becomes the reporter of the submitted content.
        /// </summary>
        /// <param name="dto">The report payload containing target information, reason, and optional details.</param>
        /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
        /// <returns>A confirmation message indicating that the report was successfully submitted.</returns>
        /// <response code="200">The report was successfully registered.</response>
        /// <response code="400">The report request is invalid or fails business validation rules.</response>
        /// <response code="401">The user is not authenticated or the token is invalid.</response>
        [HttpPost]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SubmitReport([FromBody] CreateReportDto dto, CancellationToken cancellationToken)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();

            await _reportService.SubmitReportAsync(userId, dto, cancellationToken);

            return Ok(new { Message = "Report submitted successfully." });
        }

        /// <summary>
        /// Retrieves all reports matching the specified moderation status.
        /// Intended for administrative review and moderation dashboards.
        /// </summary>
        /// <param name="status">
        /// The report status filter. Defaults to <c>PENDING</c>.
        /// Supported values typically include PENDING, RESOLVED, and other moderation states.
        /// </param>
        /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
        /// <returns>A collection of reports matching the requested status.</returns>
        /// <response code="200">The reports were successfully retrieved.</response>
        /// <response code="401">The user is not authenticated.</response>
        /// <response code="403">The authenticated user lacks administrative permissions.</response>
        [HttpGet]
        [Authorize(Roles = Constants.Roles.Admin)]
        [ProducesResponseType(typeof(IEnumerable<ReportDetailsDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetReports([FromQuery] string status = "PENDING", CancellationToken cancellationToken = default)
        {
            var reports = await _reportService.GetReportsByStatusAsync(status.ToUpper(), cancellationToken);
            return Ok(reports);
        }

        /// <summary>
        /// Updates the moderation status of an existing report.
        /// Allows administrators to resolve, dismiss, or otherwise process reported content.
        /// </summary>
        /// <param name="reportId">The unique identifier of the report to update.</param>
        /// <param name="dto">The payload containing the new report status and moderation notes.</param>
        /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
        /// <returns>A confirmation message indicating the report status was updated.</returns>
        /// <response code="200">The report status was successfully updated.</response>
        /// <response code="401">The user is not authenticated or the token is invalid.</response>
        /// <response code="403">The authenticated user lacks administrative permissions.</response>
        /// <response code="404">The specified report could not be found.</response>
        [HttpPatch("{reportId}")]
        [Authorize(Roles = Constants.Roles.Admin)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ResolveReport(string reportId, [FromBody] UpdateReportStatusDto dto, CancellationToken cancellationToken)
        {
            var moderatorIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(moderatorIdStr, out var moderatorId)) return Unauthorized();

            await _reportService.UpdateReportStatusAsync(reportId, moderatorId, dto, cancellationToken);

            return Ok(new { Message = $"Report status updated to {dto.NewStatus}." });
        }
    }
}
