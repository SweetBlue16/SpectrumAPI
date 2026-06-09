using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Spectrum.API.Controllers;
using Spectrum.API.Services.External;

namespace Spectrum.Tests.UnitTests.Controllers
{
    public class AdminGamesControllerTests
    {
        [Fact]
        public void SyncCatalogShouldReturnAcceptedWithSelectedMode()
        {
            var syncServiceMock = new Mock<IRawgSyncService>();
            syncServiceMock
                .Setup(service => service.SyncCatalogAsync(true))
                .Returns(Task.CompletedTask);
            var controller = new AdminGamesController(syncServiceMock.Object, Mock.Of<ILogger<AdminGamesController>>());

            var result = controller.SyncCatalog(fullSync: true);

            var accepted = Assert.IsType<AcceptedResult>(result);
            Assert.NotNull(accepted.Value);
        }
    }
}
