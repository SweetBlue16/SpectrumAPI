using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spectrum.API.Dtos.Auth;
using Spectrum.API.Services.Auth;
using Spectrum.API.Utilities;

namespace Spectrum.API.Controllers
{
    /// <summary>
    /// Provides administrative endpoints for managing administrator accounts.
    /// Access is restricted to users with the Administrator role.
    /// </summary>
    [ApiController]
    [Route("api/admin/admins")]
    [Authorize(Roles = Constants.Roles.Admin)]
    public class AdminAdminsController : ControllerBase
    {
        private readonly IAuthService _authService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminAdminsController"/> class.
        /// </summary>
        /// <param name="authService">
        /// Authentication service responsible for administrator account provisioning.
        /// </param>
        public AdminAdminsController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Creates a new administrator account.
        /// </summary>
        /// <param name="dto">
        /// Registration data required to create the administrator account.
        /// </param>
        /// <returns>
        /// The newly created administrator and an authentication token.
        /// </returns>
        /// <response code="201">
        /// The administrator account was successfully created.
        /// </response>
        /// <response code="400">
        /// The request payload failed validation or contains invalid data.
        /// </response>
        /// <response code="401">
        /// Authentication is required.
        /// </response>
        /// <response code="403">
        /// The authenticated user does not have sufficient privileges.
        /// </response>
        [HttpPost]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Create([FromBody] RegisterAdminDto dto)
        {
            var response = await _authService.RegisterAdminByAdminAsync(dto);
            return StatusCode(StatusCodes.Status201Created, response);
        }
    }
}
