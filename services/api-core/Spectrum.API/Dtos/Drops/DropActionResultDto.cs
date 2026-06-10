namespace Spectrum.API.Dtos.Drops
{
    /// <summary>
    /// Represents the result of a giveaway-related action.
    /// </summary>
    public class DropActionResultDto
    {
        /// <summary>
        /// Indicates whether the operation completed successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Identifier of the affected giveaway event.
        /// </summary>
        public string EventId { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable operation result message.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
