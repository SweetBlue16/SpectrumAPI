using Spectrum.API.Services.Email;
using System.Security.Cryptography;
using System.Text;

namespace Spectrum.API.Services.Drops
{
    public interface IRewardDeliveryService
    {
        Task SendRewardAsync(
            string recipientEmail,
            string eventTitle,
            string rewardCode,
            CancellationToken cancellationToken = default
        );
    }

    public class EmailRewardDeliveryService : IRewardDeliveryService
    {
        private readonly IEmailService _emailService;
        private readonly ILogger<EmailRewardDeliveryService> _logger;

        public EmailRewardDeliveryService(IEmailService emailService, ILogger<EmailRewardDeliveryService> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        public async Task SendRewardAsync(
            string recipientEmail,
            string eventTitle,
            string rewardCode,
            CancellationToken cancellationToken = default
        )
        {
            await _emailService.SendGiveawayWinnerEmailAsync(recipientEmail, eventTitle, rewardCode);

            _logger.LogInformation(
                "Reward email sent for event {EventTitle} to recipient hash {RecipientHash}.",
                eventTitle,
                HashRecipient(recipientEmail)
            );
        }

        private static string HashRecipient(string recipientEmail)
        {
            return Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(recipientEmail.Trim().ToLowerInvariant())
            ))[..12];
        }
    }
}
