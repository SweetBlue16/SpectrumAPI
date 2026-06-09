using Spectrum.API.Dtos.Profile;
using Spectrum.API.Exceptions;
using Spectrum.API.Models;
using Spectrum.API.Repositories;
using Spectrum.API.Services.Admin;
using Spectrum.API.Services.Email;
using Spectrum.API.Utilities;

namespace Spectrum.API.Services.Profile
{
    /// <summary>
    /// Defines operations for moderating user accounts, including suspension, banning,
    /// and other moderation actions.
    /// </summary>
    public interface IUserModerationService
    {
        /// <summary>
        /// Applies or removes a temporary suspension from a user account.
        /// </summary>
        /// <param name="targetUserId">Identifier of the target user.</param>
        /// <param name="suspend">
        /// Indicates whether the account should be suspended or reactivated.
        /// </param>
        /// <param name="requesterId">
        /// Identifier of the moderator performing the action.
        /// </param>
        /// <param name="reason">
        /// Optional moderation reason.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="SpectrumNotFoundException">
        /// Thrown when the user cannot be found.
        /// </exception>
        /// <exception cref="SpectrumBusinessException">
        /// Thrown when the requested state is already applied.
        /// </exception>
        /// <exception cref="SpectrumForbiddenException">
        /// Thrown when moderation is not allowed for the target account.
        /// </exception>
        Task ToggleSuspensionAsync(Guid targetUserId, bool suspend, Guid? requesterId, string? reason, CancellationToken cancellationToken);

        /// <summary>
        /// Applies or removes a suspension from a user account.
        /// </summary>
        /// <param name="targetUserId">Identifier of the target user.</param>
        /// <param name="suspend">
        /// Indicates whether the account should be suspended or reactivated.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ToggleSuspensionAsync(Guid targetUserId, bool suspend, CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies or removes a permanent ban from a user account.
        /// </summary>
        /// <param name="targetUserId">Identifier of the target user.</param>
        /// <param name="ban">
        /// Indicates whether the account should be banned or unbanned.
        /// </param>
        /// <param name="requesterId">
        /// Identifier of the moderator performing the action.
        /// </param>
        /// <param name="reason">
        /// Optional moderation reason.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="SpectrumNotFoundException">
        /// Thrown when the user cannot be found.
        /// </exception>
        /// <exception cref="SpectrumBusinessException">
        /// Thrown when the requested state is already applied.
        /// </exception>
        /// <exception cref="SpectrumForbiddenException">
        /// Thrown when moderation is not allowed for the target account.
        /// </exception>
        Task ToggleBanAsync(Guid targetUserId, bool ban, Guid? requesterId, string? reason, CancellationToken cancellationToken);

        /// <summary>
        /// Restores a previously moderated account to an active state.
        /// </summary>
        /// <param name="userId">Identifier of the target user.</param>
        /// <param name="requesterId">
        /// Identifier of the moderator performing the action.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="SpectrumNotFoundException">
        /// Thrown when the user cannot be found.
        /// </exception>
        /// <exception cref="SpectrumBusinessException">
        /// Thrown when the account is already active.
        /// </exception>
        /// <exception cref="SpectrumForbiddenException">
        /// Thrown when moderation is not allowed for the target account.
        /// </exception>
        Task ReactivateUserAsync(Guid userId, Guid? requesterId, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves a paginated list of users for moderation purposes.
        /// </summary>
        /// <param name="page">Page number to retrieve.</param>
        /// <param name="pageSize">Number of items per page.</param>
        /// <param name="searchTerm">Optional search filter.</param>
        /// <param name="status">Optional account status filter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// A paginated collection of moderation user records.
        /// </returns>
        Task<PagedResult<UserModerationDto>> GetUsersForModerationAsync(int page, int pageSize, string? searchTerm, string? status, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves a paginated list of users for moderation purposes.
        /// </summary>
        /// <param name="page">Page number to retrieve.</param>
        /// <param name="pageSize">Number of items per page.</param>
        /// <param name="searchTerm">Optional search filter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// A paginated collection of moderation user records.
        /// </returns>
        Task<PagedResult<UserModerationDto>> GetUsersForModerationAsync(int page, int pageSize, string? searchTerm, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves detailed moderation information for a specific user.
        /// </summary>
        /// <param name="userId">Identifier of the user.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// Detailed moderation information including account status and activity statistics.
        /// </returns>
        /// <exception cref="SpectrumNotFoundException">
        /// Thrown when the user cannot be found.
        /// </exception>
        Task<AdminUserDetailDto> GetUserDetailAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Soft-deletes a user account and marks it as suspended.
        /// </summary>
        /// <param name="userId">Identifier of the target user.</param>
        /// <param name="requesterId">
        /// Identifier of the moderator performing the action.
        /// </param>
        /// <param name="reason">
        /// Optional moderation reason.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="SpectrumNotFoundException">
        /// Thrown when the target user cannot be found.
        /// </exception>
        /// <exception cref="SpectrumForbiddenException">
        /// Thrown when moderation is not allowed for the target account.
        /// </exception>
        Task DeleteUserAsync(Guid userId, Guid? requesterId, string? reason, CancellationToken cancellationToken);

        /// <summary>
        /// Soft-deletes a user account.
        /// </summary>
        /// <param name="userId">Identifier of the target user.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Provides moderation operations for user accounts, including suspension,
    /// banning, deletion, reactivation, and moderation queries.
    /// </summary>
    public class UserModerationService : IUserModerationService
    {
        private readonly IUserRepository _userRepository;
        private readonly IEmailService? _emailService;
        private readonly IAdminNotificationService? _adminNotificationService;
        private readonly ILogger<UserModerationService>? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserModerationService"/> class.
        /// </summary>
        /// <param name="userRepository">
        /// Repository used to retrieve and update user moderation data.
        /// </param>
        /// <param name="emailService">
        /// Optional email service used to notify users about moderation actions.
        /// </param>
        /// <param name="adminNotificationService">
        /// Optional service used to send moderation notifications through the administration workflow.
        /// </param>
        /// <param name="logger">
        /// Optional logger used to record notification delivery failures.
        /// </param>
        public UserModerationService(
            IUserRepository userRepository,
            IEmailService? emailService = null,
            IAdminNotificationService? adminNotificationService = null,
            ILogger<UserModerationService>? logger = null)
        {
            _userRepository = userRepository;
            _emailService = emailService;
            _adminNotificationService = adminNotificationService;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<AdminUserDetailDto> GetUserDetailAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var user = await ResolveUserForModerationAsync(userId);
            if (user == null)
            {
                throw new SpectrumNotFoundException(Constants.ErrorMessages.UserNotFound);
            }

            var totalReviews = await _userRepository.GetTotalReviewsCountAsync(userId, cancellationToken);
            var totalClips = await _userRepository.GetTotalClipsCountAsync(userId, cancellationToken);

            return new AdminUserDetailDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                IsSuspended = user.IsSuspended,
                IsBanned = user.IsBanned,
                IsDeleted = user.IsDeleted,
                Status = ResolveStatus(user),
                CreatedAt = user.CreatedAt,
                AvatarUrl = user.ProfilePicture,
                TotalReviews = totalReviews,
                TotalClips = totalClips
            };
        }

        /// <inheritdoc/>
        public async Task<PagedResult<UserModerationDto>> GetUsersForModerationAsync(
            int page,
            int pageSize,
            string? searchTerm,
            string? status,
            CancellationToken cancellationToken)
        {
            var pagedUsers = await _userRepository.GetPaginatedUsersAsync(page, pageSize, searchTerm, status, cancellationToken);

            var dtos = pagedUsers.Items.Select(user => new UserModerationDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                IsSuspended = user.IsSuspended,
                IsBanned = user.IsBanned,
                IsDeleted = user.IsDeleted,
                Status = ResolveStatus(user),
                CreatedAt = user.CreatedAt
            }).ToList();

            return new PagedResult<UserModerationDto>
            {
                Items = dtos,
                TotalCount = pagedUsers.TotalCount,
                Page = pagedUsers.Page,
                PageSize = pagedUsers.PageSize
            };
        }

        /// <inheritdoc/>
        public Task<PagedResult<UserModerationDto>> GetUsersForModerationAsync(int page, int pageSize, string? searchTerm, CancellationToken cancellationToken = default)
        {
            return MapUsersForModerationAsync(
                _userRepository.GetPaginatedUsersAsync(page, pageSize, searchTerm, cancellationToken)
            );
        }

        /// <inheritdoc/>
        public async Task DeleteUserAsync(Guid userId, Guid? requesterId, string? reason, CancellationToken cancellationToken)
        {
            var user = await ResolveUserForModerationAsync(userId);
            if (user == null || user.IsDeleted)
            {
                throw new SpectrumNotFoundException(Constants.ErrorMessages.UserNotFound);
            }

            EnsureCanModerate(user, requesterId);

            user.IsDeleted = true;
            user.IsSuspended = true;
            await _userRepository.UpdateUserAsync(user);
            await NotifyDeletedAsync(user, reason, cancellationToken);
        }

        /// <inheritdoc/>
        public Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return DeleteUserAsync(userId, requesterId: null, reason: null, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task ReactivateUserAsync(Guid userId, Guid? requesterId, CancellationToken cancellationToken)
        {
            var user = await ResolveUserForModerationAsync(userId);
            if (user == null)
            {
                throw new SpectrumNotFoundException(Constants.ErrorMessages.UserNotFound);
            }

            EnsureCanModerate(user, requesterId);

            if (!user.IsDeleted && !user.IsSuspended && !user.IsBanned)
            {
                throw new SpectrumBusinessException(Constants.ErrorMessages.AccountAlreadyActive);
            }

            user.IsDeleted = false;
            user.IsSuspended = false;
            user.IsBanned = false;
            await _userRepository.UpdateUserAsync(user);
            await NotifyReactivatedAsync(user, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task ToggleBanAsync(Guid targetUserId, bool ban, Guid? requesterId, string? reason, CancellationToken cancellationToken)
        {
            var user = await ResolveUserForModerationAsync(targetUserId);
            if (user == null || user.IsDeleted)
            {
                throw new SpectrumNotFoundException(Constants.ErrorMessages.UserNotFound);
            }

            EnsureCanModerate(user, requesterId);

            if (user.IsBanned == ban)
            {
                throw new SpectrumBusinessException(ban
                    ? Constants.ErrorMessages.AccountAlreadyBanned
                    : Constants.ErrorMessages.AccountAlreadyActive);
            }

            user.IsBanned = ban;
            user.IsSuspended = ban;
            await _userRepository.UpdateUserAsync(user);

            if (ban)
            {
                await NotifyBannedAsync(user, reason, cancellationToken);
            }
            else
            {
                await NotifyUnbannedAsync(user, cancellationToken);
            }
        }

        /// <inheritdoc/>
        public async Task ToggleSuspensionAsync(Guid targetUserId, bool suspend, Guid? requesterId, string? reason, CancellationToken cancellationToken)
        {
            var user = await ResolveUserForModerationAsync(targetUserId);
            if (user == null || user.IsDeleted)
            {
                throw new SpectrumNotFoundException(Constants.ErrorMessages.UserNotFound);
            }

            EnsureCanModerate(user, requesterId);

            if (user.IsSuspended == suspend)
            {
                throw new SpectrumBusinessException(Constants.ErrorMessages.AccountAlreadySuspended);
            }

            user.IsSuspended = suspend;
            if (!suspend)
            {
                user.IsBanned = false;
            }

            await _userRepository.UpdateUserAsync(user);

            if (suspend)
            {
                await NotifySuspendedAsync(user, reason, cancellationToken);
            }
            else
            {
                await NotifyReactivatedAsync(user, cancellationToken);
            }
        }

        /// <inheritdoc/>
        public Task ToggleSuspensionAsync(Guid targetUserId, bool suspend, CancellationToken cancellationToken = default)
        {
            return ToggleSuspensionAsync(targetUserId, suspend, requesterId: null, reason: null, cancellationToken);
        }

        private static void EnsureCanModerate(User user, Guid? requesterId)
        {
            if ((requesterId.HasValue && user.Id == requesterId.Value) || user.Role == Constants.Roles.Admin)
            {
                throw new SpectrumForbiddenException(Constants.ErrorMessages.AdminSanctionForbidden);
            }
        }

        private async Task<User?> ResolveUserForModerationAsync(Guid userId)
        {
            return await _userRepository.GetUserByIdForModerationAsync(userId) ??
                   await _userRepository.GetUserByIdAsync(userId);
        }

        private static async Task<PagedResult<UserModerationDto>> MapUsersForModerationAsync(Task<PagedResult<User>> pagedUsersTask)
        {
            var pagedUsers = await pagedUsersTask;
            var dtos = pagedUsers.Items.Select(user => new UserModerationDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                IsSuspended = user.IsSuspended,
                IsBanned = user.IsBanned,
                IsDeleted = user.IsDeleted,
                Status = ResolveStatus(user),
                CreatedAt = user.CreatedAt
            }).ToList();

            return new PagedResult<UserModerationDto>
            {
                Items = dtos,
                TotalCount = pagedUsers.TotalCount,
                Page = pagedUsers.Page,
                PageSize = pagedUsers.PageSize
            };
        }

        private static string ResolveStatus(User user)
        {
            if (user.IsDeleted) return "DELETED";
            if (user.IsBanned) return "BANNED";
            if (user.IsSuspended) return "SUSPENDED";
            return "ACTIVE";
        }

        private async Task NotifySuspendedAsync(User user, string? reason, CancellationToken cancellationToken)
        {
            if (_adminNotificationService is not null)
            {
                await _adminNotificationService.NotifyAccountSuspendedAsync(user, reason, cancellationToken: cancellationToken);
                return;
            }

            await TrySendAsync(() => _emailService?.SendAccountSuspendedAsync(user.Email), "suspension", user.Id, cancellationToken);
        }

        private async Task NotifyBannedAsync(User user, string? reason, CancellationToken cancellationToken)
        {
            if (_adminNotificationService is not null)
            {
                await _adminNotificationService.NotifyAccountBannedAsync(user, reason, cancellationToken);
                return;
            }

            await TrySendAsync(() => _emailService?.SendAccountBannedAsync(user.Email), "ban", user.Id, cancellationToken);
        }

        private async Task NotifyDeletedAsync(User user, string? reason, CancellationToken cancellationToken)
        {
            if (_adminNotificationService is not null)
            {
                await _adminNotificationService.NotifyAccountDeletedAsync(user, reason, cancellationToken);
                return;
            }

            await TrySendAsync(() => _emailService?.SendAccountBannedAsync(user.Email), "delete", user.Id, cancellationToken);
        }

        private async Task NotifyReactivatedAsync(User user, CancellationToken cancellationToken)
        {
            if (_adminNotificationService is not null)
            {
                await _adminNotificationService.NotifyAccountReactivatedAsync(user, cancellationToken);
                return;
            }

            await TrySendAsync(() => _emailService?.SendAccountReactivatedAsync(user.Email), "reactivation", user.Id, cancellationToken);
        }

        private async Task NotifyUnbannedAsync(User user, CancellationToken cancellationToken)
        {
            if (_adminNotificationService is not null)
            {
                await _adminNotificationService.NotifyAccountUnbannedAsync(user, cancellationToken);
                return;
            }

            await TrySendAsync(() => _emailService?.SendAccountReactivatedAsync(user.Email), "unban", user.Id, cancellationToken);
        }

        private async Task TrySendAsync(Func<Task?> send, string action, Guid userId, CancellationToken cancellationToken)
        {
            if (_emailService is null)
            {
                return;
            }

            try
            {
                var task = send();
                if (task is not null)
                {
                    await task;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger?.LogWarning(ex, "Could not send {Action} email for user {UserId}", action, userId);
            }
        }
    }
}
