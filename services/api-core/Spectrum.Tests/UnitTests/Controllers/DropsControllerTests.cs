using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Spectrum.API.Controllers;
using Spectrum.API.Dtos.Drops;
using Spectrum.API.Services.Drops;
using Spectrum.API.Utilities;
using System.Security.Claims;

namespace Spectrum.Tests.UnitTests.Controllers
{
    public class DropsControllerTests
    {
        private readonly Mock<IDropsService> _dropServiceMock;
        private readonly DropsController _controller;
        private readonly AdminDropsController _adminController;

        public DropsControllerTests()
        {
            _dropServiceMock = new Mock<IDropsService>();
            _controller = new DropsController(_dropServiceMock.Object);
            _adminController = new AdminDropsController(_dropServiceMock.Object);
        }

        [Fact]
        public async Task ClaimWhenUserAuthenticatedShouldReturnOkWithClaimResult()
        {
            var userId = Guid.NewGuid();
            SetupControllerUser(_controller, userId);

            var eventId = "event-123";
            var dto = new ClaimDropDto { ChallengeCode = "READY" };
            var expectedResult = new ClaimDropResultDto
            {
                Success = true,
                EventId = eventId,
                WinnerUserId = userId.ToString(),
                WinnerUsername = "neo"
            };

            _dropServiceMock
                .Setup(service => service.ClaimAccessKeyAsync(userId, eventId, dto, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            var result = await _controller.Claim(eventId, dto, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var returned = Assert.IsType<ClaimDropResultDto>(okResult.Value);
            Assert.True(returned.Success);
            Assert.Equal("neo", returned.WinnerUsername);
        }

        [Fact]
        public async Task JoinWhenUserAuthenticatedShouldReturnOk()
        {
            var userId = Guid.NewGuid();
            SetupControllerUser(_controller, userId);

            _dropServiceMock
                .Setup(service => service.JoinEventAsync(userId, "event-123", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DropActionResultDto { Success = true, EventId = "event-123" });

            var result = await _controller.Join("event-123", CancellationToken.None);

            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task JoinWhenUserIsAdminShouldThrowForbidden()
        {
            SetupControllerUser(_controller, Guid.NewGuid(), Constants.Roles.Admin);

            await Assert.ThrowsAsync<Spectrum.API.Exceptions.SpectrumForbiddenException>(() =>
                _controller.Join("event-123", CancellationToken.None));
        }

        [Fact]
        public async Task ClaimWhenUserIsAdminShouldThrowForbidden()
        {
            SetupControllerUser(_controller, Guid.NewGuid(), Constants.Roles.Admin);

            await Assert.ThrowsAsync<Spectrum.API.Exceptions.SpectrumForbiddenException>(() =>
                _controller.Claim("event-123", new ClaimDropDto(), CancellationToken.None));
        }

        [Fact]
        public async Task CreateWhenCalledByAdminShouldReturnCreated()
        {
            var adminId = Guid.NewGuid();
            SetupControllerUser(_adminController, adminId, Constants.Roles.Admin);

            var now = DateTime.UtcNow;
            var dto = new CreateDropEventDto
            {
                Title = "Halo launch",
                GameTitle = "Halo",
                Platform = "PC",
                StartAt = now.AddHours(1),
                JoinDeadlineAt = now.AddHours(2),
                RevealAt = now.AddHours(3),
                EndAt = now.AddHours(4),
                TotalSlots = 100,
                PublicChallengeCode = "READY"
            };

            _dropServiceMock
                .Setup(service => service.CreateEventAsync(dto, adminId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DropActionResultDto { Success = true, EventId = "new-event" });

            var result = await _adminController.Create(dto, CancellationToken.None);

            Assert.IsType<CreatedAtActionResult>(result);
            _dropServiceMock.Verify(service => service.CreateEventAsync(dto, adminId, It.IsAny<CancellationToken>()), Times.Once);
        }

        private static void SetupControllerUser(ControllerBase controller, Guid userId, string role = "REVIEWER")
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Role, role)
            }, "mock"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }
    }
}
