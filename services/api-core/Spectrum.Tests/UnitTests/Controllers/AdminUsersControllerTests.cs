using Microsoft.AspNetCore.Mvc;
using Moq;
using Spectrum.API.Controllers;
using Spectrum.API.Dtos.Profile;
using Spectrum.API.Services.Profile;
using Spectrum.API.Utilities;

namespace Spectrum.Tests.UnitTests.Controllers
{
    public class AdminUsersControllerTests
    {
        private readonly Mock<IUserModerationService> _moderationServiceMock;
        private readonly AdminUsersController _controller;

        public AdminUsersControllerTests()
        {
            _moderationServiceMock = new Mock<IUserModerationService>();
            _controller = new AdminUsersController(_moderationServiceMock.Object);
        }

        [Fact]
        public async Task TestSuspendUserShouldReturnOkResult()
        {
            var userId = Guid.NewGuid();
            _moderationServiceMock
                .Setup(s => s.ToggleSuspensionAsync(userId, true, null, null, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var result = await _controller.SuspendUser(userId, null, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
            _moderationServiceMock.Verify(s => s.ToggleSuspensionAsync(userId, true, null, null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task TestReactivateUserShouldReturnOkResult()
        {
            var userId = Guid.NewGuid();
            _moderationServiceMock
                .Setup(s => s.ReactivateUserAsync(userId, null, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var result = await _controller.ReactivateUser(userId, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
            _moderationServiceMock.Verify(s => s.ReactivateUserAsync(userId, null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task TestGetUsersShouldReturnOkWithPagedResult()
        {
            var expectedPagedResult = new PagedResult<UserModerationDto>
            {
                Items = new List<UserModerationDto>(),
                TotalCount = 0,
                Page = 1,
                PageSize = 10
            };

            _moderationServiceMock
                .Setup(s => s.GetUsersForModerationAsync(1, 10, null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedPagedResult);

            var result = await _controller.GetUsers(1, 10, null, null, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedResult = Assert.IsType<PagedResult<UserModerationDto>>(okResult.Value);
            Assert.Equal(expectedPagedResult.TotalCount, returnedResult.TotalCount);
        }
    }
}
