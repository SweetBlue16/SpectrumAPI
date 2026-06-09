using Grpc.Core;
using Spectrum.API.Dtos.Profile;
using Spectrum.API.Dtos.Reviews;
using Spectrum.API.Exceptions;
using Spectrum.API.Grpc.Social;
using Spectrum.API.Models;
using Spectrum.API.Repositories;
using Spectrum.API.Utilities;

namespace Spectrum.API.Services.Reviews
{
    /// <summary>
    /// Defines the contract for managing review comments through the social microservice.
    /// Provides operations for creating, retrieving, and deleting comments associated with reviews.
    /// </summary>
    public interface IReviewCommentService
    {
        /// <summary>
        /// Creates a new comment for a specific review.
        /// </summary>
        /// <param name="reviewId">The unique identifier of the review being commented on.</param>
        /// <param name="dto">The payload containing the comment content.</param>
        /// <param name="userId">The unique identifier of the author creating the comment.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The newly created review comment enriched with author metadata.</returns>
        Task<ReviewCommentResponseDto> CreateAsync(
            Guid reviewId,
            CreateReviewCommentDto dto,
            Guid userId,
            CancellationToken cancellationToken = default
        );

        /// <summary>
        /// Retrieves a paginated collection of comments associated with a review.
        /// </summary>
        /// <param name="reviewId">The unique identifier of the review.</param>
        /// <param name="currentUserId">The authenticated user identifier, if available.</param>
        /// <param name="isAdmin">Indicates whether the requester has administrative privileges.</param>
        /// <param name="page">The page number to retrieve.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>A collection of review comments enriched with author information and permissions.</returns>
        Task<IReadOnlyList<ReviewCommentResponseDto>> GetByReviewAsync(
            Guid reviewId,
            Guid? currentUserId = null,
            bool isAdmin = false,
            int page = 1,
            CancellationToken cancellationToken = default
        );

        /// <summary>
        /// Deletes an existing review comment.
        /// </summary>
        /// <param name="commentId">The identifier of the comment to remove.</param>
        /// <param name="requesterId">The identifier of the user requesting the deletion.</param>
        /// <param name="isAdmin">Indicates whether the requester has administrative privileges.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        Task DeleteAsync(
            string commentId,
            Guid requesterId,
            bool isAdmin,
            CancellationToken cancellationToken = default
        );
    }

    /// <summary>
    /// Service responsible for orchestrating review comment operations.
    /// Integrates with the Social gRPC microservice for comment persistence
    /// and enriches responses with Spectrum user profile information.
    /// </summary>
    public class ReviewCommentService : IReviewCommentService
    {
        private const int MinimumPage = 1;
        private const int MaximumContentLength = 500;
        private const string ReviewNotFoundMessage = "La resena solicitada no existe.";
        private readonly CommentService.CommentServiceClient _commentServiceClient;
        private readonly IReviewRepository _reviewRepository;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<ReviewCommentService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReviewCommentService"/> class.
        /// </summary>
        /// <param name="commentServiceClient">The gRPC client used to communicate with the social service.</param>
        /// <param name="reviewRepository">The repository used to validate review existence.</param>
        /// <param name="userRepository">The repository used to enrich comments with author information.</param>
        /// <param name="logger">The logger used for diagnostics and gRPC failure tracking.</param>
        public ReviewCommentService(
            CommentService.CommentServiceClient commentServiceClient,
            IReviewRepository reviewRepository,
            IUserRepository userRepository,
            ILogger<ReviewCommentService> logger
        )
        {
            _commentServiceClient = commentServiceClient;
            _reviewRepository = reviewRepository;
            _userRepository = userRepository;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<ReviewCommentResponseDto> CreateAsync(
            Guid reviewId,
            CreateReviewCommentDto dto,
            Guid userId,
            CancellationToken cancellationToken = default
        )
        {
            var review = await GetExistingReviewAsync(reviewId, cancellationToken);

            var content = NormalizeContent(dto.Content);

            try
            {
                var response = await _commentServiceClient.PublishCommentAsync(
                    new PublishCommentRequest
                    {
                        UserId = userId.ToString(),
                        ReviewId = reviewId.ToString(),
                        Content = content,
                        GameId = review.GameId.ToString()
                    },
                    cancellationToken: cancellationToken
                );

                var comment = MapCommentResponse(response, userId, reviewId, content);
                var enrichedComments = await EnrichCommentsAsync(
                    new[] { comment },
                    userId,
                    isAdmin: false,
                    cancellationToken
                );

                return enrichedComments[0];
            }
            catch (RpcException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Review comment publish failed for reviewId={ReviewId} userId={UserId} status={StatusCode}",
                    reviewId,
                    userId,
                    ex.StatusCode
                );
                throw MapRpcException(ex);
            }
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ReviewCommentResponseDto>> GetByReviewAsync(
            Guid reviewId,
            Guid? currentUserId = null,
            bool isAdmin = false,
            int page = 1,
            CancellationToken cancellationToken = default
        )
        {
            await GetExistingReviewAsync(reviewId, cancellationToken);
            var normalizedPage = page < MinimumPage ? MinimumPage : page;

            try
            {
                using var call = _commentServiceClient.GetCommentsByReview(
                    new GetCommentsRequest
                    {
                        ReviewId = reviewId.ToString(),
                        Page = normalizedPage
                    },
                    cancellationToken: cancellationToken
                );

                var comments = new List<ReviewCommentResponseDto>();

                while (await call.ResponseStream.MoveNext(cancellationToken))
                {
                    comments.Add(MapCommentResponse(call.ResponseStream.Current, currentUserId));
                }

                return await EnrichCommentsAsync(comments, currentUserId, isAdmin, cancellationToken);
            }
            catch (RpcException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Review comments fetch failed for reviewId={ReviewId} status={StatusCode}",
                    reviewId,
                    ex.StatusCode
                );
                throw MapRpcException(ex);
            }
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(
            string commentId,
            Guid requesterId,
            bool isAdmin,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(commentId))
            {
                throw new SpectrumBusinessException("El comentario solicitado no es valido.");
            }

            try
            {
                var response = await _commentServiceClient.DeleteCommentAsync(
                    new DeleteCommentRequest
                    {
                        CommentId = commentId.Trim(),
                        RequesterId = requesterId.ToString(),
                        RequesterRole = isAdmin ? Constants.Roles.Admin : Constants.Roles.Reviewer
                    },
                    cancellationToken: cancellationToken
                );

                if (!response.Success)
                {
                    throw new SpectrumServiceUnavailableException(Constants.ErrorMessages.RpcServiceUnavailable);
                }
            }
            catch (RpcException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Review comment delete failed for commentId={CommentId} requesterId={RequesterId} status={StatusCode}",
                    commentId,
                    requesterId,
                    ex.StatusCode
                );
                throw MapRpcException(ex);
            }
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

        private static ReviewCommentResponseDto MapCommentResponse(
            CommentResponse response,
            Guid? currentUserId
        )
        {
            var userId = TryParseGuid(response.UserId);
            var reviewId = TryParseGuid(response.ReviewId);

            return new ReviewCommentResponseDto
            {
                Id = response.CommentId,
                UserId = userId,
                ReviewId = reviewId,
                Content = response.Content,
                PublishedAt = ToDateTime(response.PublishedAt),
                IsOwnComment = currentUserId.HasValue && userId == currentUserId.Value,
                CanDelete = currentUserId.HasValue && userId == currentUserId.Value
            };
        }

        private static ReviewCommentResponseDto MapCommentResponse(
            CommentResponse response,
            Guid fallbackUserId,
            Guid fallbackReviewId,
            string fallbackContent
        )
        {
            return new ReviewCommentResponseDto
            {
                Id = response.CommentId,
                UserId = TryParseGuid(response.UserId, fallbackUserId),
                ReviewId = TryParseGuid(response.ReviewId, fallbackReviewId),
                Content = string.IsNullOrWhiteSpace(response.Content) ? fallbackContent : response.Content,
                PublishedAt = response.PublishedAt > 0 ? ToDateTime(response.PublishedAt) : DateTime.UtcNow,
                IsOwnComment = true,
                CanDelete = true
            };
        }

        private async Task<IReadOnlyList<ReviewCommentResponseDto>> EnrichCommentsAsync(
            IReadOnlyList<ReviewCommentResponseDto> comments,
            Guid? currentUserId,
            bool isAdmin,
            CancellationToken cancellationToken
        )
        {
            if (comments.Count == 0)
            {
                return comments;
            }

            var usersById = await _userRepository.GetPublicUsersByIdsAsync(
                comments.Select(comment => comment.UserId),
                cancellationToken
            );

            foreach (var comment in comments)
            {
                ApplyAuthorData(comment, usersById);

                comment.IsOwnComment = currentUserId.HasValue && comment.UserId == currentUserId.Value;
                comment.CanDelete = comment.IsOwnComment || isAdmin;
            }

            return comments;
        }

        private static void ApplyAuthorData(
            ReviewCommentResponseDto comment,
            IReadOnlyDictionary<Guid, PublicUserSummaryDto> usersById
        )
        {
            if (usersById.TryGetValue(comment.UserId, out var user))
            {
                comment.Username = user.Username;
                comment.UserProfilePicture = user.ProfilePicture;
                return;
            }

            comment.Username = "Usuario Spectrum";
            comment.UserProfilePicture = string.Empty;
        }

        private static Guid TryParseGuid(string value, Guid fallback = default)
        {
            return Guid.TryParse(value, out var parsedValue) ? parsedValue : fallback;
        }

        private static DateTime ToDateTime(long unixTimeMilliseconds)
        {
            return unixTimeMilliseconds > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds).UtcDateTime
                : DateTime.MinValue;
        }

        private static string NormalizeContent(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new SpectrumBusinessException("El comentario es obligatorio.");
            }

            var normalizedContent = content.Trim();

            if (normalizedContent.Length > MaximumContentLength)
            {
                throw new SpectrumBusinessException("El comentario no puede superar los 500 caracteres.");
            }

            return normalizedContent;
        }

        private static Exception MapRpcException(RpcException exception)
        {
            return exception.StatusCode switch
            {
                StatusCode.InvalidArgument => new SpectrumBusinessException(exception.Status.Detail, exception),
                StatusCode.NotFound => new SpectrumNotFoundException(exception.Status.Detail),
                StatusCode.PermissionDenied => new SpectrumForbiddenException(exception.Status.Detail),
                StatusCode.Unavailable => new SpectrumServiceUnavailableException(
                    "El servicio social no esta disponible. Verifica que service-social este corriendo en el puerto gRPC configurado.",
                    exception
                ),
                _ => new SpectrumServiceUnavailableException(Constants.ErrorMessages.RpcServiceUnavailable, exception)
            };
        }
    }
}
