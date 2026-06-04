using Grpc.Core;
using Spectrum.API.Dtos.Drops;
using Spectrum.API.Exceptions;
using Spectrum.API.Grpc.Drops;
using Spectrum.API.Repositories;
using Spectrum.API.Utilities;

namespace Spectrum.API.Services.Drops
{
    public interface IDropsService
    {
        Task<DropActionResultDto> CreateEventAsync(CreateDropEventDto dto, Guid adminId, CancellationToken cancellationToken);
        Task<DropActionResultDto> UpdateEventAsync(string eventId, UpdateDropEventDto dto, CancellationToken cancellationToken);
        Task<DropActionResultDto> PublishEventAsync(string eventId, bool publishNow, CancellationToken cancellationToken);
        Task<DropActionResultDto> FinishEventAsync(string eventId, bool cancelIfWithoutWinner, CancellationToken cancellationToken);
        Task<DropActionResultDto> JoinEventAsync(Guid userId, string eventId, CancellationToken cancellationToken);
        Task<ClaimDropResultDto> ClaimAccessKeyAsync(Guid userId, string eventId, ClaimDropDto dto, CancellationToken cancellationToken);
        Task<EventStatusDto> GetEventStatusAsync(string eventId, bool exposeChallengeCode, CancellationToken cancellationToken, Guid? currentUserId = null);
        Task<PagedResult<EventStatusDto>> ListEventsAsync(string scope, int page, int pageSize, bool includeDrafts, bool exposeChallengeCode, CancellationToken cancellationToken, Guid? currentUserId = null);
        Task<DropActionResultDto> SendRewardAsync(Guid adminId, string eventId, SendRewardDto dto, CancellationToken cancellationToken);
        Task<IEnumerable<WonKeyDto>> GetUserWonKeysAsync(Guid userId, CancellationToken cancellationToken);
    }

    public class DropsService : IDropsService
    {
        private const int MaximumRewardLength = InputValidationLimits.DropRewardCode;

        private readonly DropService.DropServiceClient _dropServiceClient;
        private readonly IUserRepository _userRepository;
        private readonly IRewardDeliveryService _rewardDeliveryService;
        private readonly ILogger<DropsService> _logger;

        public DropsService(
            DropService.DropServiceClient dropServiceClient,
            IUserRepository userRepository,
            IRewardDeliveryService rewardDeliveryService,
            ILogger<DropsService> logger
        )
        {
            _dropServiceClient = dropServiceClient;
            _userRepository = userRepository;
            _rewardDeliveryService = rewardDeliveryService;
            _logger = logger;
        }

        public async Task<DropActionResultDto> CreateEventAsync(CreateDropEventDto dto, Guid adminId, CancellationToken cancellationToken)
        {
            ValidateEvent(dto.Title, dto.GameTitle, dto.Platform, dto.StartAt, dto.JoinDeadlineAt, dto.RevealAt, dto.EndAt, dto.TotalSlots);
            var rewardCodes = ValidateRewardCodes(dto.AccessKeys);

            var request = new CreateEventRequest
            {
                Title = dto.Title.Trim(),
                Description = dto.Description.Trim(),
                ImageUrl = dto.ImageUrl.Trim(),
                GameTitle = dto.GameTitle.Trim(),
                RawgGameId = dto.RawgGameId ?? 0,
                Platform = dto.Platform.Trim(),
                StartAt = ToUnixMilliseconds(dto.StartAt),
                JoinDeadlineAt = ToUnixMilliseconds(dto.JoinDeadlineAt),
                RevealAt = ToUnixMilliseconds(dto.RevealAt),
                EndAt = ToUnixMilliseconds(dto.EndAt),
                TotalSlots = dto.TotalSlots,
                PublicChallengeCode = string.Empty,
                CreatedByAdminId = adminId.ToString(),
                PublishNow = dto.PublishNow
            };
            request.AccessKeys.AddRange(rewardCodes);

            var response = await _dropServiceClient.CreateEventAsync(request, cancellationToken: cancellationToken);
            return EnsureActionSuccess(response);
        }

        public async Task<DropActionResultDto> UpdateEventAsync(string eventId, UpdateDropEventDto dto, CancellationToken cancellationToken)
        {
            var current = await GetEventStatusAsync(eventId, exposeChallengeCode: false, cancellationToken);
            if (!CanEditEvent(current))
            {
                throw new SpectrumBusinessException("dropEventNotEditable");
            }

            ValidateEvent(dto.Title, dto.GameTitle, dto.Platform, dto.StartAt, dto.JoinDeadlineAt, dto.RevealAt, dto.EndAt, dto.TotalSlots);
            var rewardCodes = dto.AccessKeys.Count == 0 ? new List<string>() : ValidateRewardCodes(dto.AccessKeys);

            var request = new UpdateEventRequest
            {
                EventId = eventId,
                Title = dto.Title.Trim(),
                Description = dto.Description.Trim(),
                ImageUrl = dto.ImageUrl.Trim(),
                GameTitle = dto.GameTitle.Trim(),
                RawgGameId = dto.RawgGameId ?? 0,
                Platform = dto.Platform.Trim(),
                StartAt = ToUnixMilliseconds(dto.StartAt),
                JoinDeadlineAt = ToUnixMilliseconds(dto.JoinDeadlineAt),
                RevealAt = ToUnixMilliseconds(dto.RevealAt),
                EndAt = ToUnixMilliseconds(dto.EndAt),
                TotalSlots = dto.TotalSlots,
                PublicChallengeCode = string.Empty,
                Status = dto.Status.Trim()
            };
            request.AccessKeys.AddRange(rewardCodes);

            var response = await _dropServiceClient.UpdateEventAsync(request, cancellationToken: cancellationToken);

            return EnsureActionSuccess(response);
        }

        public async Task<DropActionResultDto> PublishEventAsync(string eventId, bool publishNow, CancellationToken cancellationToken)
        {
            var response = await _dropServiceClient.PublishEventAsync(new PublishEventRequest
            {
                EventId = eventId,
                PublishNow = publishNow
            }, cancellationToken: cancellationToken);

            return EnsureActionSuccess(response);
        }

        public async Task<DropActionResultDto> FinishEventAsync(string eventId, bool cancelIfWithoutWinner, CancellationToken cancellationToken)
        {
            var response = await _dropServiceClient.FinishEventAsync(new FinishEventRequest
            {
                EventId = eventId,
                CancelIfWithoutWinner = cancelIfWithoutWinner
            }, cancellationToken: cancellationToken);

            return EnsureActionSuccess(response);
        }

        public async Task<DropActionResultDto> JoinEventAsync(Guid userId, string eventId, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _dropServiceClient.JoinEventAsync(new JoinEventRequest
                {
                    EventId = eventId,
                    UserId = userId.ToString()
                }, cancellationToken: cancellationToken);

                return EnsureActionSuccess(response);
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error calling gRPC JoinEvent for event {EventId}", eventId);
                throw new SpectrumServiceUnavailableException(Constants.ErrorMessages.RpcServiceUnavailable);
            }
        }

        public async Task<ClaimDropResultDto> ClaimAccessKeyAsync(Guid userId, string eventId, ClaimDropDto dto, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetUserByIdAsync(userId)
                ?? throw new SpectrumNotFoundException(Constants.ErrorMessages.UserNotFound);

            try
            {
                var response = await _dropServiceClient.ClaimAccessKeyAsync(new ClaimKeyRequest
                {
                    UserId = userId.ToString(),
                    EventId = eventId,
                    ChallengeCode = string.Empty,
                    Username = user.Username
                }, cancellationToken: cancellationToken);

                if (response.Success && !string.IsNullOrWhiteSpace(response.AccessKeyCode))
                {
                    await DeliverClaimedRewardAsync(user.Email, userId, eventId, response.AccessKeyCode, cancellationToken);
                }

                return new ClaimDropResultDto
                {
                    Success = response.Success,
                    EventId = eventId,
                    WinnerUserId = string.IsNullOrWhiteSpace(response.WinnerUserId) ? null : response.WinnerUserId,
                    WinnerUsername = string.IsNullOrWhiteSpace(response.WinnerUsername) ? null : response.WinnerUsername,
                    ClaimedAt = response.ClaimedAt <= 0 ? null : FromUnixMilliseconds(response.ClaimedAt),
                    Message = response.Message
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error calling gRPC ClaimAccessKey for event {EventId}", eventId);
                throw new SpectrumServiceUnavailableException(Constants.ErrorMessages.RpcServiceUnavailable);
            }
        }

        public async Task<EventStatusDto> GetEventStatusAsync(
            string eventId,
            bool exposeChallengeCode,
            CancellationToken cancellationToken,
            Guid? currentUserId = null
        )
        {
            try
            {
                var response = await _dropServiceClient.GetEventStatusAsync(new GetEventRequest
                {
                    EventId = eventId,
                    RequesterUserId = currentUserId?.ToString() ?? string.Empty
                }, cancellationToken: cancellationToken);

                if (response.Status == "NOT_FOUND")
                {
                    throw new SpectrumNotFoundException(Constants.ErrorMessages.ResourceNotFound);
                }

                return MapEvent(response, exposeChallengeCode);
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error calling gRPC GetEventStatus for event {EventId}", eventId);
                throw new SpectrumServiceUnavailableException(Constants.ErrorMessages.RpcServiceUnavailable);
            }
        }

        public async Task<PagedResult<EventStatusDto>> ListEventsAsync(
            string scope,
            int page,
            int pageSize,
            bool includeDrafts,
            bool exposeChallengeCode,
            CancellationToken cancellationToken,
            Guid? currentUserId = null
        )
        {
            var normalizedPage = Math.Max(1, page);
            var normalizedPageSize = Math.Clamp(pageSize, 1, 50);

            try
            {
                var response = await _dropServiceClient.ListEventsAsync(new ListEventsRequest
                {
                    Scope = scope.ToUpperInvariant(),
                    Page = normalizedPage,
                    PageSize = normalizedPageSize,
                    IncludeDrafts = includeDrafts,
                    RequesterUserId = currentUserId?.ToString() ?? string.Empty
                }, cancellationToken: cancellationToken);

                return new PagedResult<EventStatusDto>
                {
                    Items = response.Events.Select(item => MapEvent(item, exposeChallengeCode)).ToList(),
                    TotalCount = response.TotalCount,
                    Page = response.Page,
                    PageSize = response.PageSize
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error calling gRPC ListEvents for scope {Scope}", scope);
                throw new SpectrumServiceUnavailableException(Constants.ErrorMessages.RpcServiceUnavailable);
            }
        }

        public async Task<DropActionResultDto> SendRewardAsync(Guid adminId, string eventId, SendRewardDto dto, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(dto.RewardCode) || dto.RewardCode.Length > MaximumRewardLength)
            {
                throw new SpectrumBusinessException("rewardCodeInvalid");
            }

            var eventStatus = await GetEventStatusAsync(eventId, exposeChallengeCode: true, cancellationToken);
            if (!string.Equals(eventStatus.Status, "FINISHED", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(eventStatus.WinnerUserId))
            {
                throw new SpectrumBusinessException("rewardRequiresFinishedWinner");
            }

            if (string.Equals(eventStatus.RewardDeliveryStatus, "SENT", StringComparison.OrdinalIgnoreCase))
            {
                throw new SpectrumBusinessException("rewardAlreadySent");
            }

            if (!Guid.TryParse(eventStatus.WinnerUserId, out var winnerId))
            {
                throw new SpectrumBusinessException("winnerIdInvalid");
            }

            var winner = await _userRepository.GetUserByIdAsync(winnerId)
                ?? throw new SpectrumNotFoundException(Constants.ErrorMessages.UserNotFound);

            await _rewardDeliveryService.SendRewardAsync(
                winner.Email,
                eventStatus.Title,
                dto.RewardCode,
                cancellationToken
            );

            var sentAt = DateTime.UtcNow;
            var response = await _dropServiceClient.MarkRewardSentAsync(new MarkRewardSentRequest
            {
                EventId = eventId,
                RewardSentAt = ToUnixMilliseconds(sentAt)
            }, cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Admin {AdminId} marked reward as sent for event {EventId} and winner {WinnerId}.",
                adminId,
                eventId,
                winnerId
            );

            return EnsureActionSuccess(response);
        }

        public async Task<IEnumerable<WonKeyDto>> GetUserWonKeysAsync(Guid userId, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _dropServiceClient.GetWonKeysAsync(new WonKeysRequest
                {
                    UserId = userId.ToString()
                }, cancellationToken: cancellationToken);

                return response.WonKeys.Select(k => new WonKeyDto
                {
                    EventId = k.EventId,
                    GameTitle = k.GameTitle,
                    AccessKeyCode = string.Empty,
                    ClaimedAt = k.ClaimedAt <= 0 ? DateTime.MinValue : FromUnixMilliseconds(k.ClaimedAt),
                    RewardDeliveryStatus = k.RewardDeliveryStatus
                });
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error calling gRPC GetWonKeys for user {UserId}", userId);
                return Enumerable.Empty<WonKeyDto>();
            }
        }

        private async Task DeliverClaimedRewardAsync(
            string recipientEmail,
            Guid winnerUserId,
            string eventId,
            string rewardCode,
            CancellationToken cancellationToken
        )
        {
            try
            {
                var eventStatus = await GetEventStatusAsync(eventId, exposeChallengeCode: false, cancellationToken);
                await _rewardDeliveryService.SendRewardAsync(
                    recipientEmail,
                    $"{eventStatus.GameTitle} - {eventStatus.Platform}",
                    rewardCode,
                    cancellationToken
                );
                await _dropServiceClient.MarkRewardSentAsync(new MarkRewardSentRequest
                {
                    EventId = eventId,
                    WinnerUserId = winnerUserId.ToString(),
                    RewardSentAt = ToUnixMilliseconds(DateTime.UtcNow)
                }, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Reward delivery email failed after claim for event {EventId}. Code was not logged.",
                    eventId
                );
            }
        }

        private static EventStatusDto MapEvent(EventStatusResponse response, bool exposeChallengeCode)
        {
            var revealAt = FromUnixMilliseconds(response.RevealAt);

            return new EventStatusDto
            {
                EventId = response.EventId,
                Title = response.Title,
                Description = response.Description,
                ImageUrl = response.ImageUrl,
                GameTitle = response.GameTitle,
                RawgGameId = response.RawgGameId <= 0 ? null : response.RawgGameId,
                Platform = response.Platform,
                StartAt = FromUnixMilliseconds(response.StartAt),
                JoinDeadlineAt = FromUnixMilliseconds(response.JoinDeadlineAt),
                RevealAt = revealAt,
                EndAt = FromUnixMilliseconds(response.EndDate),
                TotalSlots = response.TotalSlots,
                AvailableSlots = response.AvailableSlots,
                Status = response.Status,
                PublicChallengeCode = string.Empty,
                CurrentUserJoined = response.CurrentUserJoined,
                CanJoin = response.CanJoin,
                CanClaim = response.CanClaim,
                HasClaimed = response.HasClaimed,
                RemainingSlots = response.RemainingSlots > 0 ? response.RemainingSlots : response.AvailableSlots,
                VisibleUntil = response.VisibleUntil <= 0 ? null : FromUnixMilliseconds(response.VisibleUntil),
                CreatedByAdminId = response.CreatedByAdminId,
                WinnerUserId = string.IsNullOrWhiteSpace(response.WinnerUserId) ? null : response.WinnerUserId,
                WinnerUsername = string.IsNullOrWhiteSpace(response.WinnerUsername) ? null : response.WinnerUsername,
                FinishedAt = response.FinishedAt <= 0 ? null : FromUnixMilliseconds(response.FinishedAt),
                RewardSentAt = response.RewardSentAt <= 0 ? null : FromUnixMilliseconds(response.RewardSentAt),
                RewardDeliveryStatus = string.IsNullOrWhiteSpace(response.RewardDeliveryStatus) ? "PENDING" : response.RewardDeliveryStatus,
                ParticipantsCount = response.ParticipantsCount,
                RewardCodesAvailable = response.RewardCodesAvailable > 0 ? response.RewardCodesAvailable : response.KeysAvailable,
                RewardCodesTotal = response.RewardCodesTotal > 0 ? response.RewardCodesTotal : response.KeysTotal,
                Winners = response.Winners.Select(winner => new DropWinnerDto
                {
                    UserId = winner.UserId,
                    Username = winner.Username,
                    ClaimedAt = winner.ClaimedAt <= 0 ? null : FromUnixMilliseconds(winner.ClaimedAt),
                    DeliveryStatus = string.IsNullOrWhiteSpace(winner.DeliveryStatus) ? "PENDING" : winner.DeliveryStatus
                }).ToList()
            };
        }

        private static DropActionResultDto EnsureActionSuccess(EventActionResponse response)
        {
            if (!response.Success)
            {
                throw new SpectrumBusinessException(response.Message);
            }

            return new DropActionResultDto
            {
                Success = response.Success,
                EventId = response.EventId,
                Message = response.Message
            };
        }

        private static bool CanEditEvent(EventStatusDto eventStatus)
        {
            var editableStatus = eventStatus.Status.Equals("UPCOMING", StringComparison.OrdinalIgnoreCase) ||
                                 eventStatus.Status.Equals("SCHEDULED", StringComparison.OrdinalIgnoreCase) ||
                                 eventStatus.Status.Equals("DRAFT", StringComparison.OrdinalIgnoreCase);

            return editableStatus &&
                   eventStatus.Winners.Count == 0 &&
                   DateTime.UtcNow < eventStatus.StartAt.ToUniversalTime().AddMinutes(-10);
        }

        private static void ValidateEvent(
            string title,
            string gameTitle,
            string platform,
            DateTime startAt,
            DateTime joinDeadlineAt,
            DateTime revealAt,
            DateTime endAt,
            int totalSlots
        )
        {
            if (string.IsNullOrWhiteSpace(title) ||
                string.IsNullOrWhiteSpace(gameTitle) ||
                string.IsNullOrWhiteSpace(platform))
            {
                throw new SpectrumBusinessException(Constants.ErrorMessages.MissingRequiredParameter);
            }

            if (totalSlots <= 0)
            {
                throw new SpectrumBusinessException("totalSlotsInvalid");
            }

            if (!(startAt < joinDeadlineAt && joinDeadlineAt <= revealAt && revealAt < endAt))
            {
                throw new SpectrumBusinessException("eventDatesInvalid");
            }
        }

        private static List<string> ValidateRewardCodes(IEnumerable<string> rewardCodes)
        {
            var normalizedCodes = rewardCodes
                .Select(code => code?.Trim() ?? string.Empty)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .ToList();

            if (normalizedCodes.Count == 0)
            {
                throw new SpectrumBusinessException("rewardCodesRequired");
            }

            if (normalizedCodes.Any(code => code.Length > MaximumRewardLength))
            {
                throw new SpectrumBusinessException("rewardCodeInvalid");
            }

            if (normalizedCodes.Count != normalizedCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            {
                throw new SpectrumBusinessException("rewardCodesDuplicated");
            }

            return normalizedCodes;
        }

        private static long ToUnixMilliseconds(DateTime value)
        {
            return new DateTimeOffset(value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
                : value.ToUniversalTime()).ToUnixTimeMilliseconds();
        }

        private static DateTime FromUnixMilliseconds(long value)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(value).UtcDateTime;
        }
    }
}
