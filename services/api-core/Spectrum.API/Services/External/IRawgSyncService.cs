using System.Text.Json;
using Spectrum.API.Dtos.External;
using Spectrum.API.Models;
using Spectrum.API.Utilities;

namespace Spectrum.API.Services.External
{
    /// <summary>
    /// Defines the contract for synchronizing the local game catalog with the external RAWG API.
    /// </summary>
    public interface IRawgSyncService
    {
        /// <summary>
        /// Synchronizes the catalog. 
        /// If fullSync is true, fetches the high-quality filtered catalog. 
        /// If false, fetches only recent updates from the last 7 days.
        /// </summary>
        Task SyncCatalogAsync(bool fullSync = false);
    }

    /// <summary>
    /// Service implementation that handles the extraction, filtering, and transformation 
    /// of RAWG data into a local JSON snapshot.
    /// </summary>
    public class RawgSyncService : IRawgSyncService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _dataFilePath;
        private readonly ILogger<RawgSyncService> _logger;

        private static readonly JsonSerializerOptions _intendedJsonOptions = new() { WriteIndented = true };

        public RawgSyncService(
            HttpClient httpClient,
            IConfiguration configuration,
            IWebHostEnvironment env,
            ILogger<RawgSyncService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            _apiKey = configuration["RawgApi:ApiKey"]
                ?? throw new ArgumentNullException("RAWG ApiKey not found in configuration.");

            _dataFilePath = Path.Combine(env.ContentRootPath, "Data", "games_snapshot.json");
        }

        /// <inheritdoc />
        public async Task SyncCatalogAsync(bool fullSync = false)
        {
            var existingGames = await LoadExistingGamesAsync();
            var newGamesCount = 0;

            string? nextUrl = BuildRequestUrl(fullSync);

            while (!string.IsNullOrEmpty(nextUrl))
            {
                var response = await _httpClient.GetFromJsonAsync<RawgResponseDto>(nextUrl);

                if (response?.Results != null)
                {
                    var validNewGames = response.Results
                        .Where(item => item.RatingsCount >= 50 && !existingGames.Any(g => g.RawgId == item.Id))
                        .Select(GameMappingUtilities.MapToInternalModel)
                        .ToList();

                    existingGames.AddRange(validNewGames);
                    newGamesCount += validNewGames.Count;

                    _logger.LogInformation("[SPECTRUM API] Quality games processed so far: {Total}", existingGames.Count);
                    nextUrl = response.Next;
                }
                else
                {
                    nextUrl = null;
                }

                await Task.Delay(250);
                if (!fullSync && newGamesCount > 100) break;
            }

            await SaveSnapshotAsync(existingGames);
            _logger.LogInformation("Sync finished. Added {Count} relevant games.", newGamesCount);
        }

        /// <summary>
        /// Build base URL to consult RAWG by type of sync.
        /// </summary>
        private string BuildRequestUrl(bool fullSync)
        {
            // API PARAMETERS:
            // exclude_additions=true -> Removes DLCs and soundtracks.
            // metacritic=30,100 -> Filters only games with professional reviews.
            // ordering=-added -> Brings the most popular/owned games first.
            string urlParams = $"key={_apiKey}&page_size=40&exclude_additions=true&metacritic=30,100&ordering=-added";

            if (!fullSync)
            {
                var startDate = DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd");
                var endDate = DateTime.Now.ToString("yyyy-MM-dd");
                urlParams += $"&dates={startDate},{endDate}";
            }

            return $"https://api.rawg.io/api/games?{urlParams}";
        }

        private async Task<List<Game>> LoadExistingGamesAsync()
        {
            if (!File.Exists(_dataFilePath)) return new List<Game>();
            var json = await File.ReadAllTextAsync(_dataFilePath);
            return JsonSerializer.Deserialize<List<Game>>(json) ?? new List<Game>();
        }

        /// <summary>
        /// Serializes and saves the game list to a physical JSON file in the Data folder.
        /// </summary>
        /// <param name="games">The list of mapped games to persist.</param>
        private async Task SaveSnapshotAsync(List<Game> games)
        {
            var directory = Path.GetDirectoryName(_dataFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(games, _intendedJsonOptions);

            await File.WriteAllTextAsync(_dataFilePath, json);
            _logger.LogInformation("Catalog snapshot successfully saved to {Path}", _dataFilePath);
        }
    }
}