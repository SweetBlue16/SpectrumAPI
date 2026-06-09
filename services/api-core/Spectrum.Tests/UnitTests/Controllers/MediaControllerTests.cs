using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Spectrum.API.Controllers;
using Spectrum.API.Dtos.Media;
using Spectrum.API.Dtos.Votes;
using Spectrum.API.Services.Clips;
using Spectrum.API.Services.Storage;

namespace Spectrum.Tests.UnitTests.Controllers
{
    public class MediaControllerTests
    {
        private readonly Mock<IImageStorageService> _imageStorageServiceMock = new();
        private readonly Mock<IVideoStorageService> _videoStorageServiceMock = new();
        private readonly Mock<IGameClipService> _gameClipServiceMock = new();

        [Fact]
        public async Task UploadReviewAttachmentWhenImageShouldUseImageStorage()
        {
            var controller = CreateController();
            var file = CreateFile("image/png");
            _imageStorageServiceMock
                .Setup(service => service.UploadImageAsync(file, "review-attachments", 5))
                .ReturnsAsync("https://cdn.test/image.png");

            var result = await controller.UploadReviewAttachment(file);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("image", ok.Value!.ToString(), StringComparison.OrdinalIgnoreCase);
            _videoStorageServiceMock.Verify(
                service => service.UploadReviewVideoAsync(It.IsAny<IFormFile>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task UploadReviewAttachmentWhenVideoShouldUseVideoStorage()
        {
            var controller = CreateController();
            var file = CreateFile("video/mp4");
            _videoStorageServiceMock
                .Setup(service => service.UploadReviewVideoAsync(file, "review-attachments"))
                .ReturnsAsync("https://cdn.test/video.mp4");

            var result = await controller.UploadReviewAttachment(file);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("video", ok.Value!.ToString(), StringComparison.OrdinalIgnoreCase);
            _imageStorageServiceMock.Verify(
                service => service.UploadImageAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<int>()),
                Times.Never);
        }

        [Theory]
        [InlineData("sub")]
        [InlineData("nameid")]
        [InlineData("invalid")]
        public async Task CompleteDeleteAndVoteShouldResolveAuthenticatedUser(string claimType)
        {
            var userId = Guid.NewGuid();
            var clipId = Guid.NewGuid();
            var controller = CreateController(BuildPrincipal(claimType, userId));
            var request = new CompleteUploadRequestDto
            {
                UploadId = "upload-1",
                KeyName = "clips/key.mp4",
                GameId = 42,
                Title = "Launch clip",
                Etags = []
            };
            _videoStorageServiceMock
                .Setup(service => service.CompleteVideoUploadAsync(request))
                .ReturnsAsync("https://cdn.test/clip.mp4");
            _gameClipServiceMock
                .Setup(service => service.CastClipVoteAsync(clipId, userId, true))
                .ReturnsAsync(new VoteResultDto { Success = true });

            var complete = await controller.CompleteVideoUpload(request);
            var delete = await controller.DeleteClip(clipId);
            var vote = await controller.VoteClip(clipId, new CastReviewVoteDto { IsPositive = true });

            if (claimType == "invalid")
            {
                Assert.IsType<UnauthorizedResult>(complete);
                Assert.IsType<UnauthorizedResult>(delete);
                Assert.IsType<UnauthorizedResult>(vote);
                return;
            }

            Assert.IsType<OkObjectResult>(complete);
            Assert.IsType<NoContentResult>(delete);
            Assert.IsType<OkObjectResult>(vote);
            _gameClipServiceMock.Verify(service => service.CreateClipAsync(userId, request, "https://cdn.test/clip.mp4"), Times.Once);
            _gameClipServiceMock.Verify(service => service.DeleteClipAsync(clipId, userId), Times.Once);
            _gameClipServiceMock.Verify(service => service.CastClipVoteAsync(clipId, userId, true), Times.Once);
        }

        private MediaController CreateController(ClaimsPrincipal? user = null)
        {
            return new MediaController(
                _imageStorageServiceMock.Object,
                _videoStorageServiceMock.Object,
                _gameClipServiceMock.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = user ?? new ClaimsPrincipal(new ClaimsIdentity())
                    }
                }
            };
        }

        private static IFormFile CreateFile(string contentType)
        {
            var file = new Mock<IFormFile>();
            file.SetupGet(item => item.ContentType).Returns(contentType);
            file.SetupGet(item => item.FileName).Returns(contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? "image.png" : "clip.mp4");
            file.SetupGet(item => item.Length).Returns(128);
            return file.Object;
        }

        private static ClaimsPrincipal BuildPrincipal(string claimType, Guid userId)
        {
            var value = claimType == "invalid" ? "not-a-guid" : userId.ToString();
            var claim = claimType switch
            {
                "sub" => new Claim(JwtRegisteredClaimNames.Sub, value),
                _ => new Claim(ClaimTypes.NameIdentifier, value)
            };

            return new ClaimsPrincipal(new ClaimsIdentity([claim], "TestAuth"));
        }
    }
}
