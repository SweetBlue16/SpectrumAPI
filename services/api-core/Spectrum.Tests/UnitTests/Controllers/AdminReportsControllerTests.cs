using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Spectrum.API.Controllers;
using Spectrum.API.Dtos.Reports;
using Spectrum.API.Models;
using Spectrum.API.Services.Profile;
using Spectrum.API.Services.Reports;
using Spectrum.API.Services.Reviews;
using Spectrum.API.Utilities;
using Spectrum.Tests.Helpers;

namespace Spectrum.Tests.UnitTests.Controllers
{
    public class AdminReportsControllerTests
    {
        private readonly Mock<IReportService> _reportServiceMock = new();
        private readonly Mock<IReviewService> _reviewServiceMock = new();
        private readonly Mock<IUserModerationService> _userModerationServiceMock = new();

        [Fact]
        public async Task ListShouldNormalizeFiltersSortAndEnrichReports()
        {
            await using var context = TestDbContextFactory.CreateContext();
            var reporterId = Guid.NewGuid();
            var review = new Review
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                GameId = 10,
                Rating = 8,
                Title = "Review title",
                Content = "Review content",
                CreatedAt = DateTime.UtcNow
            };
            await context.Users.AddAsync(new User
            {
                Id = reporterId,
                Username = "reporter",
                Email = "reporter@test.com",
                PasswordHash = "hash"
            });
            await context.Reviews.AddAsync(review);
            await context.SaveChangesAsync();
            _reportServiceMock
                .Setup(service => service.GetReportsByStatusAsync("PENDING", It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    new ReportDetailsDto
                    {
                        ReportId = "report-1",
                        ReporterId = reporterId.ToString(),
                        TargetId = review.Id.ToString(),
                        TargetType = "REVIEW",
                        Reason = "spam",
                        Status = "PENDING",
                        ReportedAt = DateTime.UtcNow
                    }
                ]);
            var controller = CreateController(context, Guid.NewGuid());

            var result = await controller.List(
                page: 0,
                pageSize: 100,
                status: "PENDING",
                targetType: "review",
                search: "spam",
                sort: "type",
                CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            var page = Assert.IsType<PagedResult<ReportDetailsDto>>(ok.Value);
            Assert.Equal(1, page.Page);
            Assert.Equal(50, page.PageSize);
            Assert.Equal("reporter", page.Items.Single().ReporterUsername);
            Assert.Contains("Review title", page.Items.Single().TargetContentSnippet);
        }

        [Fact]
        public async Task ResolveShouldUseCurrentAdminIdAndDelegateStatusUpdate()
        {
            await using var context = TestDbContextFactory.CreateContext();
            var adminId = Guid.NewGuid();
            var dto = new UpdateReportStatusDto { NewStatus = "RESOLVED", ResolutionNotes = "done" };
            var controller = CreateController(context, adminId);

            var result = await controller.Resolve("report-1", dto, CancellationToken.None);

            Assert.IsType<OkObjectResult>(result);
            _reportServiceMock.Verify(
                service => service.UpdateReportStatusAsync("report-1", adminId, dto, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task DeleteReportedContentWhenReportTargetsReviewShouldDeleteAndResolve()
        {
            await using var context = TestDbContextFactory.CreateContext();
            var adminId = Guid.NewGuid();
            var reviewId = Guid.NewGuid();
            _reportServiceMock
                .Setup(service => service.GetReportsByStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string status, CancellationToken _) => status == "PENDING"
                    ? [new ReportDetailsDto { ReportId = "report-1", TargetId = reviewId.ToString(), TargetType = "REVIEW" }]
                    : []);
            var controller = CreateController(context, adminId);

            var result = await controller.DeleteReportedContent(
                "report-1",
                new UpdateReportStatusDto { ResolutionNotes = "policy" },
                CancellationToken.None);

            Assert.IsType<OkObjectResult>(result);
            _reviewServiceMock.Verify(
                service => service.DeleteAsync(reviewId, adminId, true, "policy", It.IsAny<CancellationToken>()),
                Times.Once);
            _reportServiceMock.Verify(
                service => service.UpdateReportStatusAsync(
                    "report-1",
                    adminId,
                    It.Is<UpdateReportStatusDto>(dto => dto.NewStatus == "RESOLVED" && dto.ResolutionNotes == "policy"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SuspendReportedAuthorWhenReviewTargetExistsShouldSuspendReviewAuthor()
        {
            await using var context = TestDbContextFactory.CreateContext();
            var adminId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var reviewId = Guid.NewGuid();
            await context.Reviews.AddAsync(new Review
            {
                Id = reviewId,
                UserId = authorId,
                GameId = 10,
                Rating = 8,
                Title = "Reported",
                Content = "Content"
            });
            await context.SaveChangesAsync();
            _reportServiceMock
                .Setup(service => service.GetReportsByStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string status, CancellationToken _) => status == "PENDING"
                    ? [new ReportDetailsDto { ReportId = "report-1", TargetId = reviewId.ToString(), TargetType = "REVIEW" }]
                    : []);
            var controller = CreateController(context, adminId);

            var result = await controller.SuspendReportedAuthor(
                "report-1",
                new UpdateReportStatusDto { AdminNotes = "abuse" },
                CancellationToken.None);

            Assert.IsType<OkObjectResult>(result);
            _userModerationServiceMock.Verify(
                service => service.ToggleSuspensionAsync(authorId, true, adminId, "abuse", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        private AdminReportsController CreateController(Spectrum.API.Data.SpectrumDbContext context, Guid adminId)
        {
            return new AdminReportsController(
                _reportServiceMock.Object,
                _reviewServiceMock.Object,
                _userModerationServiceMock.Object,
                context)
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
