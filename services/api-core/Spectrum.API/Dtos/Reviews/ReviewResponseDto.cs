namespace Spectrum.API.Dtos.Reviews
{
    /// <summary>
    /// Review contract consumed by SpectrumApp in game lists, profile views and detail modals.
    /// </summary>
    public class ReviewResponseDto
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public string UserProfileImageUrl { get; set; } = string.Empty;

        public string ProfilePicture { get; set; } = string.Empty;

        public int GameId { get; set; }

        public string GameTitle { get; set; } = string.Empty;

        public string GameCoverUrl { get; set; } = string.Empty;

        public int Rating { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = string.Empty;

        public string AttachmentUrl { get; set; } = string.Empty;

        public string AttachmentType { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int LikesCount { get; set; }

        public int DislikesCount { get; set; }

        /// <summary>
        /// Authenticated user's active vote for this review: like, dislike or null.
        /// </summary>
        public string? CurrentUserVote { get; set; }

        public string? UserVote { get; set; }

        public string? MyVote { get; set; }

        public bool IsOwnReview { get; set; }

        public bool CanDelete { get; set; }
    }
}
