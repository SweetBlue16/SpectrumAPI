namespace Spectrum.API.Dtos.Votes
{
    /// <summary>
    /// Represents the result of a voting action, including whether it was successful and the updated counts of likes and dislikes.
    /// </summary>
    public class VoteResultDto
    {
        /// <summary>
        /// Indicates whether the voting action was successful. A value of true means the vote was processed successfully, while false indicates a failure (e.g., due to invalid input, user not authorized, etc.).
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The updated number of likes after the voting action has been processed. This value reflects the new total count of likes for the item (e.g., post, comment) that was voted on.
        /// </summary>
        public int UpdatedLikes { get; set; }

        /// <summary>
        /// The updated number of dislikes after the voting action has been processed. This value reflects the new total count of dislikes for the item (e.g., post, comment) that was voted on.
        /// </summary>
        public int UpdatedDislikes { get; set; }
    }
}
