using Microsoft.EntityFrameworkCore;
using Spectrum.API.Data;
using Spectrum.API.Dtos.Profile;
using Spectrum.API.Models;
using Spectrum.API.Utilities;

namespace Spectrum.API.Repositories
{
    /// <summary>
    /// Defines the contract for data access operations related to the User entity.
    /// </summary>
    public interface IUserRepository
    {
        /// <summary>
        /// Checks if an email address is already present in the database.
        /// </summary>
        /// <returns>True if the email exists, false otherwise.</returns>
        Task<bool> EmailExistsAsync(string email);

        /// <summary>
        /// Checks if a username is already present in the database.
        /// </summary>
        /// <returns>True if the username exists, false otherwise.</returns>
        Task<bool> UsernameExistsAsync(string username);

        /// <summary>
        /// Retrieves a user record by their registered email address.
        /// </summary>
        /// <returns>The matching <see cref="User"/> entity, or null if no user is found.</returns>
        Task<User?> GetUserByEmailAsync(string email);

        Task<User?> GetUserByEmailIncludingDeletedAsync(string email);

        /// <summary>
        /// Retrieves a user record by their unique system identifier.
        /// </summary>
        /// <returns>The matching <see cref="User"/> entity, or null if no user is found.</returns>
        Task<User?> GetUserByIdAsync(Guid id);

        Task<User?> GetUserByIdForModerationAsync(Guid id);

        Task<IReadOnlyDictionary<Guid, PublicUserSummaryDto>> GetPublicUsersByIdsAsync(
            IEnumerable<Guid> userIds,
            CancellationToken cancellationToken = default
        );

        /// <summary>
        /// Retrieves a user record including its related interested games and platforms.
        /// </summary>
        Task<User?> GetUserWithProfileDataAsync(Guid id);

        /// <summary>
        /// Updates an existing user record in the database.
        /// </summary>
        Task UpdateUserAsync(User user);

        /// <summary>
        /// Persists a newly created user record to the database.
        /// </summary>
        /// <returns>The saved <see cref="User"/> entity, including any database-generated fields.</returns>
        Task<User> AddUserAsync(User user);

        /// <summary>
        /// Retrieves a paginated list of users, optionally filtered by a search term that matches the username or email.
        /// </summary>
        /// <param name="page">The page number to retrieve.</param>
        /// <param name="pageSize">The number of items per page.</param>
        /// <param name="searchTerm">The term to search for in usernames or emails.</param>
        /// <param name="status">Optional moderation status filter: ACTIVE, SUSPENDED, BANNED, DELETED, or ALL.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A paged result containing the matching users.</returns>
        Task<PagedResult<User>> GetPaginatedUsersAsync(int page, int pageSize, string? searchTerm, string? status = null, CancellationToken cancellationToken = default);

        Task<PagedResult<User>> GetPaginatedUsersAsync(int page, int pageSize, string? searchTerm, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the total number of reviews published by a specific user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
        /// <returns>The total count of reviews.</returns>
        Task<int> GetTotalReviewsCountAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the total number of game clips uploaded by a specific user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
        /// <returns>The total count of game clips.</returns>
        Task<int> GetTotalClipsCountAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a user including their interested games and platforms tracking collection state.
        /// </summary>
        /// <returns>The user entity with initialized navigation collections, or null if not found.</returns>
        Task<User?> GetUserWithCollectionsAsync(Guid userId);

        /// <summary>
        /// Synchronizes the many-to-many collections for interested games and platforms, and persists changes.
        /// </summary>
        /// <param name="user">The user entity context instance to synchronize.</param>
        /// <param name="incomingGameIds">The definitive list of game IDs the user is interested in.</param>
        /// <param name="incomingPlatformIds">The definitive list of platform IDs the user plays on.</param>
        Task UpdateUserProfileCollectionsAsync(User user, List<Guid> incomingGameIds, List<int> incomingPlatformIds);
    }

    /// <summary>
    /// Implementation of the <see cref="IUserRepository"/> using Entity Framework Core.
    /// </summary>
    public class UserRepository : IUserRepository
    {
        private readonly SpectrumDbContext _context;
        private readonly IGameRepository _gameRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserRepository"/> class.
        /// </summary>
        /// <param name="context">The Entity Framework database context.</param>
        /// <param name="gameRepository">The game repository.</param>
        public UserRepository(SpectrumDbContext context, IGameRepository gameRepository)
        {
            _context = context;
            _gameRepository = gameRepository;
        }

        /// <inheritdoc />
        public async Task<User> AddUserAsync(User user)
        {
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
            return user;
        }

        /// <inheritdoc />
        public async Task UpdateUserAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        /// <inheritdoc />
        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _context.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Email == email);
        }

        /// <inheritdoc />
        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<User?> GetUserByEmailIncludingDeletedAsync(string email)
        {
            return await _context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        /// <inheritdoc />
        public async Task<User?> GetUserByIdAsync(Guid id)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
        }

        public async Task<User?> GetUserByIdForModerationAsync(Guid id)
        {
            return await _context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<IReadOnlyDictionary<Guid, PublicUserSummaryDto>> GetPublicUsersByIdsAsync(
            IEnumerable<Guid> userIds,
            CancellationToken cancellationToken = default
        )
        {
            var distinctUserIds = userIds
                .Where(userId => userId != Guid.Empty)
                .Distinct()
                .ToArray();

            if (distinctUserIds.Length == 0)
            {
                return new Dictionary<Guid, PublicUserSummaryDto>();
            }

            return await _context.Users
                .AsNoTracking()
                .Where(user => distinctUserIds.Contains(user.Id))
                .Select(user => new PublicUserSummaryDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    ProfilePicture = user.ProfilePicture ?? string.Empty
                })
                .ToDictionaryAsync(user => user.Id, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<User?> GetUserWithProfileDataAsync(Guid id)
        {
            return await _context.Users
                .Include(u => u.InterestedGames)
                .Include(u => u.Platforms)
                .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
        }

        /// <inheritdoc />
        public async Task<bool> UsernameExistsAsync(string username)
        {
            return await _context.Users.AnyAsync(u => u.Username == username);
        }

        /// <inheritdoc />
        public async Task<PagedResult<User>> GetPaginatedUsersAsync(int page, int pageSize, string? searchTerm, string? status = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Users.IgnoreQueryFilters().AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var normalizedSearch = searchTerm.Trim().ToLower();
                query = query.Where(u =>
                    u.Username.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    u.Email.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
            }

            query = NormalizeModerationStatus(status) switch
            {
                "ACTIVE" => query.Where(user => !user.IsDeleted && !user.IsBanned && !user.IsSuspended),
                "SUSPENDED" => query.Where(user => !user.IsDeleted && !user.IsBanned && user.IsSuspended),
                "BANNED" => query.Where(user => !user.IsDeleted && user.IsBanned),
                "DELETED" => query.Where(user => user.IsDeleted),
                _ => query
            };

            var totalItems = await query.CountAsync(cancellationToken);
            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<User>
            {
                Items = users,
                TotalCount = totalItems,
                Page = page,
                PageSize = pageSize
            };
        }

        public Task<PagedResult<User>> GetPaginatedUsersAsync(int page, int pageSize, string? searchTerm, CancellationToken cancellationToken = default)
        {
            return GetPaginatedUsersAsync(page, pageSize, searchTerm, status: null, cancellationToken);
        }

        private static string? NormalizeModerationStatus(string? status)
        {
            return string.IsNullOrWhiteSpace(status) || status.Equals("ALL", StringComparison.OrdinalIgnoreCase)
                ? null
                : status.Trim().ToUpperInvariant();
        }

        /// <inheritdoc />
        public async Task<int> GetTotalReviewsCountAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _context.Reviews
                .CountAsync(r => r.UserId == userId, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<int> GetTotalClipsCountAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _context.GameClips
                .CountAsync(c => c.UserId == userId, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<User?> GetUserWithCollectionsAsync(Guid userId)
        {
            return await _context.Users
                .Include(u => u.InterestedGames)
                .Include(u => u.Platforms)
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        /// <inheritdoc />
        public async Task UpdateUserProfileCollectionsAsync(User user, List<Guid> incomingGameIds, List<int> incomingPlatformIds)
        {
            await SyncInterestedGamesAsync(user, incomingGameIds);
            await SyncPlatformsAsync(user, incomingPlatformIds);
            
            await _context.SaveChangesAsync();
        }


        private async Task SyncInterestedGamesAsync(User user, List<Guid> incomingGameIds)
        {
            var gamesToRemove = user.InterestedGames.Where(g => !incomingGameIds.Contains(g.Id)).ToList();

            foreach (var game in gamesToRemove)
                user.InterestedGames.Remove(game);

            var existingGameIds = user.InterestedGames.Select(g => g.Id).ToHashSet();
            var gamesToAddIds = incomingGameIds.Except(existingGameIds).ToList();

            foreach (var gameId in gamesToAddIds)
            {
                var gameToAdd = await ResolveAndTrackGameAsync(gameId);

                if (gameToAdd != null)
                    user.InterestedGames.Add(gameToAdd);
            }

        }

        private async Task SyncPlatformsAsync(User user, List<int> incomingPlatformIds)
        {
            var platformsToRemove = user.Platforms.Where(p => !incomingPlatformIds.Contains(p.Id)).ToList();
            foreach (var platform in platformsToRemove)
            {
                user.Platforms.Remove(platform);
            }

            var existingPlatformIds = user.Platforms.Select(p => p.Id).ToHashSet();
            var platformsToAddIds = incomingPlatformIds.Except(existingPlatformIds).ToList();

            if (platformsToAddIds.Count > 0)
            {
                var platformsToAdd = await _context.Platforms
                    .Where(p => platformsToAddIds.Contains(p.Id))
                    .ToListAsync();

                foreach (var platform in platformsToAdd)
                {
                    user.Platforms.Add(platform);
                }
            }
        }

        /// <summary>
        /// Search games. Look at EF caché, then BD, if it not exists, look in the in-memory catalog and prepare it for insertion.
        /// </summary>
        private async Task<Game?> ResolveAndTrackGameAsync(Guid gameId)
        {
            var localGame = _context.Games.Local.FirstOrDefault(g => g.Id == gameId);
            if (localGame != null) return localGame;

            var dbGame = await _context.Games.FirstOrDefaultAsync(g => g.Id == gameId);
            if (dbGame != null) return dbGame;

            var gameFromCatalog = _gameRepository.GetByGuid(gameId);
            if (gameFromCatalog is null) return null;

            _context.Games.Add(gameFromCatalog); 
            return gameFromCatalog;
        }
    }
}
