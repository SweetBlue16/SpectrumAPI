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
    public static class AuthUtilities
    {
        public static string GenerateJwtToken(User user, IConfiguration configuration)
        {
            var secretKey = configuration["JwtSettings:Secret"];
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

        public static async Task ValidateRegisterInput(RegisterDto registerDto, IUserRepository userRepository)
        {
            if (await userRepository.EmailExistsAsync(registerDto.Email))
            {
                throw new SpectrumBusinessException("emailAlreadyRegistered");
            }

            if (await userRepository.UsernameExistsAsync(registerDto.Username))
            {
                throw new SpectrumBusinessException("usernameAlreadyTaken");
            }
        }

        public static void ValidateLoginInput(User user, LoginDto loginDto)
        {
            if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
            {
                throw new SpectrumUnauthorizedException("invalidCredentials");
            }

            if (user.IsSuspended)
            {
                throw new SpectrumUnauthorizedException("accountSuspended");
            }
        }
    }
}
