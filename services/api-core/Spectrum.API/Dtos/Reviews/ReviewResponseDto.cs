namespace Spectrum.API.Dtos.Reviews
{
    /// <summary>
    /// Review contract consumed by SpectrumApp in game lists, profile views and detail modals.
    /// </summary>
    public class ReviewResponseDto
    {
        /// <summary>
        /// Review identifier.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Review author identifier.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Review author username.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Profile image displayed for the author.
        /// </summary>
        public string UserProfileImageUrl { get; set; } = string.Empty;

        public string ProfilePicture { get; set; } = string.Empty;

        /// <summary>
        /// Reviewed game identifier.
        /// </summary>
        public int GameId { get; set; }

        /// <summary>
        /// Reviewed game title.
        /// </summary>
        public string GameTitle { get; set; } = string.Empty;

        public string GameCoverUrl { get; set; } = string.Empty;

        /// <summary>
        /// Review score from 5 to 10.
        /// </summary>
        public int Rating { get; set; }

        /// <summary>
        /// Review title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Review body content.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = string.Empty;

        public string AttachmentUrl { get; set; } = string.Empty;

        public string AttachmentType { get; set; } = string.Empty;

        /// <summary>
        /// Creation date in UTC.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Total positive votes.
        /// </summary>
        public int LikesCount { get; set; }

        /// <summary>
        /// Total negative votes.
        /// </summary>
        public int DislikesCount { get; set; }

        /// <summary>
        /// Authenticated user's active vote for this review: like, dislike or null.
        /// </summary>
        public string? CurrentUserVote { get; set; }

        public string? UserVote { get; set; }

        public string? MyVote { get; set; }

        /// <summary>
        /// Indicates whether the review belongs to the authenticated user.
        /// </summary>
        public bool IsOwnReview { get; set; }

        /// <summary>
        /// Indicates whether the authenticated user can delete the review.
        /// </summary>
        public bool CanDelete { get; set; }
    }
}
