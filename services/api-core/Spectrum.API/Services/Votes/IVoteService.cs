using Grpc.Core;
using Spectrum.API.Dtos.Votes;
using Spectrum.API.Exceptions;
using Spectrum.API.Grpc.Social;
using Spectrum.API.Repositories;
using Spectrum.API.Utilities;

namespace Spectrum.API.Services.Votes
{
    /// <summary>
    /// Defines the contract for casting and resolving votes associated with review entities.
    /// Acts as an abstraction over the social microservice responsible for vote persistence.
    /// </summary>
    public interface IVoteService
    {
        /// <summary>
        /// Casts or updates a vote for a specific review on behalf of an authenticated user.
        /// </summary>
        /// <param name="reviewId">The unique identifier of the target review.</param>
        /// <param name="userId">The unique identifier of the user casting the vote.</param>
        /// <param name="isPositive">
        /// Indicates whether the vote is positive (<c>true</c>) or negative (<c>false</c>).
        /// </param>
        /// <param name="cancellationToken">The cancellation token controlling the asynchronous operation.</param>
        /// <returns>
        /// A <see cref="VoteResultDto"/> containing the updated vote counters and operation result.
        /// </returns>
        Task<VoteResultDto> CastReviewVoteAsync(
            Guid reviewId,
            Guid userId,
            bool isPositive,
            CancellationToken cancellationToken = default
        );

        /// <summary>
        /// Resolves the authenticated user's active vote for a batch of reviews.
        /// </summary>
        /// <param name="reviewIds">The collection of review identifiers to evaluate.</param>
        /// <param name="userId">The unique identifier of the authenticated user.</param>
        /// <param name="cancellationToken">The cancellation token controlling the asynchronous operation.</param>
        /// <returns>
        /// A dictionary where the key is the review identifier and the value is the vote type
        /// (<c>like</c> or <c>dislike</c>).
        /// </returns>
        Task<IReadOnlyDictionary<Guid, string>> GetCurrentReviewVotesAsync(
            IEnumerable<Guid> reviewIds,
            Guid? userId,
            CancellationToken cancellationToken = default
        );
    }

    /// <summary>
    /// gRPC-backed implementation of <see cref="IVoteService"/>.
    /// Coordinates vote operations between the Spectrum API, the review repository,
    /// and the social microservice responsible for vote management.
    /// </summary>
    public class VoteServiceClient : IVoteService
    {
        private const string ReviewTargetType = "REVIEW";
        private const string ReviewNotFoundMessage = "La resena solicitada no existe.";

        private readonly VoteService.VoteServiceClient _voteServiceClient;
        private readonly IReviewRepository _reviewRepository;
        private readonly ILogger<VoteServiceClient> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="VoteServiceClient"/> class.
        /// </summary>
        /// <param name="voteServiceClient">
        /// The gRPC client used to communicate with the social service vote endpoints.
        /// </param>
        /// <param name="reviewRepository">
        /// The repository used to validate review existence and synchronize vote counters.
        /// </param>
        /// <param name="logger">
        /// The logger used for vote operation diagnostics and gRPC failure tracking.
        /// </param>
        public VoteServiceClient(
            VoteService.VoteServiceClient voteServiceClient,
            IReviewRepository reviewRepository,
            ILogger<VoteServiceClient> logger
        )
        {
            _voteServiceClient = voteServiceClient;
            _reviewRepository = reviewRepository;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<VoteResultDto> CastReviewVoteAsync(
            Guid reviewId,
            Guid userId,
            bool isPositive,
            CancellationToken cancellationToken = default
        )
        {
            var review = await _reviewRepository.GetByIdAsync(reviewId, cancellationToken);

            if (review is null)
            {
                throw new SpectrumNotFoundException(ReviewNotFoundMessage);
            }

            if (review.UserId == userId)
            {
                throw new SpectrumForbiddenException("No puedes votar tu propia resena.");
            }

            try
            {
                var response = await _voteServiceClient.CastVoteAsync(
                    new CastVoteRequest
                    {
                        UserId = userId.ToString(),
                        TargetId = reviewId.ToString(),
                        TargetType = ReviewTargetType,
                        IsPositive = isPositive
                    },
                    cancellationToken: cancellationToken
                );

                var result = new VoteResultDto
                {
                    Success = response.Success,
                    UpdatedLikes = response.UpdatedLikes,
                    UpdatedDislikes = response.UpdatedDislikes
                };

                if (result.Success)
                {
                    await _reviewRepository.UpdateCountersAsync(
                        reviewId,
                        result.UpdatedLikes,
                        result.UpdatedDislikes,
                        cancellationToken
                    );
                    await _reviewRepository.SaveChangesAsync(cancellationToken);
                }

                return result;
            }
            catch (RpcException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Review vote failed for reviewId={ReviewId} userId={UserId} status={StatusCode}",
                    reviewId,
                    userId,
                    ex.StatusCode
                );

                throw ex.StatusCode switch
                {
                    StatusCode.InvalidArgument => new SpectrumBusinessException(ex.Status.Detail, ex),
                    StatusCode.Unavailable => new SpectrumServiceUnavailableException(
                        "El servicio social no esta disponible. Verifica que service-social este corriendo en el puerto gRPC configurado.",
                        ex
                    ),
                    _ => new SpectrumServiceUnavailableException(Constants.ErrorMessages.RpcServiceUnavailable, ex)
                };
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyDictionary<Guid, string>> GetCurrentReviewVotesAsync(
            IEnumerable<Guid> reviewIds,
            Guid? userId,
            CancellationToken cancellationToken = default
        )
        {
            var ids = reviewIds.Distinct().ToArray();
            if (!userId.HasValue || ids.Length == 0)
            {
                return new Dictionary<Guid, string>();
            }

            try
            {
                var request = new GetUserVotesRequest
                {
                    UserId = userId.Value.ToString(),
                    TargetType = ReviewTargetType
                };
                request.TargetIds.AddRange(ids.Select(id => id.ToString()));

                var response = await _voteServiceClient.GetUserVotesAsync(
                    request,
                    cancellationToken: cancellationToken
                );

                var votes = new Dictionary<Guid, string>();
                foreach (var vote in response.Votes)
                {
                    if (Guid.TryParse(vote.TargetId, out var targetId))
                    {
                        votes[targetId] = vote.IsPositive ? "like" : "dislike";
                    }
                }

                return votes;
            }
            catch (RpcException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Could not resolve current review votes for userId={UserId} targetCount={TargetCount} status={StatusCode}",
                    userId,
                    ids.Length,
                    ex.StatusCode
                );

                return new Dictionary<Guid, string>();
            }
        }
    }
}
