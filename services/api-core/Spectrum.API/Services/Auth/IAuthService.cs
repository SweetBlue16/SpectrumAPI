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

    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IConfiguration _configuration;

        public AuthService(IUserRepository userRepository, IConfiguration configuration)
        {
            _userRepository = userRepository;
            _configuration = configuration;
        }

        public async Task<AuthResponseDto> GoogleLoginAsync(GoogleAuthDto googleAuthDto)
        {
            Payload payload;
            try
            {
                var settings = new ValidationSettings
                {
                    Audience = new[] { _configuration["Google:ClientId"] }
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

        public async Task<AuthResponseDto> RegisterAdminAsync(RegisterAdminDto registerAdminDto)
        {
            var masterKey = _configuration["Admin:MasterKey"];
            if (registerAdminDto.AdminSecretKey != masterKey)
            {
                throw new SpectrumUnauthorizedException("invalidAdminKey");
            }

            await AuthUtilities.ValidateRegisterInput(registerAdminDto, _userRepository);

            var user = new User
            {
                Username = registerAdminDto.Username,
                Email = registerAdminDto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerAdminDto.Password),
                CreatedAt = DateTime.UtcNow,
                Role = UserRole.Admin
            };
            await _userRepository.AddUserAsync(user);

            return new AuthResponseDto
            {
                Token = AuthUtilities.GenerateJwtToken(user, _configuration),
                Username = user.Username,
                Email = user.Email
            };
        }

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
