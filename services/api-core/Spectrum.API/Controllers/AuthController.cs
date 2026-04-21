using Microsoft.AspNetCore.Mvc;
using Spectrum.API.Dtos.Auth;
using Spectrum.API.Services.Auth;

namespace Spectrum.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            var response = await _authService.RegisterAsync(registerDto);
            return Ok(response);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            var response = await _authService.LoginAsync(loginDto);
            return Ok(response);
        }

        [HttpPost("google")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleAuthDto googleAuthDto)
        {
            var response = await _authService.GoogleLoginAsync(googleAuthDto);
            return Ok(response);
        }

        [HttpPost("register-admin")]
        public async Task<IActionResult> RegisterAdmin([FromBody] RegisterAdminDto registerAdminDto)
        {
            var response = await _authService.RegisterAdminAsync(registerAdminDto);
            return Ok(response);
        }
    }
}
