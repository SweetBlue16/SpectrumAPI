using Spectrum.API.Dtos.Media;
using Spectrum.API.Dtos.Votes;
using Spectrum.API.Exceptions;
using Spectrum.API.Models;
using Spectrum.API.Repositories;
using Spectrum.API.Services.Email;
using Spectrum.API.Utilities;

namespace Spectrum.API.Services.Clips
{
    /// <summary>
    /// Defines the contract for managing game clips.
    /// Handles the business logic for creating, retrieving, and deleting clips in the database.
    /// </summary>
    public interface IGameClipService
    {
        /// <summary>
        /// Creates a new game clip record in the database after a successful upload to AWS S3.
        /// </summary>
        /// <param name="userId">The unique identifier of the authenticated user.</param> 
        /// <param name="request">The request payload containing metadata about the video upload.</param>
        /// <param name="videoUrl">The public cloud URL where the raw video file is stored.</param>
        Task CreateClipAsync(Guid userId, CompleteUploadRequestDto request, string videoUrl);

        /// <summary>
        /// Retrieves all game clips belonging to a specific user.
        /// </summary>
        /// <returns>A collection of <see cref="GameClipSummaryDto"/> optimized for view representations.</returns>
        Task<IEnumerable<GameClipSummaryDto>> GetClipsByUserIdAsync(Guid userId);

        Task<VoteResultDto> CastClipVoteAsync(Guid clipId, Guid userId, bool isPositive);

        /// <summary>
        /// Deletes a specific game clip from active queries using a soft-delete flag.
        /// </summary>
        Task DeleteClipAsync(Guid clipId, Guid userId);
    }

    /// <summary>
    /// Implementation of <see cref="IGameClipService"/> for managing game clips.
    /// Operates completely decoupled from the infrastructure layer through repositories.
    /// </summary>
    public class GameClipService : IGameClipService
    {
        private readonly IGameClipRepository _clipRepository;
        private readonly IUserRepository _userRepository;
        private readonly IGameRepository _gameRepository;
        private readonly IEmailService? _emailService;
        private readonly ILogger<GameClipService>? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GameClipService"/> class.
        /// </summary>
        /// <param name="clipRepository">The repository abstracted for data access operations related to game clips.</param>
        /// <param name="userRepository">The repository abstracted for user records and authorization lookups.</param>
        /// <param name="gameRepository">The central memory repository acting as the snapshot catalog cache.</param>
        /// <param name="emailService">Optional transactional email service used for moderation notices.</param>
        /// <param name="logger">Optional structured logger used when non-blocking notifications fail.</param>
        public GameClipService(
            IGameClipRepository clipRepository,
            IUserRepository userRepository,
            IGameRepository gameRepository,
            IEmailService? emailService = null,
            ILogger<GameClipService>? logger = null)
        {
            _clipRepository = clipRepository;
            _userRepository = userRepository;
            _gameRepository = gameRepository;
            _emailService = emailService;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task CreateClipAsync(Guid userId, CompleteUploadRequestDto request, string videoUrl)
        {
            var title = request.Title.Trim();
            var description = request.Description?.Trim() ?? string.Empty;
            if (title.Length is < 3 or > 100)
            {
                throw new SpectrumBusinessException("El título del clip debe tener entre 3 y 100 caracteres.");
            }

            if (description.Length > 500)
            {
                throw new SpectrumBusinessException("La descripción del clip no puede exceder 500 caracteres.");
            }

            Guid stableGameGuid = Spectrum.API.Utilities.GameMappingUtilities.GenerateDeterministicGuid(request.GameId);

            var gameExists = await _clipRepository.GameExistsAsync(stableGameGuid);

            if (!gameExists)
            {
                var gameFromCatalog = _gameRepository.GetById(request.GameId);

                if (gameFromCatalog == null)
                {
                    throw new SpectrumNotFoundException("The specified game was not found in the catalog cache.");
                }

                gameFromCatalog.Id = stableGameGuid;
                await _clipRepository.AddGameAsync(gameFromCatalog);
                await _clipRepository.SaveChangesAsync();
            }

            var newClip = new GameClip
            {
                UserId = userId,
                GameId = stableGameGuid,
                Title = title,
                Description = description,
                Url = videoUrl,
                CreatedAt = DateTime.UtcNow
            };

            await _clipRepository.AddClipAsync(newClip);
            await _clipRepository.SaveChangesAsync();
        }

        /// <inheritdoc />
        public async Task<IEnumerable<GameClipSummaryDto>> GetClipsByUserIdAsync(Guid userId)
        {
            var clips = (await _clipRepository.GetClipsByUserIdAsync(userId)).ToList();
            var clipIds = clips.Select(clip => clip.Id).ToArray();
            var counts = await _clipRepository.GetVoteCountsByClipIdsAsync(clipIds);
            var userVotes = await _clipRepository.GetUserVotesByClipIdsAsync(clipIds, userId);

            return clips.Select(c => new GameClipSummaryDto
            {
                Id = c.Id,
                Title = c.Title,
                ThumbnailUrl = c.Game?.CoverImageUrl,
                GameName = c.Game?.Title,
                Url = c.Url,
                LikesCount = counts.TryGetValue(c.Id, out var count) ? count.Likes : 0,
                DislikesCount = counts.TryGetValue(c.Id, out count) ? count.Dislikes : 0,
                UserVote = userVotes.TryGetValue(c.Id, out var vote) ? vote : null,
                UserId = c.UserId,
                CreatedAt = c.CreatedAt
            });
        }

        /// <inheritdoc />
        public async Task<VoteResultDto> CastClipVoteAsync(Guid clipId, Guid userId, bool isPositive)
        {
            var clip = await _clipRepository.GetClipByIdAsync(clipId);

            if (clip is null)
            {
                throw new SpectrumNotFoundException("The requested clip was not found.");
            }

            if (clip.UserId == userId)
            {
                throw new SpectrumForbiddenException("No puedes votar tu propio clip.");
            }

            var existingVote = await _clipRepository.GetVoteAsync(clipId, userId);

            if (existingVote is null)
            {
                await _clipRepository.AddVoteAsync(new GameClipVote
                {
                    ClipId = clipId,
                    UserId = userId,
                    IsPositive = isPositive
                });
            }
            else if (existingVote.IsPositive == isPositive)
            {
                await _clipRepository.DeleteVoteAsync(existingVote);
            }
            else
            {
                existingVote.IsPositive = isPositive;
                existingVote.UpdatedAt = DateTime.UtcNow;
            }

            await _clipRepository.SaveChangesAsync();
            var counts = await _clipRepository.GetVoteCountsByClipIdsAsync(new[] { clipId });
            var count = counts.TryGetValue(clipId, out var resolvedCount) ? resolvedCount : (Likes: 0, Dislikes: 0);

            return new VoteResultDto
            {
                Success = true,
                UpdatedLikes = count.Likes,
                UpdatedDislikes = count.Dislikes
            };
        }

        /// <inheritdoc />
        public async Task DeleteClipAsync(Guid clipId, Guid userId)
        {
            var clip = await _clipRepository.GetClipByIdAsync(clipId);

            if (clip == null)
            {
                throw new SpectrumNotFoundException("The requested clip was not found.");
            }

            var isAdminDelete = await ValidateDeletionPermissionAsync(clip, userId);

            await _clipRepository.DeleteClipAsync(clip, userId);
            await _clipRepository.SaveChangesAsync();
            if (isAdminDelete)
            {
                await TrySendClipDeletedEmailAsync(clip);
            }
        }

        /// <summary>
        /// Evaluates identity and security constraints to determine if a removal operation can take place.
        /// </summary>
        private async Task<bool> ValidateDeletionPermissionAsync(GameClip clip, Guid userId)
        {
            if (clip.UserId == userId)
            {
                return false;
            }

            var requestingUser = await _userRepository.GetUserByIdAsync(userId);

            if (requestingUser?.Role != Constants.Roles.Admin)
            {
                throw new SpectrumForbiddenException("You do not have permission to delete this clip.");
            }

            return true;
        }

        private async Task TrySendClipDeletedEmailAsync(GameClip clip)
        {
            if (_emailService is null || string.IsNullOrWhiteSpace(clip.User?.Email))
            {
                return;
            }

            try
            {
                await _emailService.SendClipDeletedAsync(clip.User.Email, clip.Title);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not send clip deletion email for clip {ClipId}", clip.Id);
            }
        }
    }
}
