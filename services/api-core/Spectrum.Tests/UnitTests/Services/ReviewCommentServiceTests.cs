using Grpc.Core;
using Microsoft.Extensions.Logging;
using Moq;
using Spectrum.API.Dtos.Profile;
using Spectrum.API.Dtos.Reviews;
using Spectrum.API.Exceptions;
using Spectrum.API.Grpc.Social;
using Spectrum.API.Models;
using Spectrum.API.Repositories;
using Spectrum.API.Services.Reviews;
using Spectrum.API.Utilities;

namespace Spectrum.Tests.UnitTests.Services
{
    public class ReviewCommentServiceTests
    {
        private readonly Mock<CommentService.CommentServiceClient> _commentClientMock = new();
        private readonly Mock<IReviewRepository> _reviewRepositoryMock = new();
        private readonly Mock<IUserRepository> _userRepositoryMock = new();
        private readonly Mock<ILogger<ReviewCommentService>> _loggerMock = new();

        [Fact]
        public async Task CreateAsyncShouldTrimContentPublishToGrpcAndEnrichAuthor()
        {
            var userId = Guid.NewGuid();
            var reviewId = Guid.NewGuid();
            _reviewRepositoryMock
                .Setup(repository => repository.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Review { Id = reviewId, GameId = 3498, UserId = Guid.NewGuid() });
            _commentClientMock
                .Setup(client => client.PublishCommentAsync(
                    It.Is<PublishCommentRequest>(request =>
                        request.UserId == userId.ToString() &&
                        request.ReviewId == reviewId.ToString() &&
                        request.GameId == "3498" &&
                        request.Content == "Great review"),
                    null,
                    null,
                    It.IsAny<CancellationToken>()))
                .Returns(CreateAsyncUnaryCall(new CommentResponse
                {
                    CommentId = "comment-1",
                    UserId = userId.ToString(),
                    ReviewId = reviewId.ToString(),
                    Content = "Great review",
                    PublishedAt = 1_700_000_000_000
                }));
            _userRepositoryMock
                .Setup(repository => repository.GetPublicUsersByIdsAsync(
                    It.Is<IEnumerable<Guid>>(ids => ids.Contains(userId)),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, PublicUserSummaryDto>
                {
                    [userId] = new() { Id = userId, Username = "neo", ProfilePicture = "neo.png" }
                });
            var service = CreateService();

            var result = await service.CreateAsync(
                reviewId,
                new CreateReviewCommentDto { Content = "  Great review  " },
                userId,
                CancellationToken.None
            );

            Assert.Equal("comment-1", result.Id);
            Assert.Equal("Great review", result.Content);
            Assert.Equal("neo", result.Username);
            Assert.Equal("neo.png", result.UserProfilePicture);
            Assert.True(result.IsOwnComment);
            Assert.True(result.CanDelete);
        }

        [Fact]
        public async Task CreateAsyncWhenGrpcOmitsResponseFieldsShouldUseFallbackRequestValues()
        {
            var userId = Guid.NewGuid();
            var reviewId = Guid.NewGuid();
            _reviewRepositoryMock
                .Setup(repository => repository.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Review { Id = reviewId, GameId = 10 });
            _commentClientMock
                .Setup(client => client.PublishCommentAsync(
                    It.IsAny<PublishCommentRequest>(),
                    null,
                    null,
                    It.IsAny<CancellationToken>()))
                .Returns(CreateAsyncUnaryCall(new CommentResponse
                {
                    CommentId = "comment-fallback",
                    UserId = "bad-user",
                    ReviewId = "bad-review",
                    Content = " ",
                    PublishedAt = 0
                }));
            _userRepositoryMock
                .Setup(repository => repository.GetPublicUsersByIdsAsync(
                    It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, PublicUserSummaryDto>());
            var service = CreateService();

            var result = await service.CreateAsync(
                reviewId,
                new CreateReviewCommentDto { Content = "  fallback content  " },
                userId,
                CancellationToken.None);

            Assert.Equal("comment-fallback", result.Id);
            Assert.Equal(userId, result.UserId);
            Assert.Equal(reviewId, result.ReviewId);
            Assert.Equal("fallback content", result.Content);
            Assert.True(result.PublishedAt > DateTime.UtcNow.AddMinutes(-1));
            Assert.Equal("Usuario Spectrum", result.Username);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateAsyncWhenContentIsBlankShouldRejectWithoutCallingGrpc(string content)
        {
            var reviewId = Guid.NewGuid();
            _reviewRepositoryMock
                .Setup(repository => repository.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Review { Id = reviewId, GameId = 10 });
            var service = CreateService();

            await Assert.ThrowsAsync<SpectrumBusinessException>(() =>
                service.CreateAsync(reviewId, new CreateReviewCommentDto { Content = content }, Guid.NewGuid()));

            _commentClientMock.Verify(client => client.PublishCommentAsync(
                It.IsAny<PublishCommentRequest>(),
                null,
                null,
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetByReviewAsyncShouldNormalizePageAndAllowAdminDeleteWithoutAuthorMatch()
        {
            var reviewId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            _reviewRepositoryMock
                .Setup(repository => repository.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Review { Id = reviewId, GameId = 10 });
            _commentClientMock
                .Setup(client => client.GetCommentsByReview(
                    It.Is<GetCommentsRequest>(request => request.ReviewId == reviewId.ToString() && request.Page == 1),
                    null,
                    null,
                    It.IsAny<CancellationToken>()))
                .Returns(CreateStreamingCall(new[]
                {
                    new CommentResponse
                    {
                        CommentId = "comment-1",
                        UserId = authorId.ToString(),
                        ReviewId = reviewId.ToString(),
                        Content = "Visible comment",
                        PublishedAt = 1_700_000_000_000
                    }
                }));
            _userRepositoryMock
                .Setup(repository => repository.GetPublicUsersByIdsAsync(
                    It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, PublicUserSummaryDto>());
            var service = CreateService();

            var result = await service.GetByReviewAsync(reviewId, currentUserId: Guid.NewGuid(), isAdmin: true, page: -8);

            var comment = Assert.Single(result);
            Assert.Equal("Usuario Spectrum", comment.Username);
            Assert.False(comment.IsOwnComment);
            Assert.True(comment.CanDelete);
        }

        [Fact]
        public async Task GetByReviewAsyncWhenCurrentUserOwnsCommentShouldMarkOwnAndDeletable()
        {
            var reviewId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            _reviewRepositoryMock
                .Setup(repository => repository.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Review { Id = reviewId, GameId = 10 });
            _commentClientMock
                .Setup(client => client.GetCommentsByReview(
                    It.IsAny<GetCommentsRequest>(),
                    null,
                    null,
                    It.IsAny<CancellationToken>()))
                .Returns(CreateStreamingCall(new[]
                {
                    new CommentResponse
                    {
                        CommentId = "comment-owned",
                        UserId = authorId.ToString(),
                        ReviewId = reviewId.ToString(),
                        Content = "Owned comment",
                        PublishedAt = 1_700_000_000_000
                    }
                }));
            _userRepositoryMock
                .Setup(repository => repository.GetPublicUsersByIdsAsync(
                    It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, PublicUserSummaryDto>());
            var service = CreateService();

            var result = await service.GetByReviewAsync(reviewId, currentUserId: authorId, isAdmin: false, page: 1);

            var comment = Assert.Single(result);
            Assert.True(comment.IsOwnComment);
            Assert.True(comment.CanDelete);
        }

        [Fact]
        public async Task DeleteAsyncWhenAdminRequestsDeletionShouldSendAdminRole()
        {
            var requesterId = Guid.NewGuid();
            _commentClientMock
                .Setup(client => client.DeleteCommentAsync(
                    It.Is<DeleteCommentRequest>(request =>
                        request.CommentId == "comment-1" &&
                        request.RequesterId == requesterId.ToString() &&
                        request.RequesterRole == Constants.Roles.Admin),
                    null,
                    null,
                    It.IsAny<CancellationToken>()))
                .Returns(CreateAsyncUnaryCall(new DeleteResponse { Success = true }));
            var service = CreateService();

            await service.DeleteAsync(" comment-1 ", requesterId, isAdmin: true);

            _commentClientMock.Verify(client => client.DeleteCommentAsync(
                It.IsAny<DeleteCommentRequest>(),
                null,
                null,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAsyncWhenCommentIdIsBlankShouldRejectWithoutCallingGrpc()
        {
            var service = CreateService();

            await Assert.ThrowsAsync<SpectrumBusinessException>(() =>
                service.DeleteAsync(" ", Guid.NewGuid(), isAdmin: false));

            _commentClientMock.Verify(client => client.DeleteCommentAsync(
                It.IsAny<DeleteCommentRequest>(),
                null,
                null,
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsyncWhenGrpcReturnsInvalidArgumentShouldMapBusinessException()
        {
            var reviewId = Guid.NewGuid();
            _reviewRepositoryMock
                .Setup(repository => repository.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Review { Id = reviewId, GameId = 10 });
            _commentClientMock
                .Setup(client => client.PublishCommentAsync(
                    It.IsAny<PublishCommentRequest>(),
                    null,
                    null,
                    It.IsAny<CancellationToken>()))
                .Throws(new RpcException(new Status(StatusCode.InvalidArgument, "comment rejected")));
            var service = CreateService();

            var exception = await Assert.ThrowsAsync<SpectrumBusinessException>(() =>
                service.CreateAsync(reviewId, new CreateReviewCommentDto { Content = "Valid comment" }, Guid.NewGuid()));

            Assert.Equal("comment rejected", exception.Message);
        }

        [Theory]
        [InlineData(StatusCode.NotFound, typeof(SpectrumNotFoundException))]
        [InlineData(StatusCode.PermissionDenied, typeof(SpectrumForbiddenException))]
        [InlineData(StatusCode.Unavailable, typeof(SpectrumServiceUnavailableException))]
        [InlineData(StatusCode.Unknown, typeof(SpectrumServiceUnavailableException))]
        public async Task DeleteAsyncWhenGrpcFailsShouldMapKnownStatusCodes(StatusCode statusCode, Type expectedExceptionType)
        {
            _commentClientMock
                .Setup(client => client.DeleteCommentAsync(
                    It.IsAny<DeleteCommentRequest>(),
                    null,
                    null,
                    It.IsAny<CancellationToken>()))
                .Throws(new RpcException(new Status(statusCode, "grpc failed")));
            var service = CreateService();

            var exception = await Assert.ThrowsAsync(expectedExceptionType, () =>
                service.DeleteAsync("comment-1", Guid.NewGuid(), isAdmin: false));

            Assert.NotNull(exception);
        }

        private ReviewCommentService CreateService()
        {
            return new ReviewCommentService(
                _commentClientMock.Object,
                _reviewRepositoryMock.Object,
                _userRepositoryMock.Object,
                _loggerMock.Object
            );
        }

        private static AsyncUnaryCall<TResponse> CreateAsyncUnaryCall<TResponse>(TResponse response)
        {
            return new AsyncUnaryCall<TResponse>(
                Task.FromResult(response),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        }

        private static AsyncServerStreamingCall<TResponse> CreateStreamingCall<TResponse>(IEnumerable<TResponse> responses)
        {
            return new AsyncServerStreamingCall<TResponse>(
                new TestAsyncStreamReader<TResponse>(responses),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        }

        private sealed class TestAsyncStreamReader<T> : IAsyncStreamReader<T>
        {
            private readonly Queue<T> _responses;

            public TestAsyncStreamReader(IEnumerable<T> responses)
            {
                _responses = new Queue<T>(responses);
            }

            public T Current { get; private set; } = default!;

            public Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                if (_responses.Count == 0)
                {
                    return Task.FromResult(false);
                }

                Current = _responses.Dequeue();
                return Task.FromResult(true);
            }
        }
    }
}
