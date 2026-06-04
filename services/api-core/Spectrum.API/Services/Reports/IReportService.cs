using Grpc.Core;
using Spectrum.API.Dtos.Reports;
using Spectrum.API.Exceptions;
using Spectrum.API.Grpc.Social;
using Spectrum.API.Repositories;
using Spectrum.API.Services.Email;
using Spectrum.API.Utilities;

namespace Spectrum.API.Services.Reports
{
    public interface IReportService
    {
        Task SubmitReportAsync(Guid reporterId, CreateReportDto dto, CancellationToken cancellationToken = default);
        Task<IEnumerable<ReportDetailsDto>> GetReportsByStatusAsync(string status, CancellationToken cancellationToken = default);
        Task UpdateReportStatusAsync(string reportId, Guid moderatorId, UpdateReportStatusDto dto, CancellationToken cancellationToken = default);
    }

    public class ReportsService : IReportService
    {
        private readonly ReportService.ReportServiceClient  _reportServiceClient;
        private readonly ILogger<ReportsService> _logger;
        private readonly IUserRepository? _userRepository;
        private readonly IEmailService? _emailService;

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

        private static string ResolveStatus(UpdateReportStatusDto dto)
        {
            var status = string.IsNullOrWhiteSpace(dto.NewStatus) ? dto.Status : dto.NewStatus;
            if (status is not ("RESOLVED" or "DISMISSED"))
            {
                throw new SpectrumBusinessException("reportStatusInvalid");
            }

            return status;
        }

        private static string ResolveNotes(UpdateReportStatusDto dto)
        {
            return string.IsNullOrWhiteSpace(dto.ResolutionNotes)
                ? dto.AdminNotes ?? string.Empty
                : dto.ResolutionNotes;
        }

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
