using Spectrum.API.Dtos.Reviews;
using Spectrum.API.Exceptions;
using Spectrum.API.Models;
using Spectrum.API.Repositories;
using Spectrum.API.Services.Admin;
using Spectrum.API.Services.Email;
using Spectrum.API.Services.Votes;

namespace Spectrum.API.Services.Reviews
{
    /// <summary>
    /// Defines the contract for review management operations, including creation,
    /// retrieval, updates, and deletion.
    /// </summary>
    public interface IReviewService
    {
        /// <summary>
        /// Creates a new review after validating game identifier,
        /// rating, textual content, and optional media attachment data.
        /// </summary>
        /// <param name="dto">Review creation request data.</param>
        /// <param name="userId">Identifier of the author creating the review.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The newly created review mapped to a response DTO.</returns>
        /// <exception cref="SpectrumBusinessException">
        /// Thrown when any validation rule is violated.
        /// </exception>
        Task<ReviewResponseDto> CreateAsync(
            CreateReviewDto dto,
            Guid userId,
            CancellationToken cancellationToken = default
        );

        /// <summary>
        /// Retrieves a review by its identifier and enriches the result
        /// with the current user's voting information when available.
        /// </summary>
        /// <param name="reviewId">Review identifier.</param>
        /// <param name="currentUserId">
        /// Optional identifier of the requesting user.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The requested review.</returns>
        /// <exception cref="SpectrumNotFoundException">
        /// Thrown when the review does not exist.
        /// </exception>
        Task<ReviewResponseDto> GetByIdAsync(
            Guid reviewId,
            Guid? currentUserId = null,
            CancellationToken cancellationToken = default
        );

        /// <summary>
        /// Retrieves all reviews associated with a specific game and
        /// enriches each review with vote information and moderation flags.
        /// </summary>
        /// <param name="gameId">Game identifier.</param>
        /// <param name="currentUserId">
        /// Optional identifier of the requesting user.
        /// </param>
        /// <param name="isAdmin">
        /// Indicates whether the requester has administrative privileges.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A collection of reviews for the specified game.</returns>
        /// <exception cref="SpectrumBusinessException">
        /// Thrown when the game identifier is invalid.
        /// </exception>
        Task<IReadOnlyList<ReviewResponseDto>> GetByGameIdAsync(
            int gameId,
            Guid? currentUserId = null,
            bool isAdmin = false,
            CancellationToken cancellationToken = default
        );

        /// <summary>
        /// Retrieves all reviews authored by a specific user and enriches
        /// them with current vote information when available.
        /// </summary>
        /// <param name="userId">Author identifier.</param>
        /// <param name="currentUserId">
        /// Optional identifier of the requesting user.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A collection of reviews created by the specified user.</returns>
        Task<IReadOnlyList<ReviewResponseDto>> GetByUserIdAsync(
            Guid userId,
            Guid? currentUserId = null,
            CancellationToken cancellationToken = default
        );

        /// <summary>
        /// Updates an existing review after validating ownership,
        /// content integrity, rating constraints, and attachment data.
        /// </summary>
        /// <param name="reviewId">Review identifier.</param>
        /// <param name="dto">Review update data.</param>
        /// <param name="userId">Identifier of the requesting user.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="SpectrumNotFoundException">
        /// Thrown when the review does not exist.
        /// </exception>
        /// <exception cref="SpectrumForbiddenException">
        /// Thrown when the user is not the review owner.
        /// </exception>
        /// <exception cref="SpectrumBusinessException">
        /// Thrown when validation rules are violated.
        /// </exception>
        Task UpdateAsync(
            Guid reviewId,
            UpdateReviewDto dto,
            Guid userId,
            CancellationToken cancellationToken = default
        );

        /// <summary>
        /// Performs a soft deletion of a review. When executed by an
        /// administrator, moderation metadata and deletion reasons are
        /// recorded and notification workflows are triggered.
        /// </summary>
        /// <param name="reviewId">Review identifier.</param>
        /// <param name="userId">
        /// Identifier of the user performing the deletion.
        /// </param>
        /// <param name="isAdmin">
        /// Indicates whether the action is performed by an administrator.
        /// </param>
        /// <param name="deletionReason">
        /// Moderation reason for deletion when applicable.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="SpectrumNotFoundException">
        /// Thrown when the review does not exist.
        /// </exception>
        /// <exception cref="SpectrumForbiddenException">
        /// Thrown when the requester lacks permission.
        /// </exception>
        /// <exception cref="SpectrumBusinessException">
        /// Thrown when an administrator deletion does not provide
        /// a valid moderation reason.
        /// </exception>
        Task DeleteAsync(
            Guid reviewId,
            Guid userId,
            bool isAdmin = false,
            string? deletionReason = null,
            CancellationToken cancellationToken = default
        );
    }

    /// <summary>
    /// Service responsible for managing review lifecycle operations,
    /// including creation, retrieval, updates, deletion, validation,
    /// vote enrichment, and moderation-related actions.
    /// </summary>
    public class ReviewService : IReviewService
    {
        private const string ReviewNotFoundMessage = "La resena solicitada no existe.";
        private const string ForbiddenActionMessage = "No tienes permisos para realizar esta accion.";

        private const int MinimumGameId = 1;
        private const int MinimumRating = 5;
        private const int MaximumRating = 10;
        private const int MaximumTitleLength = 120;
        private const int MaximumContentLength = 2000;
        private const int MaximumImageUrlLength = 255;
        private static readonly HashSet<string> AllowedMediaTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image",
            "video"
        };

        private readonly IReviewRepository _reviewRepository;
        private readonly IGameRepository? _gameRepository;
        private readonly IEmailService? _emailService;
        private readonly IAdminNotificationService? _adminNotificationService;
        private readonly IVoteService? _voteService;
        private readonly ILogger<ReviewService>? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReviewService"/> class with
        /// </summary>
        /// <param name="reviewRepository">The repository for managing review data.</param>
        /// <param name="gameRepository">The repository for managing game data.</param>
        /// <param name="emailService">The service for sending email notifications.</param>
        /// <param name="adminNotificationService">The service for sending administrator notifications.</param>
        /// <param name="voteService">The service for managing vote data.</param>
        /// <param name="logger">The logger for recording diagnostic information.</param>
        public ReviewService(
            IReviewRepository reviewRepository,
            IGameRepository? gameRepository = null,
            IEmailService? emailService = null,
            IAdminNotificationService? adminNotificationService = null,
            IVoteService? voteService = null,
            ILogger<ReviewService>? logger = null)
        {
            _reviewRepository = reviewRepository;
            _gameRepository = gameRepository;
            _emailService = emailService;
            _adminNotificationService = adminNotificationService;
            _voteService = voteService;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<ReviewResponseDto> CreateAsync(
            CreateReviewDto dto,
            Guid userId,
            CancellationToken cancellationToken = default
        )
        {
            ValidateGameId(dto.GameId);
            ValidateRating(dto.Rating);
            var title = NormalizeTitle(dto.Title);
            var content = NormalizeContent(dto.Content);
            ValidateImageUrl(dto.ImageUrl);
            ValidateMediaType(dto.ImageUrl, dto.MediaType);

            var review = new Review
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GameId = dto.GameId,
                Rating = dto.Rating,
                Title = title,
                Content = content,
                ImageUrl = dto.ImageUrl,
                MediaType = NormalizeMediaType(dto.MediaType),
                LikesCount = 0,
                DislikesCount = 0,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            var createdReview = await _reviewRepository.AddAsync(review, cancellationToken);
            await _reviewRepository.SaveChangesAsync(cancellationToken);

            var persistedReview = await _reviewRepository.GetByIdAsync(createdReview.Id, cancellationToken);

            return MapToResponseDto(persistedReview ?? createdReview, userId, currentUserVote: null);
        }

        /// <inheritdoc/>
        public async Task<ReviewResponseDto> GetByIdAsync(
            Guid reviewId,
            Guid? currentUserId = null,
            CancellationToken cancellationToken = default
        )
        {
            var review = await _reviewRepository.GetByIdAsync(reviewId, cancellationToken);

            if (review is null)
            {
                throw new SpectrumNotFoundException(ReviewNotFoundMessage);
            }

            var userVotes = await ResolveCurrentUserVotesAsync(new[] { review.Id }, currentUserId, cancellationToken);

            return MapToResponseDto(
                review,
                currentUserId,
                currentUserVote: userVotes.GetValueOrDefault(review.Id)
            );
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ReviewResponseDto>> GetByGameIdAsync(
            int gameId,
            Guid? currentUserId = null,
            bool isAdmin = false,
            CancellationToken cancellationToken = default
        )
        {
            ValidateGameId(gameId);

            var reviews = await _reviewRepository.GetByGameIdAsync(gameId, cancellationToken);

            var userVotes = await ResolveCurrentUserVotesAsync(
                reviews.Select(review => review.Id),
                currentUserId,
                cancellationToken
            );

            return reviews
                .Select(review => MapToResponseDto(
                    review,
                    currentUserId,
                    isAdmin,
                    userVotes.GetValueOrDefault(review.Id)
                ))
                .ToList();
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ReviewResponseDto>> GetByUserIdAsync(
            Guid userId,
            Guid? currentUserId = null,
            CancellationToken cancellationToken = default
        )
        {
            var reviews = await _reviewRepository.GetByUserIdAsync(userId, cancellationToken);

            var userVotes = await ResolveCurrentUserVotesAsync(
                reviews.Select(review => review.Id),
                currentUserId,
                cancellationToken
            );

            return reviews
                .Select(review => MapToResponseDto(
                    review,
                    currentUserId,
                    currentUserVote: userVotes.GetValueOrDefault(review.Id)
                ))
                .ToList();
        }

        /// <inheritdoc/>
        public async Task UpdateAsync(
            Guid reviewId,
            UpdateReviewDto dto,
            Guid userId,
            CancellationToken cancellationToken = default
        )
        {
            var review = await GetExistingReviewAsync(reviewId, cancellationToken);
            EnsureReviewOwner(review, userId);

            if (dto.Rating.HasValue)
            {
                ValidateRating(dto.Rating.Value);
                review.Rating = dto.Rating.Value;
            }

            if (dto.Title is not null)
            {
                review.Title = NormalizeTitle(dto.Title);
            }

            if (dto.Content is not null)
            {
                review.Content = NormalizeContent(dto.Content);
            }

            if (dto.ImageUrl is not null)
            {
                ValidateImageUrl(dto.ImageUrl);
                review.ImageUrl = dto.ImageUrl;
            }

            if (dto.MediaType is not null)
            {
                ValidateMediaType(review.ImageUrl, dto.MediaType);
                review.MediaType = NormalizeMediaType(dto.MediaType);
            }

            review.UpdatedAt = DateTime.UtcNow;

            await _reviewRepository.SaveChangesAsync(cancellationToken);
        }

        private async Task TrySendReviewDeletedEmailAsync(Review review, CancellationToken cancellationToken)
        {
            if (_emailService is null || string.IsNullOrWhiteSpace(review.User?.Email))
            {
                return;
            }

            try
            {
                await _emailService.SendReviewDeletedAsync(review.User.Email, review.Title);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger?.LogWarning(ex, "Could not send review deletion email for review {ReviewId}", review.Id);
            }
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(
            Guid reviewId,
            Guid userId,
            bool isAdmin = false,
            string? deletionReason = null,
            CancellationToken cancellationToken = default
        )
        {
            var review = await GetExistingReviewAsync(reviewId, cancellationToken);
            EnsureReviewOwnerOrAdmin(review, userId, isAdmin);
            var normalizedReason = NormalizeDeletionReason(deletionReason, isAdmin);

            review.IsDeleted = true;
            review.UpdatedAt = DateTime.UtcNow;
            review.DeletedAt = DateTime.UtcNow;
            review.DeletedByAdminId = isAdmin ? userId : null;
            review.DeletionReason = normalizedReason;

            await _reviewRepository.SaveChangesAsync(cancellationToken);
            if (isAdmin)
            {
                if (_adminNotificationService is not null && !string.IsNullOrWhiteSpace(normalizedReason))
                {
                    await _adminNotificationService.NotifyReviewDeletedAsync(review, normalizedReason, cancellationToken);
                }
                else
                {
                    await TrySendReviewDeletedEmailAsync(review, cancellationToken);
                }
            }
        }

        private static string? NormalizeDeletionReason(string? reason, bool isAdmin)
        {
            if (!isAdmin)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new SpectrumBusinessException("El motivo de eliminación es obligatorio.");
            }

            var normalized = reason.Trim();
            if (normalized.Length is < 10 or > 300)
            {
                throw new SpectrumBusinessException("El motivo debe tener entre 10 y 300 caracteres.");
            }

            return normalized;
        }

        private async Task<Review> GetExistingReviewAsync(Guid reviewId, CancellationToken cancellationToken)
        {
            var review = await _reviewRepository.GetByIdAsync(reviewId, cancellationToken);

            if (review is null)
            {
                throw new SpectrumNotFoundException(ReviewNotFoundMessage);
            }

            return review;
        }

        private static void EnsureReviewOwner(Review review, Guid userId)
        {
            if (review.UserId != userId)
            {
                throw new SpectrumForbiddenException(ForbiddenActionMessage);
            }
        }

        private static void EnsureReviewOwnerOrAdmin(Review review, Guid userId, bool isAdmin)
        {
            if (!isAdmin && review.UserId != userId)
            {
                throw new SpectrumForbiddenException(ForbiddenActionMessage);
            }
        }

        private ReviewResponseDto MapToResponseDto(
            Review review,
            Guid? currentUserId,
            bool isAdmin = false,
            string? currentUserVote = null
        )
        {
            var profilePicture = review.User?.ProfilePicture ?? string.Empty;
            var isOwnReview = currentUserId.HasValue && review.UserId == currentUserId.Value;
            var username = review.User?.Username ?? "Usuario Spectrum";
            var game = _gameRepository?.GetById(review.GameId);

            return new ReviewResponseDto
            {
                Id = review.Id,
                UserId = review.UserId,
                Username = username,
                UserProfileImageUrl = profilePicture,
                ProfilePicture = profilePicture,
                GameId = review.GameId,
                GameTitle = game?.Title ?? string.Empty,
                GameCoverUrl = game?.CoverImageUrl ?? string.Empty,
                Rating = review.Rating,
                Title = review.Title,
                Content = review.Content,
                ImageUrl = review.ImageUrl ?? string.Empty,
                AttachmentUrl = review.ImageUrl ?? string.Empty,
                AttachmentType = review.MediaType ?? string.Empty,
                CreatedAt = review.CreatedAt,
                UpdatedAt = review.UpdatedAt,
                LikesCount = review.LikesCount,
                DislikesCount = review.DislikesCount,
                CurrentUserVote = isOwnReview ? null : currentUserVote,
                UserVote = isOwnReview ? null : currentUserVote,
                MyVote = isOwnReview ? null : currentUserVote,
                IsOwnReview = isOwnReview,
                CanDelete = isOwnReview || isAdmin
            };
        }

        private async Task<IReadOnlyDictionary<Guid, string>> ResolveCurrentUserVotesAsync(
            IEnumerable<Guid> reviewIds,
            Guid? currentUserId,
            CancellationToken cancellationToken
        )
        {
            if (_voteService is null)
            {
                return new Dictionary<Guid, string>();
            }

            return await _voteService.GetCurrentReviewVotesAsync(reviewIds, currentUserId, cancellationToken);
        }

        private static void ValidateGameId(int gameId)
        {
            if (gameId < MinimumGameId)
            {
                throw new SpectrumBusinessException("El ID del videojuego debe ser valido.");
            }
        }

        private static void ValidateRating(int rating)
        {
            if (rating is < MinimumRating or > MaximumRating)
            {
                throw new SpectrumBusinessException("La calificacion debe estar entre 5 y 10.");
            }
        }

        private static string NormalizeTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new SpectrumBusinessException("El titulo de la resena es obligatorio.");
            }

            var normalizedTitle = title.Trim();

            if (normalizedTitle.Length > MaximumTitleLength)
            {
                throw new SpectrumBusinessException("El titulo de la resena no puede superar los 120 caracteres.");
            }

            return normalizedTitle;
        }

        private static string NormalizeContent(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new SpectrumBusinessException("El contenido de la resena es obligatorio.");
            }

            var normalizedContent = content.Trim();

            if (normalizedContent.Length > MaximumContentLength)
            {
                throw new SpectrumBusinessException("El contenido de la resena no puede superar los 2000 caracteres.");
            }

            return normalizedContent;
        }

        private static void ValidateImageUrl(string? imageUrl)
        {
            if (imageUrl is not null && imageUrl.Length > MaximumImageUrlLength)
            {
                throw new SpectrumBusinessException("La URL del adjunto no puede superar los 255 caracteres.");
            }
        }

        private static void ValidateMediaType(string? imageUrl, string? mediaType)
        {
            if (string.IsNullOrWhiteSpace(imageUrl) && string.IsNullOrWhiteSpace(mediaType))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(imageUrl) || string.IsNullOrWhiteSpace(mediaType))
            {
                throw new SpectrumBusinessException("El adjunto debe incluir URL y tipo de archivo.");
            }

            if (!AllowedMediaTypes.Contains(mediaType))
            {
                throw new SpectrumBusinessException("El tipo de archivo adjunto no es valido.");
            }
        }

        private static string? NormalizeMediaType(string? mediaType)
        {
            return string.IsNullOrWhiteSpace(mediaType) ? null : mediaType.Trim().ToLowerInvariant();
        }
    }
}
