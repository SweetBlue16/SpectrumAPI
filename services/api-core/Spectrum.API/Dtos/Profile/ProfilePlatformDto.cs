namespace Spectrum.API.Dtos.Profile
{
    /// <summary>
    /// DTO that represents a platform in user's profile. 
    /// </summary>
    public class ProfilePlatformDto
    {
        /// <summary>
        /// Platform identifier.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Platform display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }
}