using Grpc.Core;
using Spectrum.API.Dtos.Reports;
using Spectrum.API.Exceptions;
using Spectrum.API.Grpc.Social;
using Spectrum.API.Repositories;
using Spectrum.API.Services.Email;
using Spectrum.API.Utilities;

namespace Spectrum.API.Services.Reports
{
    /// <summary>
    /// Defines operations for creating, retrieving, and moderating user reports.
    /// </summary>
    public interface IReportService
    {
        /// <summary>
        /// Submits a new report against a target entity.
        /// </summary>
        /// <param name="reporterId">Identifier of the user submitting the report.</param>
        /// <param name="dto">Report creation payload.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        Task SubmitReportAsync(Guid reporterId, CreateReportDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all reports that match the specified status.
        /// </summary>
        /// <param name="status">Report status to filter by.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>A collection of reports matching the requested status.</returns>
        Task<IEnumerable<ReportDetailsDto>> GetReportsByStatusAsync(string status, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the status and moderation notes of a report.
        /// </summary>
        /// <param name="reportId">Identifier of the report to update.</param>
        /// <param name="moderatorId">Identifier of the moderator performing the action.</param>
        /// <param name="dto">Status update payload.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        Task UpdateReportStatusAsync(string reportId, Guid moderatorId, UpdateReportStatusDto dto, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Provides report management functionality through the Social microservice using gRPC.
    /// </summary>
    public class ReportsService : IReportService
    {
        private readonly ReportService.ReportServiceClient  _reportServiceClient;
        private readonly ILogger<ReportsService> _logger;
        private readonly IUserRepository? _userRepository;
        private readonly IEmailService? _emailService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReportsService"/> class.
        /// </summary>
        /// <param name="reportServiceClient">gRPC client used to communicate with the Social report service.</param>
        /// <param name="logger">Logger used for diagnostics and error reporting.</param>
        /// <param name="userRepository">Repository used to resolve affected users.</param>
        /// <param name="emailService">Service used to send moderation notifications.</param>
        public ReportsService(
            ReportService.ReportServiceClient reportServiceClient,
            ILogger<ReportsService> logger,
            IUserRepository? userRepository = null,
            IEmailService? emailService = null
        )
        {
            _reportServiceClient = reportServiceClient;
            _logger = logger;
            _userRepository = userRepository;
            _emailService = emailService;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ReportDetailsDto>> GetReportsByStatusAsync(string status, CancellationToken cancellationToken = default)
        {
            var reports = new List<ReportDetailsDto>();
            try
            {
                var request = new ListReportsRequest { Status = status };
                using var call = _reportServiceClient.ListReportsByStatus(request, cancellationToken: cancellationToken);

                await foreach (var response in call.ResponseStream.ReadAllAsync(cancellationToken))
                {
                    reports.Add(new ReportDetailsDto
                    {
                        Id = response.ReportId,
                        ReportId = response.ReportId,
                        ReporterId = response.ReporterId,
                        TargetId = response.TargetId,
                        TargetType = response.TargetType,
                        Reason = response.Reason,
                        Status = response.Status,
                        ReportedAt = DateTimeOffset.FromUnixTimeMilliseconds(response.ReportedAt).UtcDateTime,
                        CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(response.ReportedAt).UtcDateTime,
                        TargetContentSnippet = response.Description,
                        AdminNotes = response.ResolutionNotes,
                        ResolvedAt = response.ResolvedAt <= 0
                            ? null
                            : DateTimeOffset.FromUnixTimeMilliseconds(response.ResolvedAt).UtcDateTime
                    });
                }
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC streaming failed for reports.");
                throw new SpectrumServiceUnavailableException(Constants.ErrorMessages.RpcServiceUnavailable);
            }
            return reports;
        }

        /// <inheritdoc />
        public async Task SubmitReportAsync(Guid reporterId, CreateReportDto dto, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new SubmitReportRequest
                {
                    ReporterId = reporterId.ToString(),
                    TargetId = dto.TargetId.ToString(),
                    TargetType = dto.TargetType,
                    Reason = dto.Reason,
                    Description = dto.Description ?? string.Empty
                };

                var response = await _reportServiceClient.SubmitReportAsync(request, cancellationToken: cancellationToken);
                if (!response.Success)
                {
                    throw new SpectrumBusinessException(response.Message);
                }
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Failed to connect to Social gRPC service.");
                throw new SpectrumServiceUnavailableException(Constants.ErrorMessages.RpcServiceUnavailable);
            }
        }

        /// <inheritdoc />
        public async Task UpdateReportStatusAsync(string reportId, Guid moderatorId, UpdateReportStatusDto dto, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new UpdateReportStatusRequest
                {
                    ReportId = reportId,
                    ModeratorId = moderatorId.ToString(),
                    NewStatus = ResolveStatus(dto),
                    ResolutionNotes = ResolveNotes(dto)
                };

                var response = await _reportServiceClient.UpdateReportStatusAsync(request, cancellationToken: cancellationToken);

                if (!response.Success)
                {
                    if (response.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                        throw new SpectrumNotFoundException(response.Message);

                    throw new SpectrumBusinessException(response.Message);
                }

                await TrySendReportActionEmailAsync(reportId, dto, cancellationToken);
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Failed to connect to Social gRPC service.");
                throw new SpectrumServiceUnavailableException(Constants.ErrorMessages.RpcServiceUnavailable);
            }
        }

        /// <summary>
        /// Resolves the effective status value from the update payload and validates it.
        /// </summary>
        /// <param name="dto">Status update payload.</param>
        /// <returns>A valid moderation status.</returns>
        /// <exception cref="SpectrumBusinessException">
        /// Thrown when the provided status is not supported.
        /// </exception>
        private static string ResolveStatus(UpdateReportStatusDto dto)
        {
            var status = string.IsNullOrWhiteSpace(dto.NewStatus) ? dto.Status : dto.NewStatus;
            if (status is not ("RESOLVED" or "DISMISSED"))
            {
                throw new SpectrumBusinessException("reportStatusInvalid");
            }

            return status;
        }

        /// <summary>
        /// Resolves the moderation notes associated with a report action.
        /// </summary>
        /// <param name="dto">Status update payload.</param>
        /// <returns>The moderation notes to persist.</returns>
        private static string ResolveNotes(UpdateReportStatusDto dto)
        {
            return string.IsNullOrWhiteSpace(dto.ResolutionNotes)
                ? dto.AdminNotes ?? string.Empty
                : dto.ResolutionNotes;
        }

        /// <summary>
        /// Attempts to notify the affected user when a report targeting their account
        /// has been reviewed by a moderator.
        /// </summary>
        /// <param name="reportId">Identifier of the processed report.</param>
        /// <param name="dto">Status update payload.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        private async Task TrySendReportActionEmailAsync(string reportId, UpdateReportStatusDto dto, CancellationToken cancellationToken)
        {
            if (_userRepository is null || _emailService is null)
            {
                return;
            }

            try
            {
                var report = await ResolveReportAsync(reportId, ResolveStatus(dto), cancellationToken);
                if (report is null ||
                    !string.Equals(report.TargetType, "USER", StringComparison.OrdinalIgnoreCase) ||
                    !Guid.TryParse(report.TargetId, out var targetUserId))
                {
                    return;
                }

                var user = await _userRepository.GetUserByIdAsync(targetUserId);
                if (user is null || string.IsNullOrWhiteSpace(user.Email))
                {
                    return;
                }

                await _emailService.SendReportActionAsync(
                    user.Email,
                    "Un administrador revisó un reporte relacionado con tu cuenta en Spectrum."
                );
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Could not send report action email for report {ReportId}", reportId);
            }
        }

        /// <summary>
        /// Retrieves a specific report from the collection associated with the provided status.
        /// </summary>
        /// <param name="reportId">Identifier of the report to locate.</param>
        /// <param name="status">Status used to query reports.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>The matching report if found; otherwise, <c>null</c>.</returns>
        private async Task<ReportDetailsDto?> ResolveReportAsync(
            string reportId,
            string status,
            CancellationToken cancellationToken
        )
        {
            var reports = await GetReportsByStatusAsync(status, cancellationToken);
            return reports.FirstOrDefault(report => report.ReportId == reportId);
        }
    }
}
