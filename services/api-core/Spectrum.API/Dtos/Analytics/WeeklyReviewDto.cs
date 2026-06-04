namespace Spectrum.API.Dtos.Analytics
{
    /// <summary>
    /// Review or clip card payload used by weekly trends and compact review previews.
    /// </summary>
    public class WeeklyReviewDto
    {
        public Guid ReviewId { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string UserProfileImageUrl { get; set; } = string.Empty;
        public string ProfileImageUrl { get; set; } = string.Empty;
        public int GameId { get; set; }
        public string GameTitle { get; set; } = string.Empty;
        public string GameCoverUrl { get; set; } = string.Empty;
        public string CoverImageUrl { get; set; } = string.Empty;
        public string ReviewTitle { get; set; } = string.Empty;
        public string ReviewContent { get; set; } = string.Empty;
        public DateTime ReviewDate { get; set; }
        public int Rating { get; set; }
        public int Score { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string AttachmentUrl { get; set; } = string.Empty;
        public string AttachmentType { get; set; } = string.Empty;
        public int LikesCount { get; set; }
        public int DislikesCount { get; set; }
        public int CommentsCount { get; set; }
        public string SourceType { get; set; } = "REVIEW";
        public string? UserVote { get; set; }
        public string? CurrentUserVote { get; set; }
        public string? MyVote { get; set; }
        public bool IsOwnContent { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
