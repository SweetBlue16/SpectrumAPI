using Microsoft.IdentityModel.Tokens;
using Spectrum.API.Dtos.Auth;
using Spectrum.API.Exceptions;
using Spectrum.API.Models;
using Spectrum.API.Repositories;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Spectrum.API.Utilities
{
    /// <summary>
    /// Static utility class that provides helper methods for authentication, 
    /// input validation, and security token generation.
    /// </summary>
    public static class AuthUtilities
    {
        /// <summary>
        /// Generates a signed JSON Web Token (JWT) for a specific user.
        /// </summary>
        /// <param name="user">The user entity containing identity and role data.</param>
        /// <param name="configuration">Application configuration to retrieve secrets and settings.</param>
        /// <returns>A string representing the encoded JWT.</returns>
        /// <remarks>
        /// The token includes claims for ID, Email, Username, and Role to support RBAC.
        /// </remarks>
        public static string GenerateJwtToken(User user, IConfiguration configuration)
        {
            var secretKey = configuration["JwtSettings:Secret"]
                ?? throw new InvalidOperationException("JwtSettings:Secret is not configured.");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var token = new JwtSecurityToken(
                issuer: configuration["JwtSettings:Issuer"],
                audience: configuration["JwtSettings:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Validates if the registration data conflicts with existing users in the repository.
        /// </summary>
        /// <param name="registerDto">The registration data to validate.</param>
        /// <param name="userRepository">The repository instance for data access.</param>
        /// <returns>A task representing the asynchronous validation operation.</returns>
        /// <exception cref="SpectrumBusinessException">Thrown if email or username is already taken.</exception>
        public static async Task ValidateRegisterInput(RegisterDto registerDto, IUserRepository userRepository)
        {
            if (await userRepository.EmailExistsAsync(registerDto.Email))
            {
                throw new SpectrumBusinessException(Constants.ErrorMessages.EmailAlreadyRegistered);
            }

            if (await userRepository.UsernameExistsAsync(registerDto.Username))
            {
                throw new SpectrumBusinessException(Constants.ErrorMessages.UsernameAlreadyTaken);
            }
        }

        /// <summary>
        /// Validates login credentials and checks for account status.
        /// </summary>
        /// <param name="user">The user found in the database (may be null).</param>
        /// <param name="loginDto">The login attempt data containing the plain-text password.</param>
        /// <exception cref="SpectrumUnauthorizedException">Thrown if credentials match fails.</exception>
        /// <exception cref="SpectrumBusinessException">Thrown if the account is currently suspended.</exception>
        public static async Task ValidateLoginInput(User? user, LoginDto loginDto)
        {
            if (user == null)
            {
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.InvalidCredentials);
            }

            if (!string.IsNullOrWhiteSpace(user.PasswordHash) &&
                !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
            {
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.InvalidCredentials);
            }

            if (user.IsDeleted)
            {
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.AccountDeleted);
            }

            if (user.IsBanned)
            {
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.AccountBanned);
            }

            if (user.IsSuspended)
            {
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.AccountSuspended);
            }

            if (!user.IsEmailVerified)
            {
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.AccountNotVerified);
            }
        }

        /// <summary>
        /// Validates the input data for registering a new administrator account, 
        /// checking the provided secret key against the system's master key and 
        /// ensuring all required personal details are present.
        /// </summary>
        /// <param name="registerAdminDto">The data transfer object containing the administrator's registration details.</param>
        /// <param name="adminDetailRepository">The repository instance for administrator details data access.</param>
        /// <param name="masterKey">The system's master secret key required to authorize the creation of an administrator account.</param>
        /// <returns>A task representing the asynchronous validation operation.</returns>
        /// <exception cref="SpectrumUnauthorizedException">Thrown if the provided admin secret key does not match the system's master key.</exception>
        /// <exception cref="SpectrumBusinessException">Thrown if any required personal detail (first name, last name, phone number, address, RFC) is missing or whitespace.</exception>
        public static async Task ValidateRegisterAdminInput(RegisterAdminDto registerAdminDto, IAdminDetailRepository adminDetailRepository, string? masterKey)
        {
            if (string.IsNullOrWhiteSpace(masterKey))
            {
                throw new InvalidOperationException("Admin:MasterKey is not configured.");
            }

            if (registerAdminDto.AdminSecretKey != masterKey)
            {
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.InvalidAdminKey);
            }

            if (string.IsNullOrWhiteSpace(registerAdminDto.FirstName))
            {
                throw new SpectrumBusinessException(Constants.ErrorMessages.MissingRequiredParameter);
            }

            if (string.IsNullOrWhiteSpace(registerAdminDto.LastName))
            {
                throw new SpectrumBusinessException(Constants.ErrorMessages.MissingRequiredParameter);
            }

            if (string.IsNullOrWhiteSpace(registerAdminDto.PhoneNumber))
            {
                throw new SpectrumBusinessException(Constants.ErrorMessages.MissingRequiredParameter);
            }

            if (string.IsNullOrWhiteSpace(registerAdminDto.Address))
            {
                throw new SpectrumBusinessException(Constants.ErrorMessages.MissingRequiredParameter);
            }

            if (string.IsNullOrWhiteSpace(registerAdminDto.Rfc))
            {
                throw new SpectrumBusinessException(Constants.ErrorMessages.MissingRequiredParameter);
            }

            if (await adminDetailRepository.RfcExistsAsync(registerAdminDto.Rfc))
            {
                throw new SpectrumBusinessException("rfcAlreadyRegistered");
            }
        }
    }
}
