using System.ComponentModel.DataAnnotations;

namespace Spectrum.API.Dtos.Reviews
{
    /// <summary>
    /// Request used to publish a comment on a review.
    /// </summary>
    public class CreateReviewCommentDto
    {
        /// <summary>
        /// Comment text visible to other users.
        /// </summary>
        [Required]
        [MinLength(1, ErrorMessage = "El comentario es obligatorio.")]
        [MaxLength(500, ErrorMessage = "El comentario no puede superar los 500 caracteres.")]
        public string Content { get; set; } = string.Empty;
    }
}
