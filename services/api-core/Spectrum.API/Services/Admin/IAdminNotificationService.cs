using Spectrum.API.Models;
using Spectrum.API.Services.Email;

namespace Spectrum.API.Services.Admin
{
    public interface IAdminNotificationService
    {
        Task NotifyReviewDeletedAsync(Review review, string reason, CancellationToken cancellationToken = default);
        Task NotifyAccountSuspendedAsync(User user, string? reason = null, string? duration = null, CancellationToken cancellationToken = default);
        Task NotifyAccountReactivatedAsync(User user, CancellationToken cancellationToken = default);
        Task NotifyAccountBannedAsync(User user, string? reason = null, CancellationToken cancellationToken = default);
        Task NotifyAccountUnbannedAsync(User user, CancellationToken cancellationToken = default);
        Task NotifyAccountDeletedAsync(User user, string? reason = null, CancellationToken cancellationToken = default);
    }

    public class AdminNotificationService : IAdminNotificationService
    {
        private readonly IEmailService _emailService;
        private readonly ILogger<AdminNotificationService> _logger;

        public AdminNotificationService(IEmailService emailService, ILogger<AdminNotificationService> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        public Task NotifyReviewDeletedAsync(Review review, string reason, CancellationToken cancellationToken = default)
        {
            var message = $"Tu reseña \"{review.Title}\" fue retirada de Spectrum. Motivo: {reason}";
            return TryNotifyAsync(review.User?.Email, message, "review_deleted", review.Id, cancellationToken);
        }

        public Task NotifyAccountSuspendedAsync(User user, string? reason = null, string? duration = null, CancellationToken cancellationToken = default)
        {
            var durationText = string.IsNullOrWhiteSpace(duration) ? string.Empty : $" Duración: {duration.Trim()}.";
            var reasonText = string.IsNullOrWhiteSpace(reason) ? "incumplimiento de las reglas de la comunidad" : reason.Trim();
            return TryNotifyAsync(user.Email, $"Tu cuenta fue suspendida temporalmente. Motivo: {reasonText}.{durationText}", "account_suspended", user.Id, cancellationToken);
        }

        public Task NotifyAccountReactivatedAsync(User user, CancellationToken cancellationToken = default)
        {
            return TryNotifyAsync(user.Email, "Tu cuenta fue reactivada y ya puedes volver a usar Spectrum.", "account_reactivated", user.Id, cancellationToken);
        }

        public Task NotifyAccountBannedAsync(User user, string? reason = null, CancellationToken cancellationToken = default)
        {
            var reasonText = string.IsNullOrWhiteSpace(reason) ? "incumplimiento de las reglas de la comunidad" : reason.Trim();
            return TryNotifyAsync(user.Email, $"Tu cuenta fue baneada. Motivo: {reasonText}.", "account_banned", user.Id, cancellationToken);
        }

        public Task NotifyAccountUnbannedAsync(User user, CancellationToken cancellationToken = default)
        {
            return TryNotifyAsync(user.Email, "El baneo de tu cuenta fue retirado. Ya puedes volver a usar Spectrum.", "account_unbanned", user.Id, cancellationToken);
        }

        public Task NotifyAccountDeletedAsync(User user, string? reason = null, CancellationToken cancellationToken = default)
        {
            var reasonText = string.IsNullOrWhiteSpace(reason) ? "acción administrativa" : reason.Trim();
            return TryNotifyAsync(user.Email, $"Tu cuenta fue desactivada. Motivo: {reasonText}.", "account_deleted", user.Id, cancellationToken);
        }

        private async Task TryNotifyAsync(string? email, string message, string action, Guid targetId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return;
            }

            try
            {
                await _emailService.SendReportActionAsync(email, message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Could not send admin notification {Action} for target {TargetId}", action, targetId);
            }
        }
    }
}
