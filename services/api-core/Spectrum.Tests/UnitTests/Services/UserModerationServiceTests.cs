using Moq;
using Spectrum.API.Exceptions;
using Spectrum.API.Models;
using Spectrum.API.Repositories;
using Spectrum.API.Services.Email;
using Spectrum.API.Services.Profile;
using Spectrum.API.Utilities;

namespace Spectrum.Tests.UnitTests.Services
{
    public class UserModerationServiceTests
    {
        private readonly Mock<IUserRepository> _userRepositoryMock;
        private readonly UserModerationService _service;

        public UserModerationServiceTests()
        {
            _userRepositoryMock = new Mock<IUserRepository>();
            _service = new UserModerationService(_userRepositoryMock.Object);
        }

        [Fact]
        public async Task TestToggleSuspensionAsyncWhenUserNotFoundShouldThrowSpectrumNotFoundException()
        {
            var userId = Guid.NewGuid();
            _userRepositoryMock.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync((User?)null);

            var exception = await Assert.ThrowsAsync<SpectrumNotFoundException>(() =>
                _service.ToggleSuspensionAsync(userId, true));

            Assert.Equal(Constants.ErrorMessages.UserNotFound, exception.Message);
        }

        [Fact]
        public async Task TestToggleSuspensionAsyncWhenUserIsAdminShouldThrowSpectrumForbiddenException()
        {
            var userId = Guid.NewGuid();
            var user = new User { Id = userId, Role = Constants.Roles.Admin };

            _userRepositoryMock.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);

            var exception = await Assert.ThrowsAsync<SpectrumForbiddenException>(() =>
                _service.ToggleSuspensionAsync(userId, true));

            Assert.Equal(Constants.ErrorMessages.AdminSanctionForbidden, exception.Message);
        }

        [Fact]
        public async Task TestToggleSuspensionAsyncWhenUserIsAlreadySuspendedShouldThrowSpectrumBusinessException()
        {
            var userId = Guid.NewGuid();
            var user = new User { Id = userId, Role = Constants.Roles.Reviewer, IsSuspended = true };

            _userRepositoryMock.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);

            var exception = await Assert.ThrowsAsync<SpectrumBusinessException>(() =>
                _service.ToggleSuspensionAsync(userId, true));

            Assert.Equal(Constants.ErrorMessages.AccountAlreadySuspended, exception.Message);
        }

        [Fact]
        public async Task TestToggleSuspensionAsyncWhenValidRequestShouldUpdateUserStatus()
        {
            var userId = Guid.NewGuid();
            var user = new User { Id = userId, Role = Constants.Roles.Reviewer, IsSuspended = false };

            _userRepositoryMock.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);
            _userRepositoryMock.Setup(r => r.UpdateUserAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            await _service.ToggleSuspensionAsync(userId, true);

            Assert.True(user.IsSuspended);
            _userRepositoryMock.Verify(r => r.UpdateUserAsync(user), Times.Once);
        }

        [Fact]
        public async Task ToggleSuspensionAsyncWhenSuspendingShouldSendEmail()
        {
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Role = Constants.Roles.Reviewer,
                IsSuspended = false,
                Email = "user@spectrum.test"
            };
            var emailServiceMock = new Mock<IEmailService>();
            var service = new UserModerationService(_userRepositoryMock.Object, emailServiceMock.Object);

            _userRepositoryMock.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);
            _userRepositoryMock.Setup(r => r.UpdateUserAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            await service.ToggleSuspensionAsync(userId, true);

            emailServiceMock.Verify(email => email.SendAccountSuspendedAsync(user.Email), Times.Once);
        }

        [Fact]
        public async Task ToggleSuspensionAsyncWhenReactivatingShouldSendEmail()
        {
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Role = Constants.Roles.Reviewer,
                IsSuspended = true,
                Email = "user@spectrum.test"
            };
            var emailServiceMock = new Mock<IEmailService>();
            var service = new UserModerationService(_userRepositoryMock.Object, emailServiceMock.Object);

            _userRepositoryMock.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);
            _userRepositoryMock.Setup(r => r.UpdateUserAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            await service.ToggleSuspensionAsync(userId, false);

            emailServiceMock.Verify(email => email.SendAccountReactivatedAsync(user.Email), Times.Once);
        }

        [Fact]
        public async Task DeleteUserAsyncWhenValidReviewerShouldDeactivateAndSendEmail()
        {
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Role = Constants.Roles.Reviewer,
                IsDeleted = false,
                Email = "user@spectrum.test"
            };
            var emailServiceMock = new Mock<IEmailService>();
            var service = new UserModerationService(_userRepositoryMock.Object, emailServiceMock.Object);

            _userRepositoryMock.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);
            _userRepositoryMock.Setup(r => r.UpdateUserAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            await service.DeleteUserAsync(userId);

            Assert.True(user.IsDeleted);
            Assert.True(user.IsSuspended);
            emailServiceMock.Verify(email => email.SendAccountBannedAsync(user.Email), Times.Once);
        }

        [Fact]
        public async Task TestGetUsersForModerationAsyncShouldReturnMappedPagedResult()
        {
            var pagedUsers = new PagedResult<User>
            {
                Items = new List<User>
                {
                    new User { Id = Guid.NewGuid(), Username = "test_user1", Role = Constants.Roles.Reviewer },
                    new User { Id = Guid.NewGuid(), Username = "admin_user", Role = Constants.Roles.Admin }
                },
                TotalCount = 2,
                Page = 1,
                PageSize = 10
            };

            _userRepositoryMock
                .Setup(r => r.GetPaginatedUsersAsync(1, 10, "test", It.IsAny<CancellationToken>()))
                .ReturnsAsync(pagedUsers);

            var result = await _service.GetUsersForModerationAsync(1, 10, "test");

            Assert.NotNull(result);
            Assert.Equal(2, result.TotalCount);
            Assert.Equal(2, result.Items.Count());
            Assert.Equal("test_user1", result.Items.First().Username);
        }
    }
}
