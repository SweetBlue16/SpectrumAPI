namespace Spectrum.API.Dtos.Seed
{
    /// <summary>
    /// Summary of entities generated during demo data seeding.
    /// </summary>
    public class DemoSeedResultDto
    {
        /// <summary>
        /// Number of administrator accounts created.
        /// </summary>
        public int Admins { get; set; }

        /// <summary>
        /// Number of regular user accounts created.
        /// </summary>
        public int Users { get; set; }

        /// <summary>
        /// Number of reviews generated.
        /// </summary>
        public int Reviews { get; set; }

        /// <summary>
        /// Number of gameplay clips generated.
        /// </summary>
        public int Clips { get; set; }

        /// <summary>
        /// Number of comments generated.
        /// </summary>
        public int Comments { get; set; }

        /// <summary>
        /// Number of votes generated.
        /// </summary>
        public int Votes { get; set; }

        /// <summary>
        /// Number of reports generated.
        /// </summary>
        public int Reports { get; set; }

        /// <summary>
        /// Number of giveaway events generated.
        /// </summary>
        public int DropEvents { get; set; }

        /// <summary>
        /// Number of giveaway participants generated.
        /// </summary>
        public int DropParticipants { get; set; }

        /// <summary>
        /// Human-readable summary message.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
