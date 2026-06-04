using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Spectrum.API.Controllers;
using Spectrum.API.Dtos.Votes;
using Spectrum.API.Exceptions;
using Spectrum.API.Services.Reviews;
using Spectrum.API.Services.Votes;
using Spectrum.API.Utilities;

namespace Spectrum.Tests.UnitTests.Controllers
{
    public class ReviewsControllerTests
    {
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
    }
}
