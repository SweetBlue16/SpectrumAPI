using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Spectrum.API.Controllers;
using Spectrum.API.Dtos.Profile;
using Spectrum.API.Services.Profile;

namespace Spectrum.Tests.UnitTests.Controllers
{
    public class ProfileControllerTests
    {
        private readonly Mock<IProfileService> _profileServiceMock = new();

        [Theory]
        [InlineData("sub")]
        [InlineData("nameid")]
        [InlineData("invalid")]
        public async Task PasswordAndBlockEndpointsShouldResolveAuthenticatedUser(string claimType)
        {
            var currentUserId = Guid.NewGuid();
            var blockedUserId = Guid.NewGuid();
            var controller = CreateController(BuildPrincipal(claimType, currentUserId));

            var requestCode = await controller.RequestPasswordChangeCode();
            var verifyCode = await controller.VerifyPasswordChangeCode(new VerifyPasswordChangeCodeDto { Code = "123456" });
            var confirm = await controller.ConfirmPasswordChange(new ConfirmPasswordChangeDto
            {
                VerificationToken = "token",
                NewPassword = "Password123!"
            });
            var block = await controller.BlockUser(blockedUserId, new BlockUserDto { Reason = "spam" });

            if (claimType == "invalid")
            {
                Assert.IsType<UnauthorizedResult>(requestCode);
                Assert.IsType<UnauthorizedResult>(verifyCode);
                Assert.IsType<UnauthorizedResult>(confirm);
                Assert.IsType<UnauthorizedResult>(block);
                return;
            }

            Assert.IsType<OkObjectResult>(requestCode);
            Assert.IsType<OkObjectResult>(verifyCode);
            Assert.IsType<OkObjectResult>(confirm);
            Assert.IsType<NoContentResult>(block);
            _profileServiceMock.Verify(service => service.RequestPasswordChangeCodeAsync(currentUserId), Times.Once);
            _profileServiceMock.Verify(service => service.VerifyPasswordChangeCodeAsync(currentUserId, It.IsAny<VerifyPasswordChangeCodeDto>()), Times.Once);
            _profileServiceMock.Verify(service => service.ConfirmPasswordChangeAsync(currentUserId, It.IsAny<ConfirmPasswordChangeDto>()), Times.Once);
            _profileServiceMock.Verify(service => service.BlockUserAsync(currentUserId, blockedUserId, It.IsAny<BlockUserDto>()), Times.Once);
        }

        private ProfileController CreateController(ClaimsPrincipal user)
        {
            return new ProfileController(_profileServiceMock.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = user }
                }
            };
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
