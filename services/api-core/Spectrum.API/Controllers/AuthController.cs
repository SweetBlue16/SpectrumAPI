using Microsoft.AspNetCore.Mvc;
using Spectrum.API.Dtos.Auth;
using Microsoft.AspNetCore.RateLimiting;
using Spectrum.API.Services.Auth;

namespace Spectrum.API.Controllers
{
    /// <summary>
    /// Serves as the primary entry point for identity management, authentication, and token issuance.
    /// All endpoints in this controller delegate business logic to the Auth Service and rely on the 
    /// GlobalExceptionHandler to map domain exceptions to RFC 7807 Problem Details.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [EnableRateLimiting("SensitiveAuth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthController"/> class.
        /// </summary>
        /// <param name="authService">The authentication service handling the core identity business logic.</param>
        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Provisions a new standard user account (Reviewer) in the local database.
        /// </summary>
        /// <param name="registerDto">The data transfer object containing the desired username, email, and password.</param>
        /// <returns>A response indicating that email verification is required.</returns>
        /// <response code="201">Successfully created the user and sent a verification code.</response>
        /// <response code="400">The payload failed validation or the email/username is already in use.</response>
        [HttpPost("register")]
        [ProducesResponseType(typeof(RegisterResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            var response = await _authService.RegisterAsync(registerDto);
            return CreatedAtAction(nameof(Login), new { email = response.Email }, response);
        }

        /// <summary>
        /// Verifies the registration code previously sent to the user's email address.
        /// Upon successful verification, activates the account and issues an authentication token.
        /// </summary>
        /// <param name="verifyDto">
        /// The email address and verification code provided by the user.
        /// </param>
        /// <returns>
        /// The authenticated user's identity information and an active JWT.
        /// </returns>
        /// <response code="200">
        /// The verification code is valid and the account has been successfully activated.
        /// </response>
        /// <response code="400">
        /// The request payload failed validation.
        /// </response>
        /// <response code="401">
        /// The verification code is invalid, expired, or does not match the specified email address.
        /// </response>
        [HttpPost("register/verify")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> VerifyRegistration([FromBody] VerifyRegistrationCodeDto verifyDto)
        {
            var response = await _authService.VerifyRegistrationAsync(verifyDto);
            return Ok(response);
        }

        /// <summary>
        /// Generates and sends a new registration verification code to a pending user account.
        /// </summary>
        /// <param name="resendDto">
        /// The email address associated with the account awaiting verification.
        /// </param>
        /// <returns>
        /// A confirmation message indicating that a new verification code has been issued.
        /// </returns>
        /// <response code="200">
        /// A new verification code has been successfully generated and sent.
        /// </response>
        /// <response code="400">
        /// The request payload failed validation.
        /// </response>
        [HttpPost("register/resend-code")]
        [ProducesResponseType(typeof(MessageResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResendRegistrationCode([FromBody] ResendRegistrationCodeDto resendDto)
        {
            var response = await _authService.ResendRegistrationCodeAsync(resendDto);
            return Ok(response);
        }

        /// <summary>
        /// Authenticates local user credentials and issues a stateless JSON Web Token (JWT) for subsequent authorized requests.
        /// </summary>
        /// <param name="loginDto">The data transfer object containing the user's email and plain-text password.</param>
        /// <returns>The user's identity details and an active JSON Web Token.</returns>
        /// <response code="200">Successfully authenticated and issued a token.</response>
        /// <response code="401">Authentication failed due to invalid credentials, non-existent user, or suspended account.</response>
        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            var response = await _authService.LoginAsync(loginDto);
            return Ok(response);
        }

        /// <summary>
        /// Performs an OAuth2 token exchange, validating a Google-issued identity token and provisioning 
        /// a local Spectrum session for the user.
        /// </summary>
        /// <param name="googleAuthDto">The object containing the JWT credential issued by the Google Client SDK.</param>
        /// <returns>A local Spectrum API JWT exchanged for the verified Google identity.</returns>
        /// <response code="200">Successfully authenticated with Google and issued a local token.</response>
        /// <response code="401">The Google token is invalid, expired, or the associated local account is suspended.</response>
        [HttpPost("google")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleAuthDto googleAuthDto)
        {
            var response = await _authService.GoogleLoginAsync(googleAuthDto);
            return Ok(response);
        }

        /// <summary>
        /// Initiates the password recovery workflow by generating and sending a verification code.
        /// </summary>
        /// <param name="forgotPasswordDto">
        /// The email address associated with the account requesting password recovery.
        /// </param>
        /// <returns>
        /// A confirmation message indicating that the recovery process has been initiated.
        /// </returns>
        /// <response code="200">
        /// The password recovery request has been processed successfully.
        /// </response>
        /// <response code="400">
        /// The request payload failed validation.
        /// </response>
        [HttpPost("password/forgot")]
        [ProducesResponseType(typeof(MessageResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
        {
            var response = await _authService.ForgotPasswordAsync(forgotPasswordDto);
            return Ok(response);
        }

        /// <summary>
        /// Validates a password recovery verification code and issues a temporary reset token.
        /// </summary>
        /// <param name="verifyDto">
        /// The email address and verification code provided by the user.
        /// </param>
        /// <returns>
        /// A temporary verification token required to complete the password reset process.
        /// </returns>
        /// <response code="200">
        /// The verification code is valid and a reset token has been issued.
        /// </response>
        /// <response code="400">
        /// The request payload failed validation.
        /// </response>
        /// <response code="401">
        /// The verification code is invalid, expired, or does not match the specified email address.
        /// </response>
        [HttpPost("password/verify-code")]
        [ProducesResponseType(typeof(PasswordCodeVerifiedDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> VerifyPasswordResetCode([FromBody] VerifyPasswordCodeDto verifyDto)
        {
            var response = await _authService.VerifyPasswordResetCodeAsync(verifyDto);
            return Ok(response);
        }

        /// <summary>
        /// Completes the password reset workflow using a previously issued verification token.
        /// </summary>
        /// <param name="resetPasswordDto">
        /// The email address, verification token, and new password provided by the user.
        /// </param>
        /// <returns>
        /// A confirmation message indicating that the password has been successfully updated.
        /// </returns>
        /// <response code="200">
        /// The password has been successfully reset.
        /// </response>
        /// <response code="400">
        /// The request payload failed validation.
        /// </response>
        /// <response code="401">
        /// The verification token is invalid, expired, or does not match the specified email address.
        /// </response>
        [HttpPost("password/reset")]
        [ProducesResponseType(typeof(MessageResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
        {
            var response = await _authService.ResetPasswordAsync(resetPasswordDto);
            return Ok(response);
        }

        /// <summary>
        /// Provisions a high-privileged administrative account. This endpoint is protected by a 
        /// system-level master key to prevent unauthorized privilege escalation.
        /// </summary>
        /// <param name="registerAdminDto">The data transfer object including full legal admin details and the secret master key.</param>
        /// <returns>The newly created administrator's identity details and an active JSON Web Token.</returns>
        /// <response code="201">Successfully created the administrator and issued a token.</response>
        /// <response code="400">The payload failed validation or mandatory PII fields are missing.</response>
        /// <response code="401">The provided master key is incorrect or missing.</response>
        [HttpPost("register-admin")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RegisterAdmin([FromBody] RegisterAdminDto registerAdminDto)
        {
            var response = await _authService.RegisterAdminAsync(registerAdminDto);
            return CreatedAtAction(nameof(Login), new { id = response.Token.ToString() }, response);
        }
    }
}
