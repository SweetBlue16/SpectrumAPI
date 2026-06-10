namespace Spectrum.API.Dtos.Drops
{
    /// <summary>
    /// Public giveaway state consumed by SpectrumApp. Sensitive reward codes are
    /// intentionally omitted unless an administrator uses an explicit admin flow.
    /// </summary>
    public class EventStatusDto
    {
        public string EventId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = string.Empty;

        public string GameTitle { get; set; } = string.Empty;

        public int? RawgGameId { get; set; }

        public string Platform { get; set; } = string.Empty;

        public DateTime StartAt { get; set; }

        public DateTime JoinDeadlineAt { get; set; }

        public DateTime RevealAt { get; set; }

        public DateTime EndAt { get; set; }

        public int TotalSlots { get; set; }

        /// <summary>
        /// Number of participant slots still available.
        /// </summary>
        public int AvailableSlots { get; set; }

        /// <summary>
        /// Current lifecycle state of the giveaway event.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        public string PublicChallengeCode { get; set; } = string.Empty;

        /// <summary>
        /// Identifier of the administrator who created the event.
        /// </summary>
        public string CreatedByAdminId { get; set; } = string.Empty;

        public string? WinnerUserId { get; set; }

        public string? WinnerUsername { get; set; }

        public DateTime? FinishedAt { get; set; }

        public DateTime? RewardSentAt { get; set; }

        public string RewardDeliveryStatus { get; set; } = "PENDING";

        /// <summary>
        /// Total number of registered participants.
        /// </summary>
        public int ParticipantsCount { get; set; }

        /// <summary>
        /// Number of reward codes still available for claiming.
        /// </summary>
        public int RewardCodesAvailable { get; set; }

        /// <summary>
        /// Total reward codes configured for the event.
        /// </summary>
        public int RewardCodesTotal { get; set; }

        /// <summary>
        /// Indicates whether the authenticated user is already registered for this event.
        /// </summary>
        public bool CurrentUserJoined { get; set; }

        public bool IsJoined => CurrentUserJoined;

        /// <summary>
        /// Indicates whether the authenticated user can join at the current server time.
        /// </summary>
        public bool CanJoin { get; set; }

        /// <summary>
        /// Indicates whether the authenticated registered user can attempt to claim a reward code.
        /// </summary>
        public bool CanClaim { get; set; }

        /// <summary>
        /// Indicates whether the authenticated user already claimed a reward code for this event.
        /// </summary>
        public bool HasClaimed { get; set; }

        public int RemainingSlots { get; set; }

        /// <summary>
        /// Moment until which a finished or exhausted event remains visible publicly.
        /// </summary>
        public DateTime? VisibleUntil { get; set; }

        /// <summary>
        /// Winners associated with this giveaway event.
        /// </summary>
        public List<DropWinnerDto> Winners { get; set; } = new();

        public int KeysAvailable => RewardCodesAvailable;

        public int KeysTotal => RewardCodesTotal;

        public DateTime EndDate => EndAt;

        public DateTime CloseAt => JoinDeadlineAt;

        public int MaxParticipants => TotalSlots;

        public int ParticipantCount => ParticipantsCount;

        public int ClaimedRewardCount => RewardCodesTotal - RewardCodesAvailable;

    }

    /// <summary>
    /// Represents a winner of a giveaway event.
    /// </summary>
    public class DropWinnerDto
    {
        /// <summary>
        /// Unique identifier of the winning user.
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Public username of the winner.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Date and time when the reward was claimed.
        /// </summary>
        public DateTime? ClaimedAt { get; set; }

        /// <summary>
        /// Current reward delivery status.
        /// </summary>
        public string DeliveryStatus { get; set; } = "PENDING";
    }
}
