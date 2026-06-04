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
        public int AvailableSlots { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PublicChallengeCode { get; set; } = string.Empty;
        public string CreatedByAdminId { get; set; } = string.Empty;
        public string? WinnerUserId { get; set; }
        public string? WinnerUsername { get; set; }
        public DateTime? FinishedAt { get; set; }
        public DateTime? RewardSentAt { get; set; }
        public string RewardDeliveryStatus { get; set; } = "PENDING";
        public int ParticipantsCount { get; set; }
        public int RewardCodesAvailable { get; set; }
        public int RewardCodesTotal { get; set; }
        /// <summary>Indicates whether the authenticated user is already registered for this event.</summary>
        public bool CurrentUserJoined { get; set; }
        public bool IsJoined => CurrentUserJoined;
        /// <summary>Indicates whether the authenticated user can join at the current server time.</summary>
        public bool CanJoin { get; set; }
        /// <summary>Indicates whether the authenticated registered user can attempt to claim a reward code.</summary>
        public bool CanClaim { get; set; }
        /// <summary>Indicates whether the authenticated user already claimed a reward code for this event.</summary>
        public bool HasClaimed { get; set; }
        public int RemainingSlots { get; set; }
        /// <summary>Moment until which a finished or exhausted event remains visible publicly.</summary>
        public DateTime? VisibleUntil { get; set; }
        public List<DropWinnerDto> Winners { get; set; } = new();
        public int KeysAvailable => RewardCodesAvailable;
        public int KeysTotal => RewardCodesTotal;
        public DateTime EndDate => EndAt;
        public DateTime CloseAt => JoinDeadlineAt;
        public int MaxParticipants => TotalSlots;
        public int ParticipantCount => ParticipantsCount;
        public int ClaimedRewardCount => RewardCodesTotal - RewardCodesAvailable;
    }

    public class DropWinnerDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public DateTime? ClaimedAt { get; set; }
        public string DeliveryStatus { get; set; } = "PENDING";
    }
}
