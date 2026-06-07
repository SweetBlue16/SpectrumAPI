using System.ComponentModel.DataAnnotations;

namespace Spectrum.API.Dtos.Profile
{
    public class AdminModerationActionDto
    {
        [MaxLength(300)]
        public string? Reason { get; set; }
    }
}
