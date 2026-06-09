using Spectrum.API.Dtos.Drops;

namespace Spectrum.API.Dtos.Home
{
    /// <summary>
    /// Represents the data displayed on the application's home dashboard.
    /// </summary>
    public class HomeDashboardDto
    {
        /// <summary>
        /// Main banner title displayed on the home page.
        /// </summary>
        public string BannerTitle { get; set; } = "SPECTRUM";

        /// <summary>
        /// Subtitle displayed below the main dashboard banner.
        /// </summary>
        public string BannerSubtitle { get; set; } = "Descubre juegos, reseñas y sorteos activos.";

        /// <summary>
        /// Recently released or recently added games.
        /// </summary>
        public IReadOnlyList<HomeGameDto> RecentGames { get; set; } = [];

        /// <summary>
        /// Most popular reviews published during the current day.
        /// </summary>
        public IReadOnlyList<HomeReviewDto> PopularReviewsToday { get; set; } = [];

        /// <summary>
        /// Current and upcoming drops scheduled for the next seven days.
        /// </summary>
        public IReadOnlyList<EventStatusDto> WeeklyDrops { get; set; } = [];
    }
}
