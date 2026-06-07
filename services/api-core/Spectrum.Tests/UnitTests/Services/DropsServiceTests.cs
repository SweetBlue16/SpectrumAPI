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
        public async Task ClaimAccessKeyAsyncWhenRewardEmailFailsShouldMarkDeliveryFailedAndReturnWinner()
        {
            var userId = Guid.NewGuid();
            var eventId = "event-123";
            var claimedAtEpoch = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            _userRepositoryMock
                .Setup(repository => repository.GetUserByIdAsync(userId))
                .ReturnsAsync(new User { Id = userId, Username = "neo", Email = "neo@spectrum.test" });

            _grpcClientMock
                .Setup(client => client.ClaimAccessKeyAsync(It.IsAny<ClaimKeyRequest>(), null, null, It.IsAny<CancellationToken>()))
                .Returns(CreateAsyncUnaryCall(new ClaimKeyResponse
                {
                    Success = true,
                    AccessKeyCode = "DHA3-SDFE-32EF-SF5R",
                    WinnerUserId = userId.ToString(),
                    WinnerUsername = "neo",
                    ClaimedAt = claimedAtEpoch
                }));
            _grpcClientMock
                .Setup(client => client.GetEventStatusAsync(It.IsAny<GetEventRequest>(), null, null, It.IsAny<CancellationToken>()))
                .Returns(CreateAsyncUnaryCall(new EventStatusResponse
                {
                    EventId = eventId,
                    GameTitle = "Halo",
                    Platform = "PC",
                    Status = "REVEAL_READY",
                    StartAt = claimedAtEpoch,
                    JoinDeadlineAt = claimedAtEpoch,
                    RevealAt = claimedAtEpoch,
                    EndDate = claimedAtEpoch
                }));
            _rewardDeliveryServiceMock
                .Setup(service => service.SendRewardAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("smtp failed"));
            _grpcClientMock
                .Setup(client => client.MarkRewardDeliveryFailedAsync(
                    It.Is<MarkRewardDeliveryFailedRequest>(request =>
                        request.EventId == eventId &&
                        request.WinnerUserId == userId.ToString() &&
                        request.FailedAt > 0),
                    null,
                    null,
                    It.IsAny<CancellationToken>()))
                .Returns(CreateAsyncUnaryCall(new EventActionResponse
                {
                    Success = true,
                    EventId = eventId
                }));

            var result = await _dropService.ClaimAccessKeyAsync(userId, eventId, new ClaimDropDto(), CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("neo", result.WinnerUsername);
            _grpcClientMock.Verify(client => client.MarkRewardDeliveryFailedAsync(
                It.IsAny<MarkRewardDeliveryFailedRequest>(),
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

        [Fact]
        public async Task CreateEventAsyncWhenStartDateIsInPastShouldRejectEventWithSpecificMessage()
        {
            var now = DateTime.UtcNow;
            var dto = new CreateDropEventDto
            {
                Title = "Past",
                GameTitle = "Halo",
                Platform = "PC",
                StartAt = now.AddMinutes(-5),
                JoinDeadlineAt = now.AddHours(1),
                RevealAt = now.AddHours(2),
                EndAt = now.AddHours(3),
                TotalSlots = 10,
                AccessKeys = ["DHA3-SDFE-32EF-SF5R"]
            };

            var exception = await Assert.ThrowsAsync<SpectrumBusinessException>(() =>
                _dropService.CreateEventAsync(dto, Guid.NewGuid(), CancellationToken.None));

            Assert.Equal("eventStartInPast", exception.Message);
        }

        [Fact]
        public async Task CreateEventAsyncShouldPublishAutomaticallyAndNormalizeRewardCodes()
        {
            var adminId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var dto = new CreateDropEventDto
            {
                Title = "Launch",
                GameTitle = "Halo",
                Platform = "PC",
                StartAt = now.AddHours(1),
                JoinDeadlineAt = now.AddHours(2),
                RevealAt = now.AddHours(3),
                EndAt = now.AddHours(4),
                TotalSlots = 10,
                AccessKeys = ["dha3-sdfe-32ef-sf5r"]
            };

            _grpcClientMock
                .Setup(client => client.CreateEventAsync(
                    It.Is<CreateEventRequest>(request =>
                        request.PublishNow &&
                        request.AccessKeys.Count == 1 &&
                        request.AccessKeys[0] == "DHA3-SDFE-32EF-SF5R"),
                    null,
                    null,
                    It.IsAny<CancellationToken>()))
                .Returns(CreateAsyncUnaryCall(new EventActionResponse
                {
                    Success = true,
                    EventId = "created"
                }));

            var result = await _dropService.CreateEventAsync(dto, adminId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("created", result.EventId);
        }

        [Theory]
        [InlineData("")]
        [InlineData("DEMO-KEY-1")]
        [InlineData("DHA3-SDFE-32EF")]
        [InlineData("DHA3-SDFE-32EF-SF5!")]
        public async Task CreateEventAsyncWhenRewardCodeFormatIsInvalidShouldRejectEvent(string rewardCode)
        {
            var now = DateTime.UtcNow;
            var dto = new CreateDropEventDto
            {
                Title = "Invalid",
                GameTitle = "Halo",
                Platform = "PC",
                StartAt = now.AddHours(1),
                JoinDeadlineAt = now.AddHours(2),
                RevealAt = now.AddHours(3),
                EndAt = now.AddHours(4),
                TotalSlots = 10,
                AccessKeys = [rewardCode]
            };

            await Assert.ThrowsAsync<SpectrumBusinessException>(() =>
                _dropService.CreateEventAsync(dto, Guid.NewGuid(), CancellationToken.None));
        }

        [Fact]
        public async Task CreateEventAsyncWhenRewardCodesAreDuplicatedShouldRejectEvent()
        {
            var now = DateTime.UtcNow;
            var dto = new CreateDropEventDto
            {
                Title = "Duplicated",
                GameTitle = "Halo",
                Platform = "PC",
                StartAt = now.AddHours(1),
                JoinDeadlineAt = now.AddHours(2),
                RevealAt = now.AddHours(3),
                EndAt = now.AddHours(4),
                TotalSlots = 10,
                AccessKeys = ["DHA3-SDFE-32EF-SF5R", "dha3-sdfe-32ef-sf5r"]
            };

            await Assert.ThrowsAsync<SpectrumBusinessException>(() =>
                _dropService.CreateEventAsync(dto, Guid.NewGuid(), CancellationToken.None));
        }

        [Fact]
        public async Task UpdateEventAsyncWhenWithinTenMinutesBeforeStartShouldRejectWithExpectedMessage()
        {
            var now = DateTime.UtcNow;
            _grpcClientMock
                .Setup(client => client.GetEventStatusAsync(It.IsAny<GetEventRequest>(), null, null, It.IsAny<CancellationToken>()))
                .Returns(CreateAsyncUnaryCall(new EventStatusResponse
                {
                    EventId = "event-soon",
                    Status = "UPCOMING",
                    StartAt = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds(),
                    JoinDeadlineAt = DateTimeOffset.UtcNow.AddMinutes(20).ToUnixTimeMilliseconds(),
                    RevealAt = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeMilliseconds(),
                    EndDate = DateTimeOffset.UtcNow.AddMinutes(40).ToUnixTimeMilliseconds()
                }));

            var exception = await Assert.ThrowsAsync<SpectrumBusinessException>(() =>
                _dropService.UpdateEventAsync("event-soon", new UpdateDropEventDto
                {
                    Title = "Soon",
                    GameTitle = "Halo",
                    Platform = "PC",
                    StartAt = now.AddMinutes(5),
                    JoinDeadlineAt = now.AddMinutes(20),
                    RevealAt = now.AddMinutes(30),
                    EndAt = now.AddMinutes(40),
                    TotalSlots = 10,
                    AccessKeys = ["DHA3-SDFE-32EF-SF5R"]
                }, CancellationToken.None));

            Assert.Equal("Este evento está por comenzar, y no puede ser editado", exception.Message);
        }

        [Fact]
        public async Task GetUserWonKeysAsyncShouldNotExposeRewardCodes()
        {
            var userId = Guid.NewGuid();
            _grpcClientMock
                .Setup(client => client.GetWonKeysAsync(It.IsAny<WonKeysRequest>(), null, null, It.IsAny<CancellationToken>()))
                .Returns(CreateAsyncUnaryCall(new WonKeysResponse
                {
                    WonKeys =
                    {
                        new WonKey
                        {
                            EventId = "event-1",
                            GameTitle = "Halo",
                            AccessKeyCode = "DHA3-SDFE-32EF-SF5R",
                            ClaimedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            RewardDeliveryStatus = "SENT"
                        }
                    }
                }));

            var result = (await _dropService.GetUserWonKeysAsync(userId, CancellationToken.None)).ToList();

            Assert.Single(result);
            Assert.Equal(string.Empty, result[0].AccessKeyCode);
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
