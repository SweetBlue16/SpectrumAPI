using Microsoft.AspNetCore.Mvc;
using Spectrum.API.Grpc.Drops;
using Spectrum.API.Services.Drops;

namespace Spectrum.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DropsController : ControllerBase
    {
        private readonly IDropsService _dropsService;

        public DropsController(IDropsService dropsService)
        {
            _dropsService = dropsService;
        }

        [HttpPost("{eventId}/claim")]
        public async Task<IActionResult> ClaimKey(string eventId, [FromBody] string userId)
        {
            var request = new ClaimKeyRequest
            {
                EventId = eventId,
                UserId = userId
            };
            var response = await _dropsService.ClaimAccessKeyAsync(request);
            return Ok(response);
        }
    }
}
