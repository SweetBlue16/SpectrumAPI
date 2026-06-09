namespace Spectrum.API.Dtos.Auth
{
    /// <summary>
    /// Represents a generic operation result returned by the API.
    /// </summary>
    public class MessageResponseDto
    {
        /// <summary>
        /// Human-readable message describing the outcome of the operation.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
