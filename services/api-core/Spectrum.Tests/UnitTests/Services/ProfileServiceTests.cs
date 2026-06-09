using Microsoft.EntityFrameworkCore;
using Moq;
using Spectrum.API.Data;
using Spectrum.API.Dtos.Profile;
using Spectrum.API.Exceptions;
using Spectrum.API.Models;
using Spectrum.API.Repositories;
using Spectrum.API.Services.Auth;
using Spectrum.API.Services.Email;
using Spectrum.API.Services.Profile;
using Spectrum.API.Services.Storage;
using Spectrum.API.Utilities;
using Spectrum.Tests.Helpers;

namespace Spectrum.Tests.UnitTests.Services
{
    public class ProfileServiceTests : IDisposable
    {
        private readonly SpectrumDbContext _context;
        private readonly Mock<IUserRepository> _userRepositoryMock;
        private readonly Mock<IImageStorageService> _imageStorageServiceMock;
        private readonly Mock<IVerificationCodeService> _verificationCodeServiceMock;
        private readonly Mock<IEmailService> _emailServiceMock;
        private readonly ProfileService _profileService;

        public ProfileServiceTests()
        {
            var options = new DbContextOptionsBuilder<SpectrumDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new SpectrumDbContext(options);

            _userRepositoryMock = new Mock<IUserRepository>();
            _imageStorageServiceMock = new Mock<IImageStorageService>();
            _verificationCodeServiceMock = new Mock<IVerificationCodeService>();
            _emailServiceMock = new Mock<IEmailService>();
            _profileService = new ProfileService(
                _userRepositoryMock.Object,
                _context,
                _imageStorageServiceMock.Object,
                _verificationCodeServiceMock.Object,
                _emailServiceMock.Object
            );
        }

        [Fact]
        public async Task TestGetUserProfileWhenUserExistsShouldReturnUserProfileDto()
        {
            var userId = Guid.NewGuid();
            var user = GenerateValidUser(userId);

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            _userRepositoryMock.Setup(repo => repo.GetUserWithProfileDataAsync(It.IsAny<Guid>()))
                               .ReturnsAsync(user);

            var result = await _profileService.GetUserProfileAsync(userId);

            Assert.NotNull(result);
            Assert.Equal(user.Username, result.Username);
        }

        [Fact]
        public async Task TestGetUserProfileWhenUserDoesNotExistShouldThrowSpectrumNotFoundException()
        {
            var userId = Guid.NewGuid();
            _userRepositoryMock.Setup(repo => repo.GetUserWithProfileDataAsync(userId))
                               .ReturnsAsync((User)null!);

            var exception = await Assert.ThrowsAsync<SpectrumNotFoundException>(() =>
                _profileService.GetUserProfileAsync(userId));

            Assert.Equal("The requested user profile was not found.", exception.Message);
        }

        [Fact]
        public async Task TestUpdateProfileWhenValidDataIsProvidedShouldUpdateSuccessfully()
        {
            var userId = Guid.NewGuid();
            var user = GenerateValidUser(userId);
            var updateDto = new UserProfileDto
            {
                Id = userId,
                Username = "newname",
                Biography = "Actualizando el perfil",
                InterestedGames = new List<ProfileGameDto>(),
                Platforms = new List<ProfilePlatformDto>()
            };

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            _userRepositoryMock.Setup(repo => repo.GetUserWithProfileDataAsync(It.IsAny<Guid>()))
                               .ReturnsAsync(user);

            _userRepositoryMock.Setup(repo => repo.UpdateUserProfileCollectionsAsync(It.IsAny<User>(), It.IsAny<List<Guid>>(), It.IsAny<List<int>>()))
                               .Returns(Task.CompletedTask);

            await _profileService.UpdateUserProfileAsync(userId, updateDto);

            _userRepositoryMock.Verify(repo => repo.UpdateUserProfileCollectionsAsync(It.IsAny<User>(), It.IsAny<List<Guid>>(), It.IsAny<List<int>>()), Times.Once);
        }

        [Fact]
        public async Task TestUpdateProfileWhenUserDoesNotExistShouldThrowSpectrumNotFoundException()
        {
            var userId = Guid.NewGuid();
            var updateDto = new UserProfileDto { Id = userId, Username = "newname" };

            _userRepositoryMock.Setup(repo => repo.GetUserWithProfileDataAsync(userId))
                               .ReturnsAsync((User)null!);

            var exception = await Assert.ThrowsAsync<SpectrumNotFoundException>(() =>
                _profileService.UpdateUserProfileAsync(userId, updateDto));

            Assert.Equal("User not found.", exception.Message);
        }

        [Fact]
        public async Task TestGetPublicUserProfileWhenUserExistsShouldReturnProfileWithEmptyEmail()
        {
            var userId = Guid.NewGuid();
            var user = GenerateValidUser(userId);

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            _userRepositoryMock.Setup(repo => repo.GetUserWithProfileDataAsync(It.IsAny<Guid>()))
                               .ReturnsAsync(user);

            var result = await _profileService.GetPublicUserProfileAsync(userId);

            Assert.NotNull(result);
            Assert.Equal(string.Empty, result.Email);
            Assert.Equal(user.Username, result.Username);
        }

        [Fact]
        public async Task TestChangePasswordWhenCurrentPasswordIsCorrectShouldUpdatePasswordHash()
        {
            var userId = Guid.NewGuid();
            var user = GenerateValidUser(userId);
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword123!");

            var dto = new ChangePasswordDto
            {
                CurrentPassword = "OldPassword123!",
                NewPassword = "NewPassword456!"
            };

            _userRepositoryMock.Setup(repo => repo.GetUserByIdAsync(It.IsAny<Guid>()))
                               .ReturnsAsync(user);
            _userRepositoryMock.Setup(repo => repo.UpdateUserAsync(It.IsAny<User>()))
                               .Returns(Task.CompletedTask);

            await _profileService.ChangePasswordAsync(userId, dto);

            _userRepositoryMock.Verify(repo => repo.UpdateUserAsync(It.Is<User>(u =>
                BCrypt.Net.BCrypt.Verify("NewPassword456!", u.PasswordHash))), Times.Once);
        }

        [Fact]
        public async Task TestChangePasswordWhenCurrentPasswordIsIncorrectShouldThrowUnauthorized()
        {
            var userId = Guid.NewGuid();
            var user = GenerateValidUser(userId);
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword123!");

            var dto = new ChangePasswordDto
            {
                CurrentPassword = "WrongPassword!",
                NewPassword = "NewPassword456!"
            };

            _userRepositoryMock.Setup(repo => repo.GetUserByIdAsync(It.IsAny<Guid>()))
                               .ReturnsAsync(user);

            var exception = await Assert.ThrowsAsync<SpectrumUnauthorizedException>(() =>
                _profileService.ChangePasswordAsync(userId, dto));
            Assert.Equal("The current password provided is incorrect.", exception.Message);
        }

        [Fact]
        public async Task TestUpdateAvatarShouldUploadImageToStorageAndUpdateUser()
        {
            var userId = Guid.NewGuid();
            var user = GenerateValidUser(userId);
            var mockFile = new Mock<Microsoft.AspNetCore.Http.IFormFile>();
            var expectedUrl = "https://s3.aws.com/spectrum/avatar.png";

            _userRepositoryMock.Setup(repo => repo.GetUserByIdAsync(It.IsAny<Guid>()))
                               .ReturnsAsync(user);
            _imageStorageServiceMock.Setup(s => s.UploadImageAsync(It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(), "photoProfiles"))
                                    .ReturnsAsync(expectedUrl);

            var result = await _profileService.UpdateAvatarAsync(userId, mockFile.Object);

            Assert.Equal(expectedUrl, result);
            Assert.Equal(expectedUrl, user.ProfilePicture);
            _userRepositoryMock.Verify(repo => repo.UpdateUserAsync(user), Times.Once);
        }

        [Fact]
        public async Task TestBlockUserWhenValidDataShouldAddUserBlockRecord()
        {
            var blockerId = Guid.NewGuid();
            var blockedId = Guid.NewGuid();
            var blockedUser = GenerateValidUser(blockedId);
            var dto = new BlockUserDto { Reason = "Comportamiento indebido" };

            _userRepositoryMock.Setup(repo => repo.GetUserByIdAsync(blockedId))
                               .ReturnsAsync(blockedUser);

            await _profileService.BlockUserAsync(blockerId, blockedId, dto);

            var blockRecord = await _context.UserBlocks.FirstOrDefaultAsync(b =>
                b.BlockerUserId == blockerId && b.BlockedUserId == blockedId);

            Assert.NotNull(blockRecord);
            Assert.Equal("Comportamiento indebido", blockRecord.Reason);
        }

        [Fact]
        public async Task TestBlockUserWhenBlockingSelfShouldThrowBusinessException()
        {
            var userId = Guid.NewGuid();
            var dto = new BlockUserDto();

            var exception = await Assert.ThrowsAsync<SpectrumBusinessException>(() =>
                _profileService.BlockUserAsync(userId, userId, dto));
            Assert.Equal("cannotBlockSelf", exception.Message);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        private User GenerateValidUser(Guid userId)
        {
            return new User
            {
                Id = userId,
                Username = "spectrumer",
                Email = "test@spectrum.com",
                Role = Constants.Roles.Reviewer,
                IsBanned = false,
                IsEmailVerified = true,
                InterestedGames = new List<Game>(),
                Platforms = new List<Platform>()
            };
        }
    }
}
