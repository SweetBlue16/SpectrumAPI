using Spectrum.API.Utilities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Spectrum.API.Models
{
    /// <summary>
    /// Represents the core identity entity within the relational database. 
    /// This model handles authentication state, authorization roles, and acts as the 
    /// primary principal for cross-service data correlation (e.g., matching a userId in MongoDB).
    /// </summary>
    [Table("users")]
    public class User
    {
        /// <summary>
        /// The primary key generated as a universally unique identifier (UUID/GUID). 
        /// Used strictly for internal referential integrity and distributed system tracking.
        /// </summary>
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// The unique public-facing moniker for the user. Restricted to 50 characters 
        /// to optimize database index sizes and UI rendering constraints.
        /// </summary>
        [Required]
        [MaxLength(50)]
        [Column("username")]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// The unique email address serving as the primary credential for local authentication 
        /// and system notifications. Maps to identity provider claims (e.g., Google SSO).
        /// </summary>
        [Required]
        [MaxLength(100)]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// The URL or file path to the user's custom profile image. 
        /// </summary>
        [Column("profile_picture")] 
        public string? ProfilePicture { get; set; }

        /// <summary>
        /// Short description or biography for the user can redact his profile.
        /// </summary>
        [MaxLength(500)]
        [Column("biography")]
        public string? Biography { get; set; }

        /// <summary>
        /// The cryptographically secure hash of the user's password, generated using the BCrypt algorithm. 
        /// Never used to store plain-text or easily reversible credentials.
        /// </summary>
        [Required]
        [Column("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Determines the authorization level and access boundaries for the user within the system. 
        /// Must strictly correspond to one of the predefined <see cref="Constants.Roles"/>.
        /// </summary>
        [Required]
        [MaxLength(20)]
        [Column("role")]
        public string Role { get; set; } = Constants.Roles.Reviewer;

        /// <summary>
        /// An administrative flag indicating whether the user's privileges have been temporarily revoked. 
        /// When true, the API gateway or auth service should actively reject login attempts and state-mutating requests.
        /// </summary>
        [Column("is_suspended")]
        public bool IsSuspended { get; set; } = false;

        [Column("is_banned")]
        public bool IsBanned { get; set; } = false;

        /// <summary>
        /// Implements the "soft-delete" pattern. When true, the user is logically removed from active 
        /// application queries to preserve historical referential integrity (e.g., past reviews/votes) 
        /// without physically dropping the record.
        /// </summary>
        [Column("is_deleted")]
        public bool IsDeleted { get; set; } = false;

        [Column("is_email_verified")]
        public bool IsEmailVerified { get; set; } = false;

        /// <summary>
        /// The exact UTC timestamp capturing when the user's identity was first provisioned in the database. 
        /// Used for auditing and account age verification.
        /// </summary>
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Entity Framework navigation property establishing a one-to-one (or zero-to-one) relationship 
        /// with <see cref="Models.AdminDetail"/>. This is lazily loaded and only populated if the user 
        /// holds an administrative role and has completed their onboarding profile.
        /// </summary>
        public virtual AdminDetail? AdminDetail { get; set; }

        /// <summary>
        /// Navegation property for the games of the player has marked like interest in his profile.
        /// Use lot a lot relation through intermedium table.
        /// </summary>
        public virtual ICollection<Game> InterestedGames { get; set; } = new List<Game>();

        /// <summary>
        /// Navigation property for the platforms that the user plays.
        /// Use lot a lot relation through intermedium table
        /// </summary>
        public virtual ICollection<Platform> Platforms { get; set; } = new List<Platform>();

        public virtual ICollection<VerificationCode> VerificationCodes { get; set; } = new List<VerificationCode>();

    }
}
