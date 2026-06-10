namespace Spectrum.API.Dtos.Drops
{
    /// <summary>
    /// Payload used by a participant to claim a reward from a giveaway event.
    /// </summary>
    public class ClaimDropDto
    {
        /// <summary>
        /// Public challenge code required to validate the claim.
        /// </summary>
        public string ChallengeCode { get; set; } = string.Empty;
    }
}
