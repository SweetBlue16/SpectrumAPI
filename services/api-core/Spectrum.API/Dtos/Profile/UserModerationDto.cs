namespace Spectrum.API.Dtos.Profile
{
    /// <summary>
    /// Administrative user summary used in moderation listings.
    /// </summary>
    public class UserModerationDto
    {
        /// <summary>
        /// Unique user identifier.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Username displayed on the platform.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// User email address.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Assigned role.
        /// </summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether the account is suspended.
        /// </summary>
        public bool IsSuspended { get; set; }

        /// <summary>
        /// Indicates whether the account is banned.
        /// </summary>
        public bool IsBanned { get; set; }

        /// <summary>
        /// Indicates whether the account is deleted.
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// Current moderation status.
        /// </summary>
        public string Status { get; set; } = "ACTIVE";

        /// <summary>
        /// Account creation date.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}
