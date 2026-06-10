namespace Spectrum.API.Dtos.Drops
{
    /// <summary>
    /// Represents the reward code sent to a giveaway winner.
    /// </summary>
    public class SendRewardDto
    {
        /// <summary>
        /// Redeemable reward key assigned to the winner.
        /// </summary>
        public string RewardCode { get; set; } = string.Empty;
    }
}
