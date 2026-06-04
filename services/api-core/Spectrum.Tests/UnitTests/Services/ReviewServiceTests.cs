using Moq;
using Spectrum.API.Dtos.Reviews;
using Spectrum.API.Exceptions;
using Spectrum.API.Models;
using Spectrum.API.Repositories;
using Spectrum.API.Services.Reviews;
using Spectrum.API.Services.Votes;

namespace Spectrum.Tests.UnitTests.Services
{
    public class ReviewServiceTests
    {
        private readonly Mock<IReviewRepository> _reviewRepositoryMock;
        private readonly ReviewService _reviewService;

        public ReviewServiceTests()
        {
            _reviewRepositoryMock = new Mock<IReviewRepository>();
            _reviewService = new ReviewService(_reviewRepositoryMock.Object);
        }

        [Fact]
        public async Task UpdateAsyncWhenReviewBelongsToAnotherUserShouldThrowForbidden()
        {
            var ownerId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();
            var review = CreateReview(ownerId);

            _reviewRepositoryMock
                .Setup(repository => repository.GetByIdAsync(review.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(review);

            await Assert.ThrowsAsync<SpectrumForbiddenException>(() =>
                _reviewService.UpdateAsync(review.Id, new UpdateReviewDto { Content = "Updated" }, requesterId));

            _reviewRepositoryMock.Verify(
                repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never
            );
        }

        [Fact]
        public async Task DeleteAsyncWhenReviewBelongsToAnotherUserShouldThrowForbidden()
        {
            var ownerId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();
            var review = CreateReview(ownerId);

            _reviewRepositoryMock
                .Setup(repository => repository.GetByIdAsync(review.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(review);

            await Assert.ThrowsAsync<SpectrumForbiddenException>(() =>
                _reviewService.DeleteAsync(review.Id, requesterId));

            _reviewRepositoryMock.Verify(
                repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never
            );
        }

        [Fact]
        public async Task UpdateAsyncWhenReviewBelongsToUserShouldUpdateAndSave()
        {
            var ownerId = Guid.NewGuid();
            var review = CreateReview(ownerId);
            var dto = new UpdateReviewDto
            {
                Rating = 5,
                Content = "  Great update  ",
                ImageUrl = "https://cdn.example.com/review.png"
            };

            _reviewRepositoryMock
                .Setup(repository => repository.GetByIdAsync(review.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(review);

            await _reviewService.UpdateAsync(review.Id, dto, ownerId);

            Assert.Equal(5, review.Rating);
            Assert.Equal("Great update", review.Content);
            Assert.Equal(dto.ImageUrl, review.ImageUrl);
            Assert.NotNull(review.UpdatedAt);
            _reviewRepositoryMock.Verify(
                repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once
            );
        }

        [Fact]
        public async Task DeleteAsyncWhenReviewBelongsToUserShouldSoftDeleteAndSave()
        {
            var ownerId = Guid.NewGuid();
            var review = CreateReview(ownerId);

            _reviewRepositoryMock
                .Setup(repository => repository.GetByIdAsync(review.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(review);

            await _reviewService.DeleteAsync(review.Id, ownerId);

            Assert.True(review.IsDeleted);
            Assert.NotNull(review.UpdatedAt);
            _reviewRepositoryMock.Verify(
                repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once
            );
        }

        [Fact]
        public async Task GetByIdAsyncShouldCalculateIsOwnReview()
        {
            var ownerId = Guid.NewGuid();
            var review = CreateReview(ownerId);

            _reviewRepositoryMock
                .Setup(repository => repository.GetByIdAsync(review.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(review);

            var ownResult = await _reviewService.GetByIdAsync(review.Id, ownerId);
            var anonymousResult = await _reviewService.GetByIdAsync(review.Id);
            var otherUserResult = await _reviewService.GetByIdAsync(review.Id, Guid.NewGuid());

            Assert.True(ownResult.IsOwnReview);
            Assert.False(anonymousResult.IsOwnReview);
            Assert.False(otherUserResult.IsOwnReview);
            Assert.Equal(review.User!.ProfilePicture, ownResult.UserProfileImageUrl);
        }

        [Fact]
        public async Task GetByIdAsyncShouldIncludeCurrentUserVoteWhenProvidedByVoteService()
        {
            var ownerId = Guid.NewGuid();
            var currentUserId = Guid.NewGuid();
            var review = CreateReview(ownerId);
            var voteServiceMock = new Mock<IVoteService>();
            var service = new ReviewService(_reviewRepositoryMock.Object, voteService: voteServiceMock.Object);

            _reviewRepositoryMock
                .Setup(repository => repository.GetByIdAsync(review.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(review);
            voteServiceMock
                .Setup(service => service.GetCurrentReviewVotesAsync(
                    It.Is<IEnumerable<Guid>>(ids => ids.Contains(review.Id)),
                    currentUserId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string> { [review.Id] = "like" });

            var result = await service.GetByIdAsync(review.Id, currentUserId);

            Assert.Equal("like", result.CurrentUserVote);
            Assert.Equal("like", result.UserVote);
            Assert.Equal("like", result.MyVote);
        }

        private static Review CreateReview(Guid ownerId)
        {
            return new Review
            {
                Id = Guid.NewGuid(),
                UserId = ownerId,
                GameId = 123,
                Rating = 4,
                Content = "Original review",
                CreatedAt = DateTime.UtcNow,
                User = new User
                {
                    Id = ownerId,
                    Username = "reviewer",
                    ProfilePicture = "https://cdn.example.com/user.png"
                }
            };
        }
    }
}
