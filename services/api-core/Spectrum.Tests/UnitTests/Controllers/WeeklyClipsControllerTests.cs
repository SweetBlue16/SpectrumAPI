using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Spectrum.API.Controllers;
using Spectrum.API.Dtos.Analytics;
using Spectrum.API.Dtos.Home;
using Spectrum.API.Services.Analytics;
using Spectrum.API.Utilities;

namespace Spectrum.Tests.UnitTests.Controllers
{
    public class WeeklyClipsControllerTests
    {
        [Theory]
        [InlineData("sub")]
        [InlineData("nameid")]
        [InlineData("invalid")]
        public async Task EndpointsShouldResolveOptionalCurrentUser(string claimType)
        {
            var expectedUserId = Guid.NewGuid();
            Guid? weeklyUserId = null;
            Guid? monthlyUserId = null;
            var analytics = new Mock<IAnalyticsService>();
            analytics
                .Setup(service => service.GetWeeklyClipsAsync(1, 10, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .Callback<int, int, Guid?, CancellationToken>((_, _, userId, _) => weeklyUserId = userId)
                .ReturnsAsync(new PagedResult<WeeklyReviewDto> { Items = [], Page = 1, PageSize = 10 });
            analytics
                .Setup(service => service.GetMonthlyTopClipsAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .Callback<Guid?, CancellationToken>((userId, _) => monthlyUserId = userId)
                .ReturnsAsync([]);
            var controller = new WeeklyClipsController(analytics.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = BuildPrincipal(claimType, expectedUserId)
                    }
                }
            };

            var weekly = await controller.GetWeeklyClips();
            var monthly = await controller.GetMonthlyTopClips();

            Assert.IsType<OkObjectResult>(weekly);
            Assert.IsType<OkObjectResult>(monthly);
            if (claimType == "invalid")
            {
                Assert.Null(weeklyUserId);
                Assert.Null(monthlyUserId);
            }
            else
            {
                Assert.Equal(expectedUserId, weeklyUserId);
                Assert.Equal(expectedUserId, monthlyUserId);
            }
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
