namespace Spectrum.API.Dtos.Drops
{
    /// <summary>
    /// Represents the payload used to update an existing giveaway event.
    /// </summary>
    public class UpdateDropEventDto
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = string.Empty;

        public string GameTitle { get; set; } = string.Empty;

        public int? RawgGameId { get; set; }

        public string Platform { get; set; } = string.Empty;

        /// <summary>
        /// Date and time when participants can begin joining.
        /// </summary>
        public required DateTime StartAt { get; set; }

        /// <summary>
        /// Deadline for participant registration.
        /// </summary>
        public required DateTime JoinDeadlineAt { get; set; }

        /// <summary>
        /// Date and time when winners become eligible to claim rewards.
        /// </summary>
        public required DateTime RevealAt { get; set; }

        /// <summary>
        /// Date and time when the giveaway event finishes.
        /// </summary>
        public required DateTime EndAt { get; set; }

        /// <summary>
        /// Maximum number of participants allowed.
        /// </summary>
        public required int TotalSlots { get; set; }

        /// <summary>
        /// Public challenge code required to claim rewards.
        /// </summary>
        public string PublicChallengeCode { get; set; } = string.Empty;

        /// <summary>
        /// Collection of reward keys available for distribution.
        /// </summary>
        public List<string> AccessKeys { get; set; } = new();

        /// <summary>
        /// Current lifecycle status of the giveaway event.
        /// </summary>
        public string Status { get; set; } = string.Empty;
    }
}
