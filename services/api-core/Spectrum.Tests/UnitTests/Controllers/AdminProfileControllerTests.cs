using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Spectrum.API.Controllers;
using Spectrum.API.Dtos.Admin;
using Spectrum.API.Services.Admin;

namespace Spectrum.Tests.UnitTests.Controllers
{
    public class AdminProfileControllerTests
    {
        [Fact]
        public async Task GetShouldResolveCurrentAdminAndReturnProfile()
        {
            var adminId = Guid.NewGuid();
            var serviceMock = new Mock<IAdminProfileService>();
            serviceMock
                .Setup(service => service.GetProfileAsync(adminId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AdminProfileDto { Id = adminId, Username = "admin" });
            var controller = CreateController(serviceMock.Object, adminId);

            var result = await controller.Get(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            var profile = Assert.IsType<AdminProfileDto>(ok.Value);
            Assert.Equal(adminId, profile.Id);
        }

        [Fact]
        public async Task UpdateShouldResolveCurrentAdminAndReturnUpdatedProfile()
        {
            var adminId = Guid.NewGuid();
            var dto = new UpdateAdminProfileDto
            {
                Username = "admin",
                FirstName = "Ana",
                LastName = "Admin",
                PhoneNumber = "+52551111",
                Address = "Address"
            };
            var serviceMock = new Mock<IAdminProfileService>();
            serviceMock
                .Setup(service => service.UpdateProfileAsync(adminId, dto, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AdminProfileDto { Id = adminId, Username = "admin" });
            var controller = CreateController(serviceMock.Object, adminId);

            var result = await controller.Update(dto, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.IsType<AdminProfileDto>(ok.Value);
        }

        [Fact]
        public async Task GetWhenCurrentAdminClaimIsInvalidShouldThrowUnauthorizedAccess()
        {
            var controller = new AdminProfileController(Mock.Of<IAdminProfileService>())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "bad-id")]))
                    }
                }
            };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Get(CancellationToken.None));
        }

        private static AdminProfileController CreateController(IAdminProfileService service, Guid adminId)
        {
            return new AdminProfileController(service)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, adminId.ToString())]))
                    }
                }
            };
        }
    }
}
