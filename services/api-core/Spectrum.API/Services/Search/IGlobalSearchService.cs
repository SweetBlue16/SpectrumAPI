using Microsoft.EntityFrameworkCore;
using Spectrum.API.Data;
using Spectrum.API.Dtos.External;
using Spectrum.API.Dtos.Search;
using Spectrum.API.Repositories;

namespace Spectrum.API.Services.Search
{
    public interface IGlobalSearchService
    {
        Task<GlobalSearchResultDto> SearchAsync(string query, CancellationToken cancellationToken = default);
    }

    public class GlobalSearchService : IGlobalSearchService
    {
        private const int ResultLimit = 5;
        private readonly SpectrumDbContext _context;
        private readonly IGameRepository _gameRepository;

        public GlobalSearchService(SpectrumDbContext context, IGameRepository gameRepository)
        {
            _context = context;
            _gameRepository = gameRepository;
        }

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
