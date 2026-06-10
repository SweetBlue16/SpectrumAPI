namespace Spectrum.API.Dtos.Votes
{
    /// <summary>
    /// DTO for casting a review vote. Contains a single property indicating whether the vote is positive (like) or negative (dislike).
    /// </summary>
    public class CastReviewVoteDto
    {
        /// <summary>
        /// Indicates whether the vote is positive (like) or negative (dislike). True for like, false for dislike.
        /// </summary>
        public required bool IsPositive { get; set; }
    }
}
