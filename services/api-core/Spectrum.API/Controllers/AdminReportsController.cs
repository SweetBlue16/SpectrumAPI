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
