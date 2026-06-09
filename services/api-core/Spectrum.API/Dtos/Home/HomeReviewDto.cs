namespace Spectrum.API.Dtos.Home
{
    /// <summary>
    /// Popular review payload for Home cards with enough visual and vote state data for SpectrumApp.
    /// </summary>
    public class HomeReviewDto
    {
        /// <summary>
        /// Unique identifier of the review.
        /// </summary>
        public Guid ReviewId { get; set; }

        /// <summary>
        /// Identifier of the user who created the review.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Review author's username.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// URL of the review author's profile picture.
        /// </summary>
        public string UserProfileImageUrl { get; set; } = string.Empty;

        /// <summary>
        /// Legacy alias of <see cref="UserProfileImageUrl"/> maintained for compatibility.
        /// </summary>
        public string ProfileImageUrl { get; set; } = string.Empty;

        /// <summary>
        /// Identifier of the reviewed game.
        /// </summary>
        public int GameId { get; set; }

        /// <summary>
        /// Display title of the reviewed game.
        /// </summary>
        public string GameTitle { get; set; } = string.Empty;

        /// <summary>
        /// URL of the game's cover image.
        /// </summary>
        public string GameCoverUrl { get; set; } = string.Empty;

        /// <summary>
        /// Legacy alias of <see cref="GameCoverUrl"/> maintained for compatibility.
        /// </summary>
        public string CoverImageUrl { get; set; } = string.Empty;

        /// <summary>
        /// Review title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Review content provided by the user.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Numerical rating assigned by the reviewer.
        /// </summary>
        public int Rating { get; set; }

        /// <summary>
        /// Total number of positive votes received.
        /// </summary>
        public int LikesCount { get; set; }

        /// <summary>
        /// Total number of negative votes received.
        /// </summary>
        public int DislikesCount { get; set; }

        /// <summary>
        /// Vote cast by the authenticated user, if any.
        /// </summary>
        public string? CurrentUserVote { get; set; }

        /// <summary>
        /// Legacy alias of <see cref="CurrentUserVote"/> maintained for compatibility.
        /// </summary>
        public string? UserVote { get; set; }

        /// <summary>
        /// Legacy alias of <see cref="CurrentUserVote"/> maintained for compatibility.
        /// </summary>
        public string? MyVote { get; set; }

        /// <summary>
        /// Indicates whether the review belongs to the authenticated user.
        /// </summary>
        public bool IsOwnReview { get; set; }

        /// <summary>
        /// Indicates whether the authenticated user can vote on this review.
        /// </summary>
        public bool CanVote { get; set; } = true;

        /// <summary>
        /// Total number of comments associated with the review.
        /// </summary>
        public int CommentsCount { get; set; }

        /// <summary>
        /// Date and time when the review was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}
