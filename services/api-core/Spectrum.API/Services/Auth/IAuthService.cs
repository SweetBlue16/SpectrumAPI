using Spectrum.API.Dtos.Auth;
using Spectrum.API.Exceptions;
using Spectrum.API.Models;
using Spectrum.API.Repositories;
using Spectrum.API.Utilities;
using Google.Apis.Auth;
using static Google.Apis.Auth.GoogleJsonWebSignature;

namespace Spectrum.API.Services.Auth
{
    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterDto registerDto);
        Task<AuthResponseDto> RegisterAdminAsync(RegisterAdminDto registerAdminDto);
        Task<AuthResponseDto> LoginAsync(LoginDto loginDto);
        Task<AuthResponseDto> GoogleLoginAsync(GoogleAuthDto googleAuthDto);
    }

    /// <summary>
    /// Service responsible for handling authentication-related operations, 
    /// including user registration, login, and Google OAuth integration.
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IAdminDetailRepository _adminDetailRepository;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the AuthService class with the specified user repository 
        /// and configuration settings.
        /// </summary>
        /// <param name="userRepository">The user repository used to access and manage user data. Cannot be null.</param>
        /// <param name="adminDetailRepository">The admin detail repository used to access and manage admin details. Cannot be null.</param>
        /// <param name="configuration">The configuration settings used to retrieve authentication-related options. Cannot be null.</param>
        public AuthService(IUserRepository userRepository, IAdminDetailRepository adminDetailRepository, IConfiguration configuration)
        {
            _userRepository = userRepository;
            _configuration = configuration;
            _adminDetailRepository = adminDetailRepository;
        }

        /// <summary>
        /// Handles the Google login process by validating the provided Google OAuth token,
        /// creating or retrieving the corresponding user, and generating a JWT for authentication.
        /// </summary>
        /// <param name="googleAuthDto">The Google authentication data transfer object containing the OAuth token.</param>
        /// <returns>An AuthResponseDto containing the JWT, username, and email of the authenticated user.</returns>
        public async Task<AuthResponseDto> GoogleLoginAsync(GoogleAuthDto googleAuthDto)
        {
            Payload payload;
            try
            {
                var settings = new ValidationSettings
                {
                    Audience = new[] { _configuration["GoogleAuth:ClientId"] }
                };
                payload = await ValidateAsync(googleAuthDto.Credential, settings);
            }
            catch (InvalidJwtException)
            {
                throw new SpectrumUnauthorizedException("unauthorized");
            }

            var user = await CreateOrGetGoogleUserAsync(payload);
            return new AuthResponseDto
            {
                Token = AuthUtilities.GenerateJwtToken(user, _configuration),
                Username = user.Username,
                Email = user.Email
            };
        }

        /// <summary>
        /// Authenticates a user with the provided login credentials and generates a JWT token upon successful
        /// authentication.
        /// </summary>
        /// <remarks>Throws an exception if the login credentials are invalid or the user does not
        /// exist.</remarks>
        /// <param name="loginDto">An object containing the user's login credentials, including email and password. Cannot be null.</param>
        /// <returns>An AuthResponseDto containing the generated JWT token and user information if authentication is successful.</returns>
        public async Task<AuthResponseDto> LoginAsync(LoginDto loginDto)
        {
            var user = await _userRepository.GetUserByEmailAsync(loginDto.Email);
            AuthUtilities.ValidateLoginInput(user, loginDto);

            return new AuthResponseDto
            {
                Token = AuthUtilities.GenerateJwtToken(user, _configuration),
                Username = user.Username,
                Email = user.Email
            };
        }

        /// <summary>
        /// Registers a new administrator account using the provided registration details.
        /// </summary>
        /// <param name="registerAdminDto">The registration information for the new administrator, including username, email, password, and the
        /// required admin secret key. Cannot be null.</param>
        /// <returns>An AuthResponseDto containing the authentication token and account details for the newly registered
        /// administrator.</returns>
        /// <exception cref="SpectrumUnauthorizedException">Thrown if the provided admin secret key is invalid.</exception>
        public async Task<AuthResponseDto> RegisterAdminAsync(RegisterAdminDto registerAdminDto)
        {
            var masterKey = _configuration["Admin:MasterKey"];
            if (registerAdminDto.AdminSecretKey != masterKey)
            {
                throw new SpectrumUnauthorizedException("invalidAdminKey");
            }

            await AuthUtilities.ValidateRegisterInput(registerAdminDto, _userRepository);
            // TODO: Validar detalles del administrador

            var user = new User
            {
                Username = registerAdminDto.Username,
                Email = registerAdminDto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerAdminDto.Password),
                CreatedAt = DateTime.UtcNow,
                Role = UserRole.Admin
            };

            var adminDetail = new AdminDetail
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                FirstName = registerAdminDto.FirstName,
                LastName = registerAdminDto.LastName,
                PhoneNumber = registerAdminDto.PhoneNumber,
                Address = registerAdminDto.Address,
                Rfc = registerAdminDto.Rfc
            };

            await _userRepository.AddUserAsync(user);
            await _adminDetailRepository.AddAdminDetailAsync(adminDetail);

            return new AuthResponseDto
            {
                Token = AuthUtilities.GenerateJwtToken(user, _configuration),
                Username = user.Username,
                Email = user.Email
            };
        }

        /// <summary>
        /// Registers a new user account using the provided registration details.
        /// </summary>
        /// <param name="registerDto">The registration information for the new user, including username, email, and password. Cannot be null.</param>
        /// <returns>An AuthResponseDto containing the authentication token and account details for the newly registered user.</returns>
        public async Task<AuthResponseDto> RegisterAsync(RegisterDto registerDto)
        {
            await AuthUtilities.ValidateRegisterInput(registerDto, _userRepository);
            var user = new User
            {
                Username = registerDto.Username,
                Email = registerDto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
                CreatedAt = DateTime.UtcNow,
                Role = UserRole.Reviewer,
                IsSuspended = false,
                IsDeleted = false
            };

            await _userRepository.AddUserAsync(user);
            return new AuthResponseDto
            {
                Token = AuthUtilities.GenerateJwtToken(user, _configuration),
                Username = user.Username,
                Email = user.Email
            };
        }

        /// <summary>
        /// Creates a new user account based on Google authentication information if no existing user is found, or
        /// retrieves the existing user associated with the provided email address.
        /// </summary>
        /// <param name="payload">The Google authentication payload containing user information such as email and name. Cannot be null.</param>
        /// <returns>A user entity corresponding to the provided Google account. If the user does not exist, a new user is
        /// created and returned.</returns>
        /// <exception cref="SpectrumUnauthorizedException">Thrown if the existing user account associated with the provided email is suspended.</exception>
        private async Task<User> CreateOrGetGoogleUserAsync(Payload payload)
        {
            var user = await _userRepository.GetUserByEmailAsync(payload.Email);
            if (user == null)
            {
                user = new User
                {
                    Username = payload.Name.Replace(" ", "_").ToLower() + new Random().Next(100, 999),
                    Email = payload.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                    CreatedAt = DateTime.UtcNow,
                    IsSuspended = false,
                    IsDeleted = false
                };
                await _userRepository.AddUserAsync(user);
            }
            else
            {
                if (user.IsSuspended)
                {
                    throw new SpectrumUnauthorizedException("accountSuspended");
                }
            }
            return user;
        }
    }
}
