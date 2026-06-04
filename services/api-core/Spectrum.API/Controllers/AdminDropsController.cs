using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spectrum.API.Dtos.Drops;
using Spectrum.API.Exceptions;
using Spectrum.API.Services.Drops;
using Spectrum.API.Utilities;
using System.Security.Claims;

namespace Spectrum.API.Controllers
{
    [ApiController]
    [Route("api/admin/drops")]
    [Authorize(Roles = Constants.Roles.Admin)]
    public class AdminDropsController : ControllerBase
    {
        private readonly IDropsService _dropService;

        public AdminDropsController(IDropsService dropService)
        {
            _dropService = dropService;
        }

        [HttpGet]
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
                exposeChallengeCode: true,
                cancellationToken
            );
            return Ok(events);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
        {
            var status = await _dropService.GetEventStatusAsync(id, exposeChallengeCode: true, cancellationToken);
            return Ok(status);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDropEventDto dto, CancellationToken cancellationToken)
        {
            var result = await _dropService.CreateEventAsync(dto, GetCurrentAdminId(), cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.EventId }, result);
        }

        [HttpPut("{id}")]
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
