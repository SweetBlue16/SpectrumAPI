using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Spectrum.API.Data;
using Spectrum.API.Dtos.Reports;
using Spectrum.API.Exceptions;
using Spectrum.API.Services.Reports;
using Spectrum.API.Services.Reviews;
using Spectrum.API.Services.Profile;
using Spectrum.API.Utilities;
using System.Security.Claims;

namespace Spectrum.API.Controllers
{
    /// <summary>
    /// Provides administrative moderation endpoints for reviewing,
    /// resolving, dismissing, and acting upon user-generated reports.
    /// </summary>
    [ApiController]
    [Route("api/admin/reports")]
    [Authorize(Roles = Constants.Roles.Admin)]
    public class AdminReportsController : ControllerBase
    {
        private static readonly string[] KnownStatuses = ["PENDING", "RESOLVED", "DISMISSED"];

        private readonly IReportService _reportService;
        private readonly IReviewService _reviewService;
        private readonly IUserModerationService _userModerationService;
        private readonly SpectrumDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminReportsController"/> class.
        /// </summary>
        /// <param name="reportService">
        /// Service responsible for report management and moderation workflows.
        /// </param>
        /// <param name="reviewService">
        /// Service responsible for review moderation actions.
        /// </param>
        /// <param name="userModerationService">
        /// Service responsible for user suspension and moderation actions.
        /// </param>
        /// <param name="context">
        /// Database context used to enrich report information.
        /// </param>
        public AdminReportsController(
            IReportService reportService,
            IReviewService reviewService,
            IUserModerationService userModerationService,
            SpectrumDbContext context)
        {
            _reportService = reportService;
            _reviewService = reviewService;
            _userModerationService = userModerationService;
            _context = context;
        }

        /// <summary>
        /// Retrieves a paginated list of moderation reports with optional
        /// filtering, searching, and sorting capabilities.
        /// </summary>
        /// <param name="page">The requested page number.</param>
        /// <param name="pageSize">The number of reports per page.</param>
        /// <param name="status">
        /// Optional report status filter. Use ALL to retrieve reports of every status.
        /// </param>
        /// <param name="targetType">
        /// Optional reported entity type filter.
        /// </param>
        /// <param name="search">
        /// Optional search text applied to report identifiers and reasons.
        /// </param>
        /// <param name="sort">
        /// Sorting mode. Supported values include date_desc and type.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <returns>
        /// A paginated collection of moderation reports.
        /// </returns>
        /// <response code="200">
        /// Reports retrieved successfully.
        /// </response>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<ReportDetailsDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null,
            [FromQuery] string? targetType = null,
            [FromQuery] string? search = null,
            [FromQuery] string sort = "date_desc",
            CancellationToken cancellationToken = default
        )
        {
            var normalizedPage = Math.Max(1, page);
            var normalizedPageSize = Math.Clamp(pageSize, 1, 50);
            var statuses = string.IsNullOrWhiteSpace(status) || status.Equals("ALL", StringComparison.OrdinalIgnoreCase)
                ? KnownStatuses
                : [status.ToUpperInvariant()];

            var reports = new List<ReportDetailsDto>();
            foreach (var currentStatus in statuses)
            {
                reports.AddRange(await _reportService.GetReportsByStatusAsync(currentStatus, cancellationToken));
            }

            var query = reports.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(targetType))
            {
                query = query.Where(report => report.TargetType.Equals(targetType, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(report =>
                    report.ReportId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    report.Reason.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            query = sort.Equals("type", StringComparison.OrdinalIgnoreCase)
                ? query.OrderBy(report => report.TargetType).ThenByDescending(report => report.ReportedAt)
                : query.OrderByDescending(report => report.ReportedAt);

            var filtered = query.ToList();
            var paged = filtered
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToList();

            await EnrichReportsAsync(paged, cancellationToken);

            return Ok(new PagedResult<ReportDetailsDto>
            {
                Items = paged,
                TotalCount = filtered.Count,
                Page = normalizedPage,
                PageSize = normalizedPageSize
            });
        }

        /// <summary>
        /// Retrieves the details of a specific moderation report.
        /// </summary>
        /// <param name="reportId">
        /// The identifier of the report to retrieve.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <returns>
        /// The requested report details.
        /// </returns>
        /// <response code="200">
        /// Report found successfully.
        /// </response>
        /// <response code="404">
        /// The specified report does not exist.
        /// </response>
        [HttpGet("{reportId}")]
        [ProducesResponseType(typeof(ReportDetailsDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetById(string reportId, CancellationToken cancellationToken)
        {
            foreach (var status in KnownStatuses)
            {
                var report = (await _reportService.GetReportsByStatusAsync(status, cancellationToken))
                    .FirstOrDefault(item => item.ReportId == reportId);

                if (report != null)
                {
                    await EnrichReportsAsync([report], cancellationToken);
                    return Ok(report);
                }
            }

            throw new SpectrumNotFoundException(Constants.ErrorMessages.ResourceNotFound);
        }

        /// <summary>
        /// Updates the moderation status of a report.
        /// </summary>
        /// <param name="reportId">
        /// The identifier of the report to update.
        /// </param>
        /// <param name="dto">
        /// The new moderation status and resolution information.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <returns>
        /// A confirmation message indicating the report was updated.
        /// </returns>
        /// <response code="200">
        /// Report status updated successfully.
        /// </response>
        /// <response code="401">
        /// The current administrator identity is invalid.
        /// </response>
        [HttpPatch("{reportId}/status")]
        public async Task<IActionResult> Resolve(
            string reportId,
            [FromBody] UpdateReportStatusDto dto,
            CancellationToken cancellationToken
        )
        {
            var moderatorIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(moderatorIdStr, out var moderatorId))
            {
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.Unauthorized);
            }

            await _reportService.UpdateReportStatusAsync(reportId, moderatorId, dto, cancellationToken);
            return Ok(new { Message = "Report status updated." });
        }

        /// <summary>
        /// Deletes the content associated with a report and automatically
        /// marks the report as resolved.
        /// </summary>
        /// <param name="reportId">
        /// The identifier of the report being processed.
        /// </param>
        /// <param name="dto">
        /// Resolution details including the moderation reason.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <returns>
        /// A confirmation message indicating the content was deleted.
        /// </returns>
        /// <response code="200">
        /// Reported content deleted successfully.
        /// </response>
        /// <response code="400">
        /// The report target type is not supported or a deletion reason was not provided.
        /// </response>
        [HttpPost("{reportId}/delete-content")]
        public async Task<IActionResult> DeleteReportedContent(
            string reportId,
            [FromBody] UpdateReportStatusDto dto,
            CancellationToken cancellationToken)
        {
            var moderatorId = GetCurrentAdminId();
            var report = await FindReportAsync(reportId, cancellationToken);
            var reason = ResolveRequiredReason(dto);

            if (report.TargetType.Equals("REVIEW", StringComparison.OrdinalIgnoreCase))
            {
                if (!Guid.TryParse(report.TargetId, out var reviewId))
                {
                    throw new SpectrumBusinessException(Constants.ErrorMessages.InvalidParameterFormat);
                }

                await _reviewService.DeleteAsync(reviewId, moderatorId, isAdmin: true, deletionReason: reason, cancellationToken);
            }
            else
            {
                throw new SpectrumBusinessException("reportTargetTypeNotSupported");
            }

            await _reportService.UpdateReportStatusAsync(reportId, moderatorId, new UpdateReportStatusDto
            {
                NewStatus = "RESOLVED",
                Status = "RESOLVED",
                ResolutionNotes = reason,
                AdminNotes = reason
            }, cancellationToken);

            return Ok(new { Message = "Reported content deleted." });
        }

        /// <summary>
        /// Suspends the author associated with the reported content.
        /// </summary>
        /// <param name="reportId">
        /// The identifier of the report being processed.
        /// </param>
        /// <param name="dto">
        /// Resolution details and moderation notes.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <returns>
        /// A confirmation message indicating the author was suspended.
        /// </returns>
        /// <response code="200">
        /// Author suspended successfully.
        /// </response>
        /// <response code="400">
        /// The reported user could not be resolved.
        /// </response>
        [HttpPost("{reportId}/suspend-author")]
        public async Task<IActionResult> SuspendReportedAuthor(
            string reportId,
            [FromBody] UpdateReportStatusDto dto,
            CancellationToken cancellationToken)
        {
            var moderatorId = GetCurrentAdminId();
            var report = await FindReportAsync(reportId, cancellationToken);
            var targetUserId = await ResolveReportTargetUserIdAsync(report, cancellationToken);
            var reason = string.IsNullOrWhiteSpace(dto.AdminNotes) ? dto.ResolutionNotes : dto.AdminNotes;

            await _userModerationService.ToggleSuspensionAsync(
                targetUserId,
                suspend: true,
                requesterId: moderatorId,
                reason: reason,
                cancellationToken: cancellationToken);
            return Ok(new { Message = "Reported author suspended." });
        }

        /// <summary>
        /// Enriches report data with usernames, normalized identifiers,
        /// timestamps, and content previews for presentation purposes.
        /// </summary>
        private async Task EnrichReportsAsync(List<ReportDetailsDto> reports, CancellationToken cancellationToken)
        {
            if (reports.Count == 0)
            {
                return;
            }

            var reporterIds = reports
                .Select(report => TryParseGuid(report.ReporterId))
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToArray();
            var reporters = await _context.Users
                .AsNoTracking()
                .Where(user => reporterIds.Contains(user.Id))
                .ToDictionaryAsync(user => user.Id, user => user.Username, cancellationToken);

            foreach (var report in reports)
            {
                report.Id = report.ReportId;
                report.CreatedAt = report.ReportedAt;
                var reporterId = TryParseGuid(report.ReporterId);
                report.ReporterUsername = reporterId.HasValue
                    ? reporters.GetValueOrDefault(reporterId.Value, "Usuario Spectrum")
                    : "Usuario Spectrum";
                report.TargetContentSnippet = await ResolveTargetSnippetAsync(report, cancellationToken);
            }
        }

        /// <summary>
        /// Searches for a report across all known moderation statuses.
        /// </summary>
        /// <param name="report">
        /// Identifier of the report to locate.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <returns>
        /// The matching report.
        /// </returns>
        /// <exception cref="SpectrumNotFoundException">
        /// Thrown when the report cannot be found.
        /// </exception>
        private async Task<string> ResolveTargetSnippetAsync(ReportDetailsDto report, CancellationToken cancellationToken)
        {
            var targetId = TryParseGuid(report.TargetId);
            if (!targetId.HasValue)
            {
                return "Contenido alojado en el microservicio social.";
            }

            return report.TargetType.ToUpperInvariant() switch
            {
                "REVIEW" => await _context.Reviews
                    .AsNoTracking()
                    .Where(review => review.Id == targetId.Value)
                    .Select(review => review.Title + ": " + review.Content)
                    .FirstOrDefaultAsync(cancellationToken) ?? string.Empty,
                "USER" => await _context.Users
                    .AsNoTracking()
                    .Where(user => user.Id == targetId.Value)
                    .Select(user => user.Username + " - " + user.Email)
                    .FirstOrDefaultAsync(cancellationToken) ?? string.Empty,
                "GAME_CLIP" => await _context.GameClips
                    .AsNoTracking()
                    .Where(clip => clip.Id == targetId.Value)
                    .Select(clip => clip.Title)
                    .FirstOrDefaultAsync(cancellationToken) ?? string.Empty,
                _ => "Contenido alojado en el microservicio social."
            };
        }

        private static Guid? TryParseGuid(string value)
        {
            return Guid.TryParse(value, out var parsed) ? parsed : null;
        }

        private async Task<ReportDetailsDto> FindReportAsync(string reportId, CancellationToken cancellationToken)
        {
            foreach (var status in KnownStatuses)
            {
                var report = (await _reportService.GetReportsByStatusAsync(status, cancellationToken))
                    .FirstOrDefault(item => item.ReportId == reportId);

                if (report != null)
                {
                    return report;
                }
            }

            throw new SpectrumNotFoundException(Constants.ErrorMessages.ResourceNotFound);
        }

        /// <summary>
        /// Resolves the user associated with a reported entity.
        /// </summary>
        /// <param name="report">
        /// Report being processed.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <returns>
        /// The identifier of the user associated with the report target.
        /// </returns>
        /// <exception cref="SpectrumBusinessException">
        /// Thrown when the target user cannot be resolved.
        /// </exception>
        private async Task<Guid> ResolveReportTargetUserIdAsync(ReportDetailsDto report, CancellationToken cancellationToken)
        {
            if (report.TargetType.Equals("USER", StringComparison.OrdinalIgnoreCase) &&
                Guid.TryParse(report.TargetId, out var reportedUserId))
            {
                return reportedUserId;
            }

            if (report.TargetType.Equals("REVIEW", StringComparison.OrdinalIgnoreCase) &&
                Guid.TryParse(report.TargetId, out var reviewId))
            {
                var userId = await _context.Reviews
                    .IgnoreQueryFilters()
                    .Where(review => review.Id == reviewId)
                    .Select(review => review.UserId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (userId != Guid.Empty)
                {
                    return userId;
                }
            }

            throw new SpectrumBusinessException(Constants.ErrorMessages.InvalidParameterFormat);
        }

        private Guid GetCurrentAdminId()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (!Guid.TryParse(userIdStr, out var moderatorId))
            {
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.Unauthorized);
            }

            return moderatorId;
        }

        /// <summary>
        /// Resolves and validates the moderation reason required for
        /// destructive administrative actions.
        /// </summary>
        /// <param name="dto">
        /// Resolution payload submitted by the administrator.
        /// </param>
        /// <returns>
        /// The validated moderation reason.
        /// </returns>
        /// <exception cref="SpectrumBusinessException">
        /// Thrown when no valid reason is provided.
        /// </exception>
        private static string ResolveRequiredReason(UpdateReportStatusDto dto)
        {
            var reason = string.IsNullOrWhiteSpace(dto.ResolutionNotes) ? dto.AdminNotes : dto.ResolutionNotes;
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new SpectrumBusinessException("deleteReasonRequired");
            }

            return reason.Trim();
        }
    }
}
