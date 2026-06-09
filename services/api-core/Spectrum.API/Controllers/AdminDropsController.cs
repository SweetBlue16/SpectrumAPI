using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spectrum.API.Dtos.Drops;
using Spectrum.API.Exceptions;
using Spectrum.API.Services.Drops;
using Spectrum.API.Utilities;
using System.Security.Claims;

namespace Spectrum.API.Controllers
{
    /// <summary>
    /// Provides administrative endpoints for managing drop events,
    /// including creation, modification, retrieval, and listing operations.
    /// Access is restricted to users with the Administrator role.
    /// </summary>
    [ApiController]
    [Route("api/admin/drops")]
    [Authorize(Roles = Constants.Roles.Admin)]
    public class AdminDropsController : ControllerBase
    {
        private readonly IDropsService _dropService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminDropsController"/> class.
        /// </summary>
        /// <param name="dropService">
        /// Service responsible for managing drop event lifecycle operations.
        /// </param>
        public AdminDropsController(IDropsService dropService)
        {
            _dropService = dropService;
        }

        /// <summary>
        /// Retrieves a paginated list of drop events for administrative management.
        /// </summary>
        /// <param name="scope">
        /// Filter applied to the result set (for example: ALL, CURRENT, UPCOMING, or FINISHED).
        /// </param>
        /// <param name="page">
        /// Page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Number of items to include per page.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <returns>
        /// A paginated collection of drop events.
        /// </returns>
        /// <response code="200">
        /// The drop events were successfully retrieved.
        /// </response>
        /// <response code="401">
        /// Authentication is required.
        /// </response>
        /// <response code="403">
        /// The authenticated user does not have administrator privileges.
        /// </response>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<EventStatusDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> List(
            [FromQuery] string scope = "ALL",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken cancellationToken = default
        )
        {
            var events = await _dropService.ListEventsAsync(
                scope,
                page,
                pageSize,
                includeDrafts: true,
                cancellationToken
            );
            return Ok(events);
        }

        /// <summary>
        /// Retrieves detailed information about a specific drop event.
        /// </summary>
        /// <param name="id">
        /// Unique identifier of the drop event.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <returns>
        /// Detailed status information for the requested event.
        /// </returns>
        /// <response code="200">
        /// The event was successfully retrieved.
        /// </response>
        /// <response code="404">
        /// The specified event does not exist.
        /// </response>
        /// <response code="401">
        /// Authentication is required.
        /// </response>
        /// <response code="403">
        /// The authenticated user does not have administrator privileges.
        /// </response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(EventStatusDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
        {
            var status = await _dropService.GetEventStatusAsync(id, exposeChallengeCode: true, cancellationToken);
            return Ok(status);
        }

        /// <summary>
        /// Creates a new drop event.
        /// </summary>
        /// <param name="dto">
        /// Configuration data for the event to be created.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <returns>
        /// Information about the newly created drop event.
        /// </returns>
        /// <response code="201">
        /// The drop event was successfully created.
        /// </response>
        /// <response code="400">
        /// The request payload failed validation.
        /// </response>
        /// <response code="401">
        /// Authentication is required.
        /// </response>
        /// <response code="403">
        /// The authenticated user does not have administrator privileges.
        /// </response>
        [HttpPost]
        [ProducesResponseType(typeof(DropActionResultDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Create([FromBody] CreateDropEventDto dto, CancellationToken cancellationToken)
        {
            var result = await _dropService.CreateEventAsync(dto, GetCurrentAdminId(), cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.EventId }, result);
        }

        /// <summary>
        /// Updates an existing drop event.
        /// </summary>
        /// <param name="id">
        /// Unique identifier of the event to update.
        /// </param>
        /// <param name="dto">
        /// Updated event configuration data.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <returns>
        /// Information about the updated event.
        /// </returns>
        /// <response code="200">
        /// The event was successfully updated.
        /// </response>
        /// <response code="400">
        /// The request payload failed validation.
        /// </response>
        /// <response code="404">
        /// The specified event does not exist.
        /// </response>
        /// <response code="401">
        /// Authentication is required.
        /// </response>
        /// <response code="403">
        /// The authenticated user does not have administrator privileges.
        /// </response>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(DropActionResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateDropEventDto dto, CancellationToken cancellationToken)
        {
            var result = await _dropService.UpdateEventAsync(id, dto, cancellationToken);
            return Ok(result);
        }

        private Guid GetCurrentAdminId()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.Unauthorized);
            }

            return userId;
        }
    }
}
