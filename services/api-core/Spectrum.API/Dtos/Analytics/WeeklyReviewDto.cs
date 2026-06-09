namespace Spectrum.API.Dtos.Analytics
{
    /// <summary>
    /// Review or clip card payload used by weekly trends and compact review previews.
    /// </summary>
    public class WeeklyReviewDto
    {
        /// <summary>
        /// Unique identifier of the review.
        /// </summary>
        public Guid ReviewId { get; set; }

        /// <summary>
        /// Identifier of the review author.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Author username.
        /// </summary>
        public string Username { get; set; } = string.Empty;
        
        public string UserProfileImageUrl { get; set; } = string.Empty;

        public string ProfileImageUrl { get; set; } = string.Empty;

        /// <summary>
        /// Related game identifier.
        /// </summary>
        public int GameId { get; set; }

        /// <summary>
        /// Related game title.
        /// </summary>
        public string GameTitle { get; set; } = string.Empty;

        public string GameCoverUrl { get; set; } = string.Empty;

        public string CoverImageUrl { get; set; } = string.Empty;

        /// <summary>
        /// Review headline.
        /// </summary>
        public string ReviewTitle { get; set; } = string.Empty;

        /// <summary>
        /// Main review content.
        /// </summary>
        public string ReviewContent { get; set; } = string.Empty;

        public DateTime ReviewDate { get; set; }

        /// <summary>
        /// User rating assigned to the game.
        /// </summary>
        public int Rating { get; set; }

        /// <summary>
        /// Calculated engagement score used for rankings.
        /// </summary>
        public int Score { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public string AttachmentUrl { get; set; } = string.Empty;

        public string AttachmentType { get; set; } = string.Empty;

        /// <summary>
        /// Total positive votes received.
        /// </summary>
        public int LikesCount { get; set; }

        /// <summary>
        /// Total negative votes received.
        /// </summary>
        public int DislikesCount { get; set; }

        /// <summary>
        /// Total comments associated with the review.
        /// </summary>
        public int CommentsCount { get; set; }

        /// <summary>
        /// Indicates whether the content originated from a review or another supported source.
        /// </summary>
        public string SourceType { get; set; } = "REVIEW";

        public string? UserVote { get; set; }

        /// <summary>
        /// Current authenticated user's vote state.
        /// </summary>
        public string? CurrentUserVote { get; set; }

        public string? MyVote { get; set; }

        /// <summary>
        /// Indicates whether the content belongs to the authenticated user.
        /// </summary>
        public bool IsOwnContent { get; set; }

        /// <summary>
        /// Creation timestamp in UTC.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}
