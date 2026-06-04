using Grpc.Core;
using Microsoft.Extensions.Logging;
using Moq;
using Spectrum.API.Dtos.Drops;
using Spectrum.API.Exceptions;
using Spectrum.API.Grpc.Drops;
using Spectrum.API.Models;
using Spectrum.API.Repositories;
using Spectrum.API.Services.Drops;

namespace Spectrum.Tests.UnitTests.Services
{
    public class DropsServiceTests
    {
        private readonly Mock<DropService.DropServiceClient> _grpcClientMock;
        private readonly Mock<IUserRepository> _userRepositoryMock;
        private readonly Mock<IRewardDeliveryService> _rewardDeliveryServiceMock;
        private readonly Mock<ILogger<DropsService>> _loggerMock;
        private readonly DropsService _dropService;

        public DropsServiceTests()
        {
            _grpcClientMock = new Mock<DropService.DropServiceClient>();
            _userRepositoryMock = new Mock<IUserRepository>();
            _rewardDeliveryServiceMock = new Mock<IRewardDeliveryService>();
            _loggerMock = new Mock<ILogger<DropsService>>();
            _dropService = new DropsService(
                _grpcClientMock.Object,
                _userRepositoryMock.Object,
                _rewardDeliveryServiceMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task ClaimAccessKeyAsyncWhenJavaServiceReturnsSuccessShouldReturnWinner()
        {
            var userId = Guid.NewGuid();
            var eventId = "event-123";
            var claimedAtEpoch = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            _userRepositoryMock
                .Setup(repository => repository.GetUserByIdAsync(userId))
                .ReturnsAsync(new User { Id = userId, Username = "neo", Email = "neo@spectrum.test" });

            _grpcClientMock
                .Setup(client => client.ClaimAccessKeyAsync(
                    It.Is<ClaimKeyRequest>(request =>
                        request.UserId == userId.ToString() &&
                        request.EventId == eventId &&
                        request.ChallengeCode == string.Empty),
                    null,
                    null,
                    It.IsAny<CancellationToken>()))
                .Returns(CreateAsyncUnaryCall(new ClaimKeyResponse
                {
                    Success = true,
                    AccessKeyCode = "WIN-KEY",
                    WinnerUserId = userId.ToString(),
                    WinnerUsername = "neo",
                    ClaimedAt = claimedAtEpoch,
                    Message = "Winner assigned."
                }));
            _grpcClientMock
                .Setup(client => client.GetEventStatusAsync(
                    It.Is<GetEventRequest>(request => request.EventId == eventId),
                    null,
                    null,
                    It.IsAny<CancellationToken>()))
                .Returns(CreateAsyncUnaryCall(new EventStatusResponse
                {
                    EventId = eventId,
                    Title = "Launch Drop",
                    GameTitle = "Halo",
                    Platform = "PC",
                    Status = "REVEAL_READY",
                    StartAt = claimedAtEpoch,
                    JoinDeadlineAt = claimedAtEpoch,
                    RevealAt = claimedAtEpoch,
                    EndDate = claimedAtEpoch,
                    TotalSlots = 10
                }));
            _grpcClientMock
                .Setup(client => client.MarkRewardSentAsync(
                    It.Is<MarkRewardSentRequest>(request =>
                        request.EventId == eventId &&
                        request.WinnerUserId == userId.ToString() &&
                        request.RewardSentAt > 0),
                    null,
                    null,
                    It.IsAny<CancellationToken>()))
                .Returns(CreateAsyncUnaryCall(new EventActionResponse
                {
                    Success = true,
                    EventId = eventId,
                    Message = "Reward marked as sent."
                }));

            var result = await _dropService.ClaimAccessKeyAsync(
                userId,
                eventId,
                new ClaimDropDto { ChallengeCode = "READY" },
                CancellationToken.None
            );

            Assert.True(result.Success);
            Assert.Equal("neo", result.WinnerUsername);
            Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(claimedAtEpoch).UtcDateTime, result.ClaimedAt);
            _rewardDeliveryServiceMock.Verify(service => service.SendRewardAsync(
                "neo@spectrum.test",
                "Halo - PC",
                "WIN-KEY",
                It.IsAny<CancellationToken>()), Times.Once);
            _grpcClientMock.Verify(client => client.MarkRewardSentAsync(
                It.Is<MarkRewardSentRequest>(request =>
                    request.EventId == eventId &&
                    request.WinnerUserId == userId.ToString()),
                null,
                null,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetEventStatusAsyncWhenRequesterExistsShouldMapUserDropFlags()
        {
            var userId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _grpcClientMock
                .Setup(client => client.GetEventStatusAsync(
                    It.Is<GetEventRequest>(request => request.EventId == "event-joined" && request.RequesterUserId == userId.ToString()),
                    null,
                    null,
                    It.IsAny<CancellationToken>()))
                .Returns(CreateAsyncUnaryCall(new EventStatusResponse
                {
                    EventId = "event-joined",
                    Title = "Launch Drop",
                    GameTitle = "Halo",
                    Platform = "PC",
                    Status = "REVEAL_READY",
                    StartAt = now,
                    JoinDeadlineAt = now,
                    RevealAt = now,
                    EndDate = now,
                    TotalSlots = 10,
                    AvailableSlots = 3,
                    RemainingSlots = 3,
                    CurrentUserJoined = true,
                    CanClaim = true,
                    HasClaimed = false,
                    VisibleUntil = now + 3_600_000
                }));

            var result = await _dropService.GetEventStatusAsync("event-joined", false, CancellationToken.None, userId);

            Assert.True(result.CurrentUserJoined);
            Assert.True(result.CanClaim);
            Assert.False(result.HasClaimed);
            Assert.Equal(3, result.RemainingSlots);
            Assert.NotNull(result.VisibleUntil);
        }

        [Fact]
        public async Task ClaimAccessKeyAsyncWhenJavaServiceReturnsFailureShouldReturnFailedResult()
        {
            var userId = Guid.NewGuid();

            _userRepositoryMock
                .Setup(repository => repository.GetUserByIdAsync(userId))
                .ReturnsAsync(new User { Id = userId, Username = "trinity", Email = "trinity@spectrum.test" });

            _grpcClientMock
                .Setup(client => client.ClaimAccessKeyAsync(It.IsAny<ClaimKeyRequest>(), null, null, It.IsAny<CancellationToken>()))
                .Returns(CreateAsyncUnaryCall(new ClaimKeyResponse
                {
                    Success = false,
                    Message = "Challenge could not be claimed."
                }));

            var result = await _dropService.ClaimAccessKeyAsync(
                userId,
                "event-123",
                new ClaimDropDto { ChallengeCode = "READY" },
                CancellationToken.None
            );

            Assert.False(result.Success);
            Assert.Equal("Challenge could not be claimed.", result.Message);
        }

        [Fact]
        public async Task ClaimAccessKeyAsyncWhenJavaServiceIsDownShouldThrowSpectrumServiceUnavailableException()
        {
            var userId = Guid.NewGuid();

            _userRepositoryMock
                .Setup(repository => repository.GetUserByIdAsync(userId))
                .ReturnsAsync(new User { Id = userId, Username = "morpheus", Email = "morpheus@spectrum.test" });

            _grpcClientMock
                .Setup(client => client.ClaimAccessKeyAsync(It.IsAny<ClaimKeyRequest>(), null, null, It.IsAny<CancellationToken>()))
                .Throws(new RpcException(new Status(StatusCode.Unavailable, "Service Unavailable")));

            await Assert.ThrowsAsync<SpectrumServiceUnavailableException>(() =>
                _dropService.ClaimAccessKeyAsync(
                    userId,
                    "event-123",
                    new ClaimDropDto { ChallengeCode = "READY" },
                    CancellationToken.None
                ));
        }

        [Fact]
        public async Task CreateEventAsyncWhenDatesAreInvalidShouldRejectEvent()
        {
            var now = DateTime.UtcNow;
            var dto = new CreateDropEventDto
            {
                Title = "Invalid",
                GameTitle = "Halo",
                Platform = "PC",
                StartAt = now.AddDays(1),
                JoinDeadlineAt = now,
                RevealAt = now.AddHours(1),
                EndAt = now.AddHours(2),
                TotalSlots = 10,
                PublicChallengeCode = string.Empty,
                AccessKeys = ["DEMO-KEY"]
            };

            await Assert.ThrowsAsync<SpectrumBusinessException>(() =>
                _dropService.CreateEventAsync(dto, Guid.NewGuid(), CancellationToken.None));
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
    }
}
