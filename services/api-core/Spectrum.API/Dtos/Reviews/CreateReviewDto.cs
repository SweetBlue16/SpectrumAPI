using System.ComponentModel.DataAnnotations;

namespace Spectrum.API.Dtos.Reviews
{
    /// <summary>
    /// Request used to create a new game review.
    /// </summary>
    public class CreateReviewDto
    {
        /// <summary>
        /// Identifier of the reviewed game.
        /// </summary>
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "El ID del videojuego debe ser valido.")]
        public int GameId { get; set; }

        /// <summary>
        /// Review score from 5 to 10.
        /// </summary>
        [Required]
        [Range(5, 10, ErrorMessage = "La calificacion debe estar entre 5 y 10.")]
        public int Rating { get; set; }

        /// <summary>
        /// Review title shown in listings and detail pages.
        /// </summary>
        [Required]
        [MinLength(1, ErrorMessage = "El titulo de la resena es obligatorio.")]
        [MaxLength(120, ErrorMessage = "El titulo de la resena no puede superar los 120 caracteres.")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Detailed review content.
        /// </summary>
        [Required]
        [MinLength(1, ErrorMessage = "El contenido de la resena es obligatorio.")]
        [MaxLength(2000, ErrorMessage = "El contenido de la resena no puede superar los 2000 caracteres.")]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Optional image attached to the review.
        /// </summary>
        [MaxLength(255, ErrorMessage = "La URL del adjunto no puede superar los 255 caracteres.")]
        public string? ImageUrl { get; set; }

        /// <summary>
        /// MIME type or media category of the attachment.
        /// </summary>
        [MaxLength(50, ErrorMessage = "El tipo de archivo no puede superar los 50 caracteres.")]
        public string? MediaType { get; set; }
    }
}
