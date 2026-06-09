using Microsoft.Extensions.Logging;
using Moq;
using Spectrum.API.Models;
using Spectrum.API.Services.Admin;
using Spectrum.API.Services.Email;

namespace Spectrum.Tests.UnitTests.Services
{
    public class AdminNotificationServiceTests
    {
        private readonly Mock<IEmailService> _emailServiceMock = new();
        private readonly AdminNotificationService _service;

        public AdminNotificationServiceTests()
        {
            _service = new AdminNotificationService(_emailServiceMock.Object, Mock.Of<ILogger<AdminNotificationService>>());
        }

        [Fact]
        public async Task NotifyReviewDeletedAsyncWhenUserHasEmailShouldSendReportAction()
        {
            var review = new Review
            {
                Id = Guid.NewGuid(),
                Title = "Review",
                User = new User { Email = "user@test.com" }
            };

            await _service.NotifyReviewDeletedAsync(review, "spam");

            _emailServiceMock.Verify(
                service => service.SendReportActionAsync(
                    "user@test.com",
                    It.Is<string>(message => message.Contains("Review") && message.Contains("spam"))),
                Times.Once);
        }

        [Theory]
        [MemberData(nameof(AccountNotifications))]
        public async Task AccountNotificationsShouldSendObservableMessage(
            Func<AdminNotificationService, User, Task> notify,
            string expectedMessage)
        {
            var user = new User { Id = Guid.NewGuid(), Email = "user@test.com" };

            await notify(_service, user);

            _emailServiceMock.Verify(
                service => service.SendReportActionAsync(
                    "user@test.com",
                    It.Is<string>(message => message.Contains(expectedMessage))),
                Times.Once);
        }

        [Fact]
        public async Task NotifyWhenEmailIsMissingShouldNotCallEmailService()
        {
            await _service.NotifyAccountDeletedAsync(new User { Id = Guid.NewGuid(), Email = " " }, "cleanup");

            _emailServiceMock.Verify(
                service => service.SendReportActionAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task NotifyWhenEmailServiceFailsShouldSwallowException()
        {
            _emailServiceMock
                .Setup(service => service.SendReportActionAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("smtp down"));

            await _service.NotifyAccountBannedAsync(new User { Id = Guid.NewGuid(), Email = "user@test.com" }, "abuse");

            _emailServiceMock.Verify(
                service => service.SendReportActionAsync("user@test.com", It.IsAny<string>()),
                Times.Once);
        }

        public static IEnumerable<object[]> AccountNotifications()
        {
            yield return [new Func<AdminNotificationService, User, Task>((service, user) => service.NotifyAccountSuspendedAsync(user, "abuse", "7 days")), "7 days"];
            yield return [new Func<AdminNotificationService, User, Task>((service, user) => service.NotifyAccountReactivatedAsync(user)), "reactivada"];
            yield return [new Func<AdminNotificationService, User, Task>((service, user) => service.NotifyAccountBannedAsync(user, "abuse")), "baneada"];
            yield return [new Func<AdminNotificationService, User, Task>((service, user) => service.NotifyAccountUnbannedAsync(user)), "retirado"];
            yield return [new Func<AdminNotificationService, User, Task>((service, user) => service.NotifyAccountDeletedAsync(user, "cleanup")), "desactivada"];
        }
    }
}
