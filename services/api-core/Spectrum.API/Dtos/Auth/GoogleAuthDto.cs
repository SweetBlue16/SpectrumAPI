using System.ComponentModel.DataAnnotations;

namespace Spectrum.API.Dtos.Auth
{
    public class GoogleAuthDto
    {
        [Required]
        public string Credential { get; set; }
    }
}
