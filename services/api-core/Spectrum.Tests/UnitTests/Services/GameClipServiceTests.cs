using Microsoft.Extensions.Logging;
using Moq;
using Spectrum.API.Dtos.Media;
using Spectrum.API.Exceptions;
using Spectrum.API.Models;
using Spectrum.API.Repositories;
using Spectrum.API.Services.Clips;
using Spectrum.API.Services.Email;
using Spectrum.API.Utilities;

namespace Spectrum.Tests.UnitTests.Services
{
    public class GameClipServiceTests
    {
        private readonly Mock<IGameClipRepository> _clipRepositoryMock;
        private readonly Mock<IUserRepository> _userRepositoryMock;
        private readonly Mock<IGameRepository> _gameRepositoryMock;
        private readonly Mock<IEmailService> _emailServiceMock;
        private readonly Mock<ILogger<GameClipService>> _loggerMock;
        private readonly GameClipService _gameClipService;

        public GameClipServiceTests()
        {
            _clipRepositoryMock = new Mock<IGameClipRepository>();
            _userRepositoryMock = new Mock<IUserRepository>();
            _gameRepositoryMock = new Mock<IGameRepository>();
            _emailServiceMock = new Mock<IEmailService>();
            _loggerMock = new Mock<ILogger<GameClipService>>();

            _gameClipService = new GameClipService(
                _clipRepositoryMock.Object,
                _userRepositoryMock.Object,
                _gameRepositoryMock.Object,
                _emailServiceMock.Object,
                _loggerMock.Object
            );
        }

        [Theory]
        [InlineData("ab")]
        [InlineData("Este titulo es exageradamente largo y supera por mucho el limite de cien caracteres que la regla de negocio impone para la base de datos.")]
        public async Task TestCreateClipAsyncWhenTitleIsInvalidShouldThrowBusinessException(string invalidTitle)
        {
            var userId = Guid.NewGuid();
            var request = new CompleteUploadRequestDto { Title = invalidTitle, GameId = 123 };

            var exception = await Assert.ThrowsAsync<SpectrumBusinessException>(() =>
                _gameClipService.CreateClipAsync(userId, request, "http://video.url"));

            Assert.Equal("El título del clip debe tener entre 3 y 100 caracteres.", exception.Message);
        }

        [Fact]
        public async Task TestCreateClipAsyncWhenGameNotInDbButInCacheShouldSyncGameAndCreateClip()
        {
            var userId = Guid.NewGuid();
            var request = new CompleteUploadRequestDto { Title = "Jugada Epica", Description = "Desc", GameId = 3498 };
            var gameCache = new Game { Id = Guid.NewGuid(), Title = "GTA V" };

            _clipRepositoryMock.Setup(repo => repo.GameExistsAsync(It.IsAny<Guid>())).ReturnsAsync(false);
            _gameRepositoryMock.Setup(repo => repo.GetById(request.GameId)).Returns(gameCache);

            await _gameClipService.CreateClipAsync(userId, request, "http://video.url");

            _clipRepositoryMock.Verify(repo => repo.AddGameAsync(It.IsAny<Game>()), Times.Once);
            _clipRepositoryMock.Verify(repo => repo.AddClipAsync(It.Is<GameClip>(c => c.Title == "Jugada Epica" && c.Url == "http://video.url")), Times.Once);
            _clipRepositoryMock.Verify(repo => repo.SaveChangesAsync(), Times.Exactly(2));
        }

        [Fact]
        public async Task TestCreateClipAsyncWhenGameNotInDbAndNotInCacheShouldThrowNotFoundException()
        {
            var userId = Guid.NewGuid();
            var request = new CompleteUploadRequestDto { Title = "Valid Title", GameId = 9999 };

            _clipRepositoryMock.Setup(repo => repo.GameExistsAsync(It.IsAny<Guid>())).ReturnsAsync(false);
            _gameRepositoryMock.Setup(repo => repo.GetById(request.GameId)).Returns((Game)null!);

            await Assert.ThrowsAsync<SpectrumNotFoundException>(() =>
                _gameClipService.CreateClipAsync(userId, request, "http://video.url"));
        }

        [Fact]
        public async Task TestGetClipsByUserIdAsyncShouldReturnMappedClipsWithVoteCounts()
        {
            var userId = Guid.NewGuid();
            var clipId = Guid.NewGuid();
            var dummyClip = new GameClip { Id = clipId, Title = "Mi clip", Url = "url", UserId = userId };

            var countsDict = new Dictionary<Guid, (int Likes, int Dislikes)> { { clipId, (5, 1) } };
            var userVotesDict = new Dictionary<Guid, string> { { clipId, "like" } };

            _clipRepositoryMock.Setup(repo => repo.GetClipsByUserIdAsync(userId)).ReturnsAsync(new[] { dummyClip });
            _clipRepositoryMock.Setup(repo => repo.GetVoteCountsByClipIdsAsync(It.IsAny<Guid[]>())).ReturnsAsync(countsDict);
            _clipRepositoryMock.Setup(repo => repo.GetUserVotesByClipIdsAsync(It.IsAny<IEnumerable<Guid>>(), userId)).ReturnsAsync(userVotesDict);

            var results = (await _gameClipService.GetClipsByUserIdAsync(userId)).ToList();

            Assert.Single(results);
            Assert.Equal("Mi clip", results[0].Title);
            Assert.Equal(5, results[0].LikesCount);
            Assert.Equal(1, results[0].DislikesCount);
            Assert.Equal("like", results[0].UserVote);
        }

        [Fact]
        public async Task TestCastClipVoteAsyncWhenVotingOwnClipShouldThrowForbiddenException()
        {
            var userId = Guid.NewGuid();
            var clip = new GameClip { Id = Guid.NewGuid(), UserId = userId };

            _clipRepositoryMock.Setup(repo => repo.GetClipByIdAsync(clip.Id)).ReturnsAsync(clip);

            var exception = await Assert.ThrowsAsync<SpectrumForbiddenException>(() =>
                _gameClipService.CastClipVoteAsync(clip.Id, userId, true));

            Assert.Equal("No puedes votar tu propio clip.", exception.Message);
        }

        [Fact]
        public async Task TestCastClipVoteAsyncWhenExistingVoteHasSamePolarityShouldDeleteVote()
        {
            var userId = Guid.NewGuid();
            var clipId = Guid.NewGuid();
            var clip = new GameClip { Id = clipId, UserId = Guid.NewGuid() };
            var existingVote = new GameClipVote { ClipId = clipId, UserId = userId, IsPositive = true };

            _clipRepositoryMock.Setup(repo => repo.GetClipByIdAsync(clipId)).ReturnsAsync(clip);
            _clipRepositoryMock.Setup(repo => repo.GetVoteAsync(clipId, userId)).ReturnsAsync(existingVote);
            _clipRepositoryMock.Setup(repo => repo.GetVoteCountsByClipIdsAsync(It.IsAny<Guid[]>()))
                               .ReturnsAsync(new Dictionary<Guid, (int, int)>());

            await _gameClipService.CastClipVoteAsync(clipId, userId, true);

            _clipRepositoryMock.Verify(repo => repo.DeleteVoteAsync(existingVote), Times.Once);
            _clipRepositoryMock.Verify(repo => repo.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task TestDeleteClipAsyncWhenUserIsOwnerShouldDeleteWithoutEmail()
        {
            var ownerId = Guid.NewGuid();
            var clip = new GameClip { Id = Guid.NewGuid(), UserId = ownerId };

            _clipRepositoryMock.Setup(repo => repo.GetClipByIdAsync(clip.Id)).ReturnsAsync(clip);

            await _gameClipService.DeleteClipAsync(clip.Id, ownerId);

            _clipRepositoryMock.Verify(repo => repo.DeleteClipAsync(clip, ownerId), Times.Once);
            _emailServiceMock.Verify(email => email.SendClipDeletedAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task TestDeleteClipAsyncWhenUserIsAdminShouldDeleteAndSendEmail()
        {
            var adminId = Guid.NewGuid();
            var ownerUser = new User { Email = "owner@spectrum.com" };
            var clip = new GameClip { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Title = "Bad Clip", User = ownerUser };
            var adminUser = new User { Id = adminId, Role = Constants.Roles.Admin };

            _clipRepositoryMock.Setup(repo => repo.GetClipByIdAsync(clip.Id)).ReturnsAsync(clip);
            _userRepositoryMock.Setup(repo => repo.GetUserByIdAsync(adminId)).ReturnsAsync(adminUser);

            await _gameClipService.DeleteClipAsync(clip.Id, adminId);

            _clipRepositoryMock.Verify(repo => repo.DeleteClipAsync(clip, adminId), Times.Once);
            _emailServiceMock.Verify(email => email.SendClipDeletedAsync("owner@spectrum.com", "Bad Clip"), Times.Once);
        }

        [Fact]
        public async Task TestDeleteClipAsyncWhenUserIsNeitherOwnerNorAdminShouldThrowForbidden()
        {
            var randomUserId = Guid.NewGuid();
            var clip = new GameClip { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
            var randomUser = new User { Id = randomUserId, Role = Constants.Roles.Reviewer };

            _clipRepositoryMock.Setup(repo => repo.GetClipByIdAsync(clip.Id)).ReturnsAsync(clip);
            _userRepositoryMock.Setup(repo => repo.GetUserByIdAsync(randomUserId)).ReturnsAsync(randomUser);

            await Assert.ThrowsAsync<SpectrumForbiddenException>(() =>
                _gameClipService.DeleteClipAsync(clip.Id, randomUserId));
        }
    }
}
