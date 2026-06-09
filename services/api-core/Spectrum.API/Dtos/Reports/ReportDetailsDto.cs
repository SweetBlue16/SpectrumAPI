namespace Spectrum.API.Dtos.Reports
{
    /// <summary>
    /// Represents the complete information of a moderation report,
    /// including reporter details, target content metadata, status,
    /// and administrative resolution data.
    /// </summary>
    public class ReportDetailsDto
    {
        /// <summary>
        /// Gets or sets the unique identifier of the report.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the report identifier returned by the reporting service.
        /// </summary>
        public string ReportId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the identifier of the user who submitted the report.
        /// </summary>
        public string ReporterId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the identifier of the reported entity.
        /// </summary>
        public string TargetId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of the reported entity
        /// (for example REVIEW, USER, or GAME_CLIP).
        /// </summary>
        public string TargetType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the reason provided when the report was created.
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current moderation status of the report.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the creation date of the report record.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the date when the report was originally submitted.
        /// </summary>
        public DateTime ReportedAt { get; set; }

        /// <summary>
        /// Gets or sets the username of the reporting user.
        /// </summary>
        public string ReporterUsername { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a short preview of the reported content.
        /// </summary>
        public string? TargetContentSnippet { get; set; }

        /// <summary>
        /// Gets or sets internal notes recorded by moderators.
        /// </summary>
        public string? AdminNotes { get; set; }

        /// <summary>
        /// Gets or sets the date when the report was resolved.
        /// </summary>
        public DateTime? ResolvedAt { get; set; }

        /// <summary>
        /// Gets or sets the moderation action taken during resolution.
        /// </summary>
        public string? ResolutionAction { get; set; }
    }
}
