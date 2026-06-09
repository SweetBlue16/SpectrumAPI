using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Spectrum.API.Controllers;
using Spectrum.API.Dtos.Reviews;
using Spectrum.API.Dtos.Votes;
using Spectrum.API.Exceptions;
using Spectrum.API.Services.Reviews;
using Spectrum.API.Services.Votes;
using Spectrum.API.Utilities;

namespace Spectrum.Tests.UnitTests.Controllers
{
    public class ReviewsControllerTests
    {
        [Theory]
        [InlineData("nameid", false)]
        [InlineData("sub", false)]
        [InlineData("userId", true)]
        [InlineData("invalid", false)]
        [InlineData("anonymous", false)]
        public async Task OptionalReadEndpointsShouldResolveClaimsAndAdminRole(string claimType, bool isAdmin)
        {
            var userId = Guid.NewGuid();
            var reviewId = Guid.NewGuid();
            Guid? capturedReviewUserId = Guid.Empty;
            Guid? capturedGameUserId = Guid.Empty;
            Guid? capturedCommentsUserId = Guid.Empty;
            bool capturedGameAdmin = false;
            bool capturedCommentsAdmin = false;
            var reviewServiceMock = new Mock<IReviewService>();
            reviewServiceMock
                .Setup(service => service.GetByIdAsync(reviewId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, Guid?, CancellationToken>((_, currentUserId, _) => capturedReviewUserId = currentUserId)
                .ReturnsAsync(new ReviewResponseDto { Id = reviewId });
            reviewServiceMock
                .Setup(service => service.GetByGameIdAsync(42, It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Callback<int, Guid?, bool, CancellationToken>((_, currentUserId, admin, _) =>
                {
                    capturedGameUserId = currentUserId;
                    capturedGameAdmin = admin;
                })
                .ReturnsAsync([]);
            var commentServiceMock = new Mock<IReviewCommentService>();
            commentServiceMock
                .Setup(service => service.GetByReviewAsync(reviewId, It.IsAny<Guid?>(), It.IsAny<bool>(), 1, It.IsAny<CancellationToken>()))
                .Callback<Guid, Guid?, bool, int, CancellationToken>((_, currentUserId, admin, _, _) =>
                {
                    capturedCommentsUserId = currentUserId;
                    capturedCommentsAdmin = admin;
                })
                .ReturnsAsync([]);
            var controller = CreateController(
                reviewServiceMock.Object,
                commentServiceMock.Object,
                Mock.Of<IVoteService>(),
                BuildPrincipal(claimType, userId, isAdmin));

            await controller.GetById(reviewId, CancellationToken.None);
            await controller.GetByGame(42, CancellationToken.None);
            await controller.GetComments(reviewId, page: 1, CancellationToken.None);

            var expectedUserId = claimType is "invalid" or "anonymous" ? (Guid?)null : userId;
            Assert.Equal(expectedUserId, capturedReviewUserId);
            Assert.Equal(expectedUserId, capturedGameUserId);
            Assert.Equal(expectedUserId, capturedCommentsUserId);
            Assert.Equal(isAdmin && claimType != "anonymous", capturedGameAdmin);
            Assert.Equal(isAdmin && claimType != "anonymous", capturedCommentsAdmin);
        }

        [Theory]
        [InlineData("anonymous")]
        [InlineData("invalid")]
        public async Task WriteEndpointsWhenUserIsMissingOrInvalidShouldThrowUnauthorized(string claimType)
        {
            var controller = CreateController(
                Mock.Of<IReviewService>(),
                Mock.Of<IReviewCommentService>(),
                Mock.Of<IVoteService>(),
                BuildPrincipal(claimType, Guid.NewGuid(), isAdmin: false));

            await Assert.ThrowsAsync<SpectrumUnauthorizedException>(() =>
                controller.Create(new CreateReviewDto(), CancellationToken.None));
        }

        [Fact]
        public async Task VoteWhenCurrentUserIsAdminShouldRejectBeforeCallingVoteService()
        {
            var reviewServiceMock = new Mock<IReviewService>();
            var commentServiceMock = new Mock<IReviewCommentService>();
            var voteServiceMock = new Mock<IVoteService>();
            var controller = new ReviewsController(
                reviewServiceMock.Object,
                commentServiceMock.Object,
                voteServiceMock.Object
            );
            var adminId = Guid.NewGuid();
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
                            new Claim(ClaimTypes.Role, Constants.Roles.Admin)
                        ],
                        "TestAuth"
                    ))
                }
            };

            await Assert.ThrowsAsync<SpectrumForbiddenException>(() =>
                controller.Vote(Guid.NewGuid(), new CastReviewVoteDto { IsPositive = true }, CancellationToken.None));

            voteServiceMock.Verify(service => service.CastReviewVoteAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        private static ReviewsController CreateController(
            IReviewService reviewService,
            IReviewCommentService commentService,
            IVoteService voteService,
            ClaimsPrincipal user)
        {
            return new ReviewsController(reviewService, commentService, voteService)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = user }
                }
            };
        }

        private static ClaimsPrincipal BuildPrincipal(string claimType, Guid userId, bool isAdmin)
        {
            if (claimType == "anonymous")
            {
                return new ClaimsPrincipal(new ClaimsIdentity());
            }

            var value = claimType == "invalid" ? "not-a-guid" : userId.ToString();
            var claim = claimType switch
            {
                "sub" => new Claim("sub", value),
                "userId" => new Claim("userId", value),
                _ => new Claim(ClaimTypes.NameIdentifier, value)
            };
            var claims = new List<Claim> { claim };
            if (isAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, Constants.Roles.Admin));
            }

            return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        }
    }
}
