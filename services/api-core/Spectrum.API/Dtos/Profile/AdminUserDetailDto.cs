namespace Spectrum.API.Dtos.Profile
{
    /// <summary>
    /// Detailed administrative view of a user account including moderation and activity information.
    /// </summary>
    public class AdminUserDetailDto
    {
        /// <summary>
        /// Unique user identifier.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Username displayed across the platform.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// User email address.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Assigned platform role.
        /// </summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether the account is temporarily suspended.
        /// </summary>
        public bool IsSuspended { get; set; }

        /// <summary>
        /// Indicates whether the account is permanently banned.
        /// </summary>
        public bool IsBanned { get; set; }

        /// <summary>
        /// Indicates whether the account was soft deleted.
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// Current moderation status of the account.
        /// </summary>
        public string Status { get; set; } = "ACTIVE";

        /// <summary>
        /// Account creation date in UTC.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// User avatar image URL.
        /// </summary>
        public string? AvatarUrl { get; set; }

        /// <summary>
        /// Total number of reviews created by the user.
        /// </summary>
        public int TotalReviews { get; set; }

        /// <summary>
        /// Total number of clips uploaded by the user.
        /// </summary>
        public int TotalClips { get; set; }
    }
}
