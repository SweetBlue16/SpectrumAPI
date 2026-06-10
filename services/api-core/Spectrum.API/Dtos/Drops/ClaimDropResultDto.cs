namespace Spectrum.API.Dtos.Drops
{
    /// <summary>
    /// Represents the result of a reward claim attempt.
    /// </summary>
    public class ClaimDropResultDto
    {
        /// <summary>
        /// Indicates whether the claim operation succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Identifier of the giveaway event.
        /// </summary>
        public string EventId { get; set; } = string.Empty;

        /// <summary>
        /// Identifier of the winning user, if applicable.
        /// </summary>
        public string? WinnerUserId { get; set; }

        /// <summary>
        /// Username of the winning user, if applicable.
        /// </summary>
        public string? WinnerUsername { get; set; }

        /// <summary>
        /// Date and time when the reward was claimed.
        /// </summary>
        public DateTime? ClaimedAt { get; set; }

        /// <summary>
        /// Human-readable claim result message.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
