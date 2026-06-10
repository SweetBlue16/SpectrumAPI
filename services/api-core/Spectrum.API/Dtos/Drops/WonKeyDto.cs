namespace Spectrum.API.Dtos.Drops
{
    /// <summary>
    /// Represents a reward key successfully claimed by a user from a giveaway event.
    /// </summary>
    public class WonKeyDto
    {
        /// <summary>
        /// Unique identifier of the giveaway event.
        /// </summary>
        public string EventId { get; set; } = string.Empty;

        /// <summary>
        /// Title of the game associated with the reward.
        /// </summary>
        public string GameTitle { get; set; } = string.Empty;

        /// <summary>
        /// Redeemable access key assigned to the winner.
        /// </summary>
        public string AccessKeyCode { get; set; } = string.Empty;

        /// <summary>
        /// Date and time when the reward was claimed.
        /// </summary>
        public DateTime ClaimedAt { get; set; }

        /// <summary>
        /// Current delivery status of the reward.
        /// </summary>
        public string RewardDeliveryStatus { get; set; } = string.Empty;
    }
}
