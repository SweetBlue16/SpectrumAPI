namespace Spectrum.API.Dtos.Profile
{
    /// <summary>
    /// Request used to suspend or block a user account.
    /// </summary>
    public class BlockUserDto
    {
        /// <summary>
        /// Optional moderation reason.
        /// </summary>
        public string? Reason { get; set; }
    }
}
