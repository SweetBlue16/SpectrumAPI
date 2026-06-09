using Microsoft.EntityFrameworkCore;
using Spectrum.API.Data;
using Spectrum.API.Dtos.External;
using Spectrum.API.Dtos.Search;
using Spectrum.API.Repositories;

namespace Spectrum.API.Services.Search
{
    /// <summary>
    /// Defines the contract for performing global searches across multiple Spectrum entities.
    /// Supports federated searches over the internal game catalog and registered user profiles.
    /// </summary>
    public interface IGlobalSearchService
    {
        /// <summary>
        /// Executes a global search operation using the provided query string.
        /// </summary>
        /// <param name="query">The search term entered by the user.</param>
        /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
        /// <returns>
        /// A <see cref="GlobalSearchResultDto"/> containing grouped search results
        /// for supported entity types such as games and users.
        /// </returns>
        Task<GlobalSearchResultDto> SearchAsync(string query, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Service implementation responsible for executing global searches across
    /// the Spectrum platform data sources.
    /// Aggregates results from the in-memory game catalog and the user database
    /// into a unified response structure.
    /// </summary>
    public class GlobalSearchService : IGlobalSearchService
    {
        private const int ResultLimit = 5;
        private readonly SpectrumDbContext _context;
        private readonly IGameRepository _gameRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalSearchService"/> class.
        /// </summary>
        /// <param name="context">
        /// The database context used to query user profile information.
        /// </param>
        /// <param name="gameRepository">
        /// The repository providing access to the internal video game catalog.
        /// </param>
        public GlobalSearchService(SpectrumDbContext context, IGameRepository gameRepository)
        {
            _context = context;
            _gameRepository = gameRepository;
        }

        /// <inheritdoc/>
        public async Task<GlobalSearchResultDto> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            var normalized = (query ?? string.Empty).Trim();
            if (normalized.Length < 2)
            {
                return new GlobalSearchResultDto();
            }

            var games = _gameRepository.Search(new GameQueryDto
            {
                Search = normalized,
                Page = 1,
                PageSize = ResultLimit,
                Ordering = "name"
            }).Items.Select(game => new GlobalSearchItemDto
            {
                Type = "game",
                Id = game.RawgId.ToString(),
                Title = game.Title,
                Subtitle = game.ReleaseDate?.Year.ToString(),
                ImageUrl = game.CoverImageUrl
            }).ToList();

            var lowered = normalized.ToLowerInvariant();
            var users = await _context.Users
                .AsNoTracking()
                .Where(user => user.Username.Contains(lowered, StringComparison.OrdinalIgnoreCase) ||
                               user.Email.Contains(lowered, StringComparison.OrdinalIgnoreCase))
                .OrderBy(user => user.Username)
                .Take(ResultLimit)
                .Select(user => new GlobalSearchItemDto
                {
                    Type = "user",
                    Id = user.Id.ToString(),
                    Title = user.Username,
                    Subtitle = "Perfil de jugador",
                    ImageUrl = user.ProfilePicture
                })
                .ToListAsync(cancellationToken);

            return new GlobalSearchResultDto
            {
                Games = games,
                Users = users
            };
        }
    }
}
