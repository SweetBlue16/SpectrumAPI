using Spectrum.API.Dtos.External;
using Spectrum.API.Exceptions;

namespace Spectrum.API.Services.External
{
    public interface IGameService
    {
        Task<IEnumerable<RawgGameDto>> SearchGamesAsync(GameQueyDto queryDto);
        Task<RawgGameDto> GetGameDetailsAsync(int externalGameId);
    }

    /// <summary>
    /// Service implementation for communicating with the RAWG Video Games Database API.
    /// </summary>
    public class GameService : IGameService
    {
        /// <summary>
        /// HTTP client configured with the RAWG base address.
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Private API key stored in secure configuration.
        /// </summary>
        private readonly string _rawgApiKey;

        private readonly ILogger<GameService> _logger;

        private const int DefaultPageSize = 20;
        private const string GamesEndpoint = "games";

        public GameService(HttpClient httpClient, IConfiguration configuration, ILogger<GameService> logger)
        {
            _httpClient = httpClient;
            _rawgApiKey = configuration["RawgApi:ApiKey"] 
                ?? throw new ArgumentNullException("RawgApi:ApiKey is not configured.");
            _logger = logger;
        }

        /// <summary>
        /// Retrieves extensive details for a specific game using its external ID.
        /// </summary>
        /// <param name="externalGameId">The ID assigned by the RAWG provider.</param>
        /// <returns>Detailed information about the requested game.</returns>
        /// <exception cref="SpectrumNotFoundException">Thrown if the game does not exist in the external catalog.</exception>
        public async Task<RawgGameDto> GetGameDetailsAsync(int externalGameId)
        {
            try
            {
                var requestUrl = $"{GamesEndpoint}/{externalGameId}?key={_rawgApiKey}";
                var response = await _httpClient.GetFromJsonAsync<RawgGameDto>(requestUrl);
                if (response == null)
                {
                    throw new SpectrumNotFoundException("resourceNotFound");
                }
                return response;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to get game details for external game ID: {ExternalGameId}", externalGameId);
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new SpectrumNotFoundException("resourceNotFound");
                }
                throw new SpectrumBusinessException("externalCatalogUnavailable");
            }
        }

        /// <summary>
        /// Searches for video games based on specified filters and keywords.
        /// </summary>
        /// <param name="queryDto">Parameters for searching, filtering, and pagination.</param>
        /// <returns>A collection of games matching the criteria. Follows the Empty Object Pattern.</returns>
        /// <exception cref="SpectrumBusinessException">Thrown if the external service is unreachable (503).</exception>
        public async Task<IEnumerable<RawgGameDto>> SearchGamesAsync(GameQueyDto queryDto)
        {
            try
            {
                var queryParams = new Dictionary<string, string>
                {
                    ["key"] = _rawgApiKey,
                    ["search"] = queryDto.Search,
                    ["platforms"] = queryDto.Platforms,
                    ["genres"] = queryDto.Genres,
                    ["ordering"] = queryDto.Ordering,
                    ["page_size"] = queryDto.PageSize.ToString() ?? DefaultPageSize.ToString(),
                    ["page"] = queryDto.Page.ToString()
                };

                var queryString = string.Join("&", queryParams
                    .Where(p => !string.IsNullOrEmpty(p.Value))
                    .Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}"));
                var response = await _httpClient.GetFromJsonAsync<RawgResponseDto>($"{GamesEndpoint}?{queryString}");
                //return (IEnumerable<RawgGameDto>)(response ?? new RawgResponseDto { Results = new List<RawgGameDto>() });
                return response?.Results ?? Enumerable.Empty<RawgGameDto>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to search games with RAWG API");
                throw new SpectrumBusinessException("externalCatalogUnavailable");
            }
        }
    }
}
