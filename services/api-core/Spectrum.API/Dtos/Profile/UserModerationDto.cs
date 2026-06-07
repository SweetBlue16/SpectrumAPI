namespace Spectrum.API.Dtos.Profile
{
    public class UserModerationDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsSuspended { get; set; }
        public bool IsBanned { get; set; }
        public bool IsDeleted { get; set; }
        public string Status { get; set; } = "ACTIVE";
        public DateTime CreatedAt { get; set; }
    }
}
