using Grpc.Core;
using Spectrum.API.Dtos.Votes;
using Spectrum.API.Exceptions;
using Spectrum.API.Grpc.Social;
using Spectrum.API.Repositories;
using Spectrum.API.Utilities;

namespace Spectrum.API.Services.Votes
{
    public interface IVoteService
    {
        Task<VoteResultDto> CastReviewVoteAsync(
            Guid reviewId,
            Guid userId,
            bool isPositive,
            CancellationToken cancellationToken = default
        );

        /// <summary>
        /// Resolves the authenticated user's active vote for a batch of reviews.
        /// </summary>
        Task<IReadOnlyDictionary<Guid, string>> GetCurrentReviewVotesAsync(
            IEnumerable<Guid> reviewIds,
            Guid? userId,
            CancellationToken cancellationToken = default
        );
    }

    public class VoteServiceClient : IVoteService
    {
        private const string ReviewTargetType = "REVIEW";
        private const string ReviewNotFoundMessage = "La resena solicitada no existe.";

        private readonly VoteService.VoteServiceClient _voteServiceClient;
        private readonly IReviewRepository _reviewRepository;
        private readonly ILogger<VoteServiceClient> _logger;

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
