using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Spectrum.API.Configuration;
using Spectrum.API.Exceptions;
using Spectrum.API.Services.Email;
using Spectrum.API.Utilities;

namespace Spectrum.Tests.UnitTests.Services
{
    public class SmtpEmailServiceTests
    {
        [Theory]
        [MemberData(nameof(EmailActions))]
        public async Task SendMethodsWhenSmtpOptionsAreIncompleteShouldThrowConfigurationError(
            Func<SmtpEmailService, Task> act)
        {
            var service = new SmtpEmailService(
                Options.Create(new SmtpOptions
                {
                    Host = string.Empty,
                    Username = "smtp-user",
                    Password = "smtp-password",
                    FromEmail = "noreply@spectrum.test"
                }),
                Mock.Of<ILogger<SmtpEmailService>>());

            var exception = await Assert.ThrowsAsync<SpectrumServiceUnavailableException>(() => act(service));

            Assert.Equal(Constants.ErrorMessages.SmtpConfigurationInvalid, exception.Message);
        }

        public static IEnumerable<object[]> EmailActions()
        {
            yield return [new Func<SmtpEmailService, Task>(service => service.SendRegistrationVerificationAsync("user@test.com", "123456"))];
            yield return [new Func<SmtpEmailService, Task>(service => service.SendPasswordResetAsync("user@test.com", "123456"))];
            yield return [new Func<SmtpEmailService, Task>(service => service.SendPasswordChangeAsync("user@test.com", "123456"))];
            yield return [new Func<SmtpEmailService, Task>(service => service.SendRewardAsync("user@test.com", "Launch", "CODE-1"))];
            yield return [new Func<SmtpEmailService, Task>(service => service.SendGiveawayWinnerEmailAsync("user@test.com", "Launch", "CODE-1"))];
            yield return [new Func<SmtpEmailService, Task>(service => service.SendReviewDeletedAsync("user@test.com", "Review"))];
            yield return [new Func<SmtpEmailService, Task>(service => service.SendClipDeletedAsync("user@test.com", "Clip"))];
            yield return [new Func<SmtpEmailService, Task>(service => service.SendAccountSuspendedAsync("user@test.com"))];
            yield return [new Func<SmtpEmailService, Task>(service => service.SendAccountBannedAsync("user@test.com"))];
            yield return [new Func<SmtpEmailService, Task>(service => service.SendAccountReactivatedAsync("user@test.com"))];
            yield return [new Func<SmtpEmailService, Task>(service => service.SendReportActionAsync("user@test.com", string.Empty))];
        }

        [Theory]
        [InlineData("smtp.test", "", "smtp-password", "noreply@spectrum.test")]
        [InlineData("smtp.test", "smtp-user", "", "noreply@spectrum.test")]
        [InlineData("smtp.test", "smtp-user", "smtp-password", "")]
        public async Task SendMethodsWhenAnyRequiredSmtpOptionIsMissingShouldThrowConfigurationError(
            string host,
            string username,
            string password,
            string fromEmail)
        {
            var service = new SmtpEmailService(
                Options.Create(new SmtpOptions
                {
                    Host = host,
                    Username = username,
                    Password = password,
                    FromEmail = fromEmail
                }),
                Mock.Of<ILogger<SmtpEmailService>>());

            var exception = await Assert.ThrowsAsync<SpectrumServiceUnavailableException>(() =>
                service.SendReportActionAsync("user@test.com", "Removed content."));

            Assert.Equal(Constants.ErrorMessages.SmtpConfigurationInvalid, exception.Message);
        }

        [Fact]
        public async Task SendMethodWhenConfiguredButRecipientIsInvalidShouldFailBeforeSmtpNetworkCall()
        {
            var service = new SmtpEmailService(
                Options.Create(new SmtpOptions
                {
                    Host = "smtp.test",
                    Username = "smtp-user",
                    Password = "smtp-password",
                    FromEmail = "noreply@spectrum.test"
                }),
                Mock.Of<ILogger<SmtpEmailService>>());

            await Assert.ThrowsAsync<FormatException>(() =>
                service.SendReportActionAsync("not-an-email", "Removed content."));
        }
    }
}
