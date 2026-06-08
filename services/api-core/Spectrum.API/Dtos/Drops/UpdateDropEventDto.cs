namespace Spectrum.API.Dtos.Drops
{
    public class UpdateDropEventDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string GameTitle { get; set; } = string.Empty;
        public int? RawgGameId { get; set; }
        public string Platform { get; set; } = string.Empty;
        public required DateTime StartAt { get; set; }
        public required DateTime JoinDeadlineAt { get; set; }
        public required DateTime RevealAt { get; set; }
        public required DateTime EndAt { get; set; }
        public required int TotalSlots { get; set; }
        public string PublicChallengeCode { get; set; } = string.Empty;
        public List<string> AccessKeys { get; set; } = new();
        public string Status { get; set; } = string.Empty;
    }
}
