namespace Spectrum.API.Dtos.Profile
{
    /// <summary>
    /// Lightweight public representation of a Spectrum user.
    /// </summary>
    public class PublicUserSummaryDto
    {
        /// <summary>
        /// Unique user identifier.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Public username.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Profile picture URL.
        /// </summary>
        public string ProfilePicture { get; set; } = string.Empty;
    }
}
