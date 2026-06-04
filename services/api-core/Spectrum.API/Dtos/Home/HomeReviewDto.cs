namespace Spectrum.API.Dtos.Home
{
    /// <summary>
    /// Popular review payload for Home cards with enough visual and vote state data for SpectrumApp.
    /// </summary>
    public class HomeReviewDto
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
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public int Rating { get; set; }
        public int LikesCount { get; set; }
        public int DislikesCount { get; set; }
        public string? CurrentUserVote { get; set; }
        public string? UserVote { get; set; }
        public string? MyVote { get; set; }
        public int CommentsCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
