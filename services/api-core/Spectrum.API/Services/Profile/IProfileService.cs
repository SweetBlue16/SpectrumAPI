using Microsoft.EntityFrameworkCore;
using Spectrum.API.Data;
using Spectrum.API.Dtos.Profile;
using Spectrum.API.Exceptions;
using Spectrum.API.Models;
using Spectrum.API.Repositories;
using Spectrum.API.Services.Auth;
using Spectrum.API.Services.Email;
using Spectrum.API.Services.Storage;
using Spectrum.API.Utilities;

namespace Spectrum.API.Services.Profile
{
    /// <summary>
    /// Defines the contract for profile-related operations, including retrieval, updates, and account security.
    /// </summary>
    public interface IProfileService
    {
        /// <summary>
        /// Retrieves the profile information of a user based on their unique identifier.
        /// </summary>
        /// <returns>A <see cref="UserProfileDto"/> containing the user's profile data.</returns>
        Task<UserProfileDto> GetUserProfileAsync(Guid userId);

        /// <summary>
        /// Retrieves the public profile information of a user.
        /// Sensitive information is removed before returning the result.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>
        /// A <see cref="UserProfileDto"/> containing only public profile information.
        /// </returns>
        Task<UserProfileDto> GetPublicUserProfileAsync(Guid userId);

        /// <summary>
        /// Updates the profile information for an existing user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user to update.</param>
        /// <param name="profileDto">The updated profile data.</param>
        Task UpdateUserProfileAsync(Guid userId, UserProfileDto profileDto);

        /// <summary>
        /// Updates the user's password after verifying the current one.
        /// </summary>
        /// <param name="userId">The unique identifier of the user changing the password.</param>
        /// <param name="passwordDto">The data containing current and new passwords.</param>
        Task ChangePasswordAsync(Guid userId, ChangePasswordDto passwordDto);

        /// <summary>
        /// Requests a verification code to initiate a secure password change process.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        Task RequestPasswordChangeCodeAsync(Guid userId);

        /// <summary>
        /// Verifies the password change token received by the user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="verifyDto">The data containing the verification code.</param>
        /// <returns>A secure session token if verification passes.</returns>
        Task<string> VerifyPasswordChangeCodeAsync(Guid userId, VerifyPasswordChangeCodeDto verifyDto);

        /// <summary>
        /// Confirms the password change using the generated session token.
        /// </summary>
        /// <param name="confirmDto">The payload containing the new password and verification token.</param>
        /// <param name="userId">The unique identifier of the user.</param>
        Task ConfirmPasswordChangeAsync(Guid userId, ConfirmPasswordChangeDto confirmDto);

        /// <summary>
        /// Uploads a new profile picture to AWS S3 and updates the user's avatar URL record.
        /// </summary>
        /// <param name="file">The image file payload.</param>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>The public URL of the newly uploaded profile picture.</returns>
        Task<string> UpdateAvatarAsync(Guid userId, IFormFile file);

        /// <summary>
        /// Creates a block relationship between two users.
        /// </summary>
        /// <param name="blockerUserId">The user initiating the block action.</param>
        /// <param name="blockedUserId">The user being blocked.</param>
        /// <param name="dto">Optional metadata describing the block reason.</param>
        Task BlockUserAsync(Guid blockerUserId, Guid blockedUserId, BlockUserDto dto);
    }

    /// <summary>
    /// Service implementation for managing user profiles within the Spectrum platform.
    /// </summary>
    public class ProfileService : IProfileService
    {
        private readonly IUserRepository _userRepository;
        private readonly SpectrumDbContext _context;
        private readonly IImageStorageService _imageStorageService;
        private readonly IVerificationCodeService _verificationCodeService;
        private readonly IEmailService _emailService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfileService"/> class.
        /// </summary>
        /// <param name="userRepository">The repository used to access user data.</param>
        /// <param name="context">The application database context used for profile relationship writes.</param>
        /// <param name="imageStorageService">The service used for direct image uploads to AWS S3.</param>
        /// <param name="verificationCodeService">The service responsible for one-time verification codes.</param>
        /// <param name="emailService">The service responsible for transactional email delivery.</param>
        public ProfileService(
            IUserRepository userRepository,
            SpectrumDbContext context,
            IImageStorageService imageStorageService,
            IVerificationCodeService verificationCodeService,
            IEmailService emailService)
        {
            _userRepository = userRepository;
            _context = context;
            _imageStorageService = imageStorageService;
            _verificationCodeService = verificationCodeService;
            _emailService = emailService;
        }

        /// <inheritdoc />
        public async Task<UserProfileDto> GetUserProfileAsync(Guid userId)
        {
            var user = await _userRepository.GetUserWithProfileDataAsync(userId);

            if (user == null)
            {
                throw new SpectrumNotFoundException("The requested user profile was not found.");
            }

            return new UserProfileDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                ProfilePicture = user.ProfilePicture,
                Biography = user.Biography,
                InterestedGames = user.InterestedGames.Select(g => new ProfileGameDto
                {
                    Id = g.Id.ToString(),
                    Name = g.Title,
                    ImageUrl = g.CoverImageUrl
                }).ToList(),
                Platforms = user.Platforms.Select(p => new ProfilePlatformDto
                {
                    Id = p.Id,
                    Name = p.Name
                }).ToList()
            };
        }

        /// <inheritdoc />
        public async Task<UserProfileDto> GetPublicUserProfileAsync(Guid userId)
        {
            var profile = await GetUserProfileAsync(userId);
            profile.Email = string.Empty;
            return profile;
        }

        /// <inheritdoc />
        public async Task UpdateUserProfileAsync(Guid userId, UserProfileDto profileDto)
        {
            var user = await _userRepository.GetUserWithProfileDataAsync(userId);

            if (user == null)
            {
                throw new SpectrumNotFoundException("User not found.");
            }

            var normalizedUsername = profileDto.Username.Trim();
            if (normalizedUsername.Length is < 3 or > 50)
            {
                throw new SpectrumBusinessException("El nombre de usuario debe tener entre 3 y 50 caracteres.");
            }

            var usernameExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(existingUser =>
                    existingUser.Id != userId &&
                    existingUser.Username.Equals(normalizedUsername, StringComparison.OrdinalIgnoreCase));

            if (usernameExists)
            {
                throw new SpectrumBusinessException("El nombre de usuario ya está en uso.");
            }

            user.Username = normalizedUsername;
            user.Biography = profileDto.Biography;
            user.ProfilePicture = profileDto.ProfilePicture;

            var incomingGameIds = profileDto.InterestedGames
                .Select(g => g.Id)
                .Select(id =>
                {
                    if (Guid.TryParse(id, out var guid))
                        return guid;

                    if (int.TryParse(id, out var rawgId))
                        return GameMappingUtilities.GenerateDeterministicGuid(rawgId);

                    return Guid.Empty;
                })
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            var incomingPlatformIds = profileDto.Platforms.
                Select(p => p.Id)
                .Distinct()
                .ToList();

            await _userRepository.UpdateUserProfileCollectionsAsync(user, incomingGameIds, incomingPlatformIds);
        }

        /// <inheritdoc />
        public async Task ChangePasswordAsync(Guid userId, ChangePasswordDto passwordDto)
        {
            var user = await GetExistingUserAsync(userId);

            bool isCurrentPasswordValid = BCrypt.Net.BCrypt.Verify(passwordDto.CurrentPassword, user.PasswordHash);

            if (!isCurrentPasswordValid)
            {
                throw new SpectrumUnauthorizedException("The current password provided is incorrect.");
            }
            
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(passwordDto.NewPassword);

            await _userRepository.UpdateUserAsync(user);
        }

        /// <inheritdoc />
        public async Task RequestPasswordChangeCodeAsync(Guid userId)
        {
            var user = await GetExistingUserAsync(userId);
            var code = await _verificationCodeService.CreateCodeAsync(
                VerificationPurpose.PasswordChange,
                user.Email,
                user.Id
            );

            await _emailService.SendPasswordChangeAsync(user.Email, code);
        }

        /// <inheritdoc />
        public async Task<string> VerifyPasswordChangeCodeAsync(Guid userId, VerifyPasswordChangeCodeDto verifyDto)
        {
            var user = await GetExistingUserAsync(userId);
            return await _verificationCodeService.VerifyCodeAndCreateSessionAsync(
                VerificationPurpose.PasswordChange,
                user.Email,
                verifyDto.Code,
                user.Id
            );
        }

        /// <inheritdoc />
        public async Task ConfirmPasswordChangeAsync(Guid userId, ConfirmPasswordChangeDto confirmDto)
        {
            var user = await GetExistingUserAsync(userId);
            await _verificationCodeService.ConsumeSessionAsync(
                VerificationPurpose.PasswordChange,
                user.Email,
                confirmDto.VerificationToken,
                user.Id
            );

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(confirmDto.NewPassword);
            await _userRepository.UpdateUserAsync(user);
        }

        /// <inheritdoc />
        public async Task<string> UpdateAvatarAsync(Guid userId, IFormFile file)
        {
            var user = await GetExistingUserAsync(userId);

            string avatarUrl = await _imageStorageService.UploadImageAsync(file, "photoProfiles");

            user.ProfilePicture = avatarUrl;

            await _userRepository.UpdateUserAsync(user);

            return avatarUrl;
        }

        /// <inheritdoc />
        public async Task BlockUserAsync(Guid blockerUserId, Guid blockedUserId, BlockUserDto dto)
        {
            if (blockerUserId == blockedUserId)
            {
                throw new SpectrumBusinessException("cannotBlockSelf");
            }

            var blockedUser = await _userRepository.GetUserByIdAsync(blockedUserId);
            if (blockedUser is null)
            {
                throw new SpectrumNotFoundException(Constants.ErrorMessages.UserNotFound);
            }

            var alreadyBlocked = await _context.UserBlocks
                .AnyAsync(block => block.BlockerUserId == blockerUserId && block.BlockedUserId == blockedUserId);

            if (alreadyBlocked)
            {
                return;
            }

            var reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();
            if (reason?.Length > 200)
            {
                throw new SpectrumBusinessException("blockReasonTooLong");
            }

            await _context.UserBlocks.AddAsync(new UserBlock
            {
                BlockerUserId = blockerUserId,
                BlockedUserId = blockedUserId,
                Reason = reason
            });
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Retrieves an existing user entity by identifier or throws a not-found exception.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>The matching <see cref="User"/> entity.</returns>
        /// <exception cref="SpectrumNotFoundException">
        /// Thrown when the specified user cannot be found.
        /// </exception>
        private async Task<User> GetExistingUserAsync(Guid userId)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
            {
                throw new SpectrumNotFoundException(Constants.ErrorMessages.UserNotFound);
            }

            return user;
        }
    }
}
