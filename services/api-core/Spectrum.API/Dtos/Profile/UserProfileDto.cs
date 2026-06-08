using System.ComponentModel.DataAnnotations;

namespace Spectrum.API.Dtos.Profile
{
    /// <summary>
    /// Data transfer object representing the detailed profile information of a user.
    /// </summary>
    public class UserProfileDto
    {
        public required Guid Id { get; set; }

        /// <summary>
        /// The authenticated user's public display name.
        /// </summary>
        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// The authenticated user's primary email address.
        /// </summary>
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// The URL or filename of the user's profile image. This can be null if the user has not set a custom photo.
        /// </summary>
        public string? ProfilePicture { get; set; }

        /// <summary>
        /// The user's biography or description.
        /// </summary>
        [MaxLength(500)]
        public string? Biography { get; set; }

        /// <summary>
        /// A list of the user's favorite or interested games.
        /// </summary>
        public List<ProfileGameDto> InterestedGames { get; set; } = new List<ProfileGameDto>();

        /// <summary>
        /// A list of gaming platforms the user plays on.
        /// </summary>
        public List<ProfilePlatformDto> Platforms { get; set; } = new List<ProfilePlatformDto>();
    }
}
