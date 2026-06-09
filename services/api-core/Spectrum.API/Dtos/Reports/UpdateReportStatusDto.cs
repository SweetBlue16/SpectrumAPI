namespace Spectrum.API.Dtos.Reports
{
    /// <summary>
    /// Represents the information required to update the status
    /// and resolution details of a moderation report.
    /// </summary>
    public class UpdateReportStatusDto
    {
        /// <summary>
        /// Gets or sets the new report status.
        /// </summary>
        public string NewStatus { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets moderator notes describing the resolution.
        /// </summary>
        public string ResolutionNotes { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the report status value used by legacy clients.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets administrative notes associated with the moderation action.
        /// </summary>
        public string AdminNotes { get; set; } = string.Empty;
    }
}
