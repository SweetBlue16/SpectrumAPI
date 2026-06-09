using Spectrum.API.Dtos.External;
using Spectrum.API.Models;
using System.Text.Json;

namespace Spectrum.API.Repositories
{
    /// <summary>
    /// Defines the contract for accessing and searching the local game catalog stored in memory.
    /// This repository acts as a bridge between the physical JSON storage and the API controllers.
    /// </summary>
    public interface IGameRepository
    {
        /// <summary>
        /// Retrieves the entire collection of games currently loaded in the cache.
        /// </summary>
        /// <returns>A collection of all games from the local snapshot.</returns>
        IEnumerable<Game> GetAll();

        /// <summary>
        /// Searches for games whose titles match the specified search term.
        /// </summary>
        /// <param name="query">The text to search for within game titles.</param>
        /// <returns>A collection of games matching the query, usually limited to a specific amount for performance.</returns>
        (IEnumerable<Game> Items, int TotalCount) Search(GameQueryDto query);

        /// <summary>
        /// Retrieves a specific game using its unique identifier from the RAWG API.
        /// </summary>
        /// <param name="id">The unique RAWG ID of the game.</param>
        /// <returns>The corresponding Game object if found; otherwise, null.</returns>
        Game? GetById(int id);

        /// <summary>
        /// Retrieves a specific game using its relational database unique identifier.
        /// </summary>
        /// <param name="id">The GUID primary key of the game.</param>
        /// <returns>The corresponding Game object if found; otherwise, null.</returns>
        Game? GetByGuid(Guid id);
    }

    /// <summary>
    /// Implementation of the game repository that uses an in-memory list 
    /// loaded from a JSON snapshot as its data source.
    /// </summary>
    public class GameRepository : IGameRepository
    {
        private readonly List<Game> _games = new();
        private readonly ILogger<GameRepository> _logger;
        private readonly string _dataPath;

        public GameRepository(IWebHostEnvironment env, ILogger<GameRepository> logger)
        {
            _logger = logger;
            _dataPath = Path.Combine(env.ContentRootPath, "Data", "games_snapshot.json");

            LoadData();
        }

        private void LoadData()
        {
            try
            {
                if (!File.Exists(_dataPath))
                {
                    _logger.LogWarning("Repository: Snapshot file not found at {Path}", _dataPath);
                    return;
                }

                var json = File.ReadAllText(_dataPath);
                var items = JsonSerializer.Deserialize<List<Game>>(json);

                if (items != null)
                {
                    _games.Clear();
                    _games.AddRange(items);
                    _logger.LogInformation("Repository: {Count} games loaded into memory successfully.", _games.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Repository: Critical error loading JSON data.");
            }
        }

        /// <inheritdoc/>
        public IEnumerable<Game> GetAll() => _games;

        /// <inheritdoc/>
        public (IEnumerable<Game> Items, int TotalCount) Search(GameQueryDto query)
        {
            var result = _games.AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                result = result.Where(g => g.Title.Contains(query.Search, StringComparison.OrdinalIgnoreCase));
            }

            int totalFiltered = result.Count();

            result = query.Ordering switch
            {
                "name" => result.OrderBy(g => g.Title),
                "-name" => result.OrderByDescending(g => g.Title),
                "released" => result.OrderBy(g => g.ReleaseDate),
                "-released" => result.OrderByDescending(g => g.ReleaseDate),
                "rating" => result.OrderBy(g => g.InternalRating),
                "-rating" => result.OrderByDescending(g => g.InternalRating),
                _ => result.OrderByDescending(g => g.ReleaseDate)
            };

            int pageSize = query.PageSize ?? 42; 
            int skip = (query.Page - 1) * pageSize;
            var pagedItems = result.Skip(skip).Take(pageSize).ToList();

            return (pagedItems, totalFiltered);
        }

        /// <inheritdoc/>
        public Game? GetById(int id) => _games.FirstOrDefault(g => g.RawgId == id);

        /// <inheritdoc/>
        public Game? GetByGuid(Guid id) => _games.FirstOrDefault(g => g.Id == id);
    }
}