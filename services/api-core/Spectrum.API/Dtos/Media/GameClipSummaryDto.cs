namespace Spectrum.API.Dtos.Media
{
    /// <summary>
    /// Represents a summary of a game clip optimized for profile views.
    /// Maps perfectly with the frontend ClipData constraints.
    /// </summary>
    public class GameClipSummaryDto
    {
        /// <summary>
        /// Unique identifier of the clip.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// User-provided title of the clip.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Optional thumbnail image URL representing the clip.
        /// </summary>
        public string? ThumbnailUrl { get; set; }

        /// <summary>
        /// Name of the game associated with the clip.
        /// </summary>
        public string? GameName { get; set; }

        /// <summary>
        /// Public URL of the uploaded video clip.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Total number of positive votes received by the clip.
        /// </summary>
        public int LikesCount { get; set; }

        /// <summary>
        /// Total number of negative votes received by the clip.
        /// </summary>
        public int DislikesCount { get; set; }

        /// <summary>
        /// Current authenticated user's vote on the clip ("like", "dislike", or null).
        /// </summary>
        public string? UserVote { get; set; }

        /// <summary>
        /// Identifier of the user who uploaded the clip.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// UTC date and time when the clip was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}
