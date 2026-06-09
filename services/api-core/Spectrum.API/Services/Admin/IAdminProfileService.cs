using Microsoft.EntityFrameworkCore;
using Spectrum.API.Data;
using Spectrum.API.Dtos.Admin;
using Spectrum.API.Exceptions;
using Spectrum.API.Utilities;

namespace Spectrum.API.Services.Admin
{
    /// <summary>
    /// Defines operations for retrieving and updating administrator profile information.
    /// </summary>
    public interface IAdminProfileService
    {
        /// <summary>
        /// Retrieves the profile associated with the specified administrator.
        /// </summary>
        /// <param name="adminId">
        /// Identifier of the administrator whose profile will be retrieved.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// The administrator profile information.
        /// </returns>
        /// <exception cref="SpectrumNotFoundException">
        /// Thrown when the administrator profile does not exist.
        /// </exception>
        Task<AdminProfileDto> GetProfileAsync(Guid adminId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the profile information of the specified administrator.
        /// </summary>
        /// <param name="adminId">
        /// Identifier of the administrator whose profile will be updated.
        /// </param>
        /// <param name="dto">
        /// Updated profile information.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// The updated administrator profile.
        /// </returns>
        /// <exception cref="SpectrumBusinessException">
        /// Thrown when the requested username is already in use.
        /// </exception>
        /// <exception cref="SpectrumNotFoundException">
        /// Thrown when the administrator profile does not exist.
        /// </exception>
        Task<AdminProfileDto> UpdateProfileAsync(Guid adminId, UpdateAdminProfileDto dto, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Provides business logic for administrator profile management.
    /// </summary>
    public class AdminProfileService : IAdminProfileService
    {
        private readonly SpectrumDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminProfileService"/> class.
        /// </summary>
        /// <param name="context">
        /// Database context used to access administrator profile data.
        /// </param>
        public AdminProfileService(SpectrumDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<AdminProfileDto> GetProfileAsync(Guid adminId, CancellationToken cancellationToken = default)
        {
            var user = await LoadAdminAsync(adminId, cancellationToken);
            return MapProfile(user);
        }

        /// <inheritdoc />
        public async Task<AdminProfileDto> UpdateProfileAsync(Guid adminId, UpdateAdminProfileDto dto, CancellationToken cancellationToken = default)
        {
            var user = await LoadAdminAsync(adminId, cancellationToken);
            var normalizedUsername = dto.Username.Trim();

            var usernameInUse = await _context.Users
                .AnyAsync(candidate => candidate.Id != adminId && candidate.Username == normalizedUsername, cancellationToken);
            if (usernameInUse)
            {
                throw new SpectrumBusinessException(Constants.ErrorMessages.UsernameAlreadyTaken);
            }

            user.Username = normalizedUsername;
            user.ProfilePicture = string.IsNullOrWhiteSpace(dto.ProfilePicture) ? user.ProfilePicture : dto.ProfilePicture.Trim();
            user.AdminDetail!.FirstName = dto.FirstName.Trim();
            user.AdminDetail.LastName = dto.LastName.Trim();
            user.AdminDetail.PhoneNumber = dto.PhoneNumber.Trim();
            user.AdminDetail.Address = dto.Address.Trim();

            await _context.SaveChangesAsync(cancellationToken);
            return MapProfile(user);
        }

        private async Task<Models.User> LoadAdminAsync(Guid adminId, CancellationToken cancellationToken)
        {
            var user = await _context.Users
                .Include(item => item.AdminDetail)
                .FirstOrDefaultAsync(item =>
                    item.Id == adminId &&
                    item.Role == Constants.Roles.Admin &&
                    !item.IsDeleted,
                    cancellationToken);

            if (user?.AdminDetail is null)
            {
                throw new SpectrumNotFoundException(Constants.ErrorMessages.ResourceNotFound);
            }

            return user;
        }

        private static AdminProfileDto MapProfile(Models.User user)
        {
            var detail = user.AdminDetail!;
            return new AdminProfileDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FirstName = detail.FirstName,
                LastName = detail.LastName,
                PhoneNumber = detail.PhoneNumber,
                Address = detail.Address,
                Rfc = detail.Rfc,
                ProfilePicture = user.ProfilePicture,
                Role = user.Role
            };
        }
    }
}
