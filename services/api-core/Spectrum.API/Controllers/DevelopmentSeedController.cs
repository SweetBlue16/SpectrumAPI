using Microsoft.AspNetCore.Mvc;
using Spectrum.API.Dtos.Seed;
using Spectrum.API.Services.Seeding;

namespace Spectrum.API.Controllers
{
    /// <summary>
    /// Provides development-only endpoints for seeding and cleaning demo data.
    /// </summary>
    /// <remarks>
    /// These endpoints are available exclusively in development environments
    /// and are intended to simplify testing, demonstrations, and local setup.
    /// </remarks>
    [ApiController]
    [Route("api/dev/seed")]
    public class DevelopmentSeedController : ControllerBase
    {
        private readonly IDemoSeedService _demoSeedService;
        private readonly IWebHostEnvironment _environment;

        /// <summary>
        /// Initializes a new instance of the <see cref="DevelopmentSeedController"/> class.
        /// </summary>
        /// <param name="demoSeedService">
        /// Service responsible for creating and removing demo data.
        /// </param>
        /// <param name="environment">
        /// Hosting environment used to verify development mode.
        /// </param>
        public DevelopmentSeedController(IDemoSeedService demoSeedService, IWebHostEnvironment environment)
        {
            _demoSeedService = demoSeedService;
            _environment = environment;
        }

        /// <summary>
        /// Creates a complete demo dataset for development and testing purposes.
        /// </summary>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <returns>
        /// A <see cref="DemoSeedResultDto"/> describing the generated demo data.
        /// </returns>
        /// <response code="200">
        /// Demo data was successfully created.
        /// </response>
        /// <response code="404">
        /// The endpoint is unavailable because the application is not running
        /// in a development environment.
        /// </response>
        [HttpPost("demo")]
        [ProducesResponseType(typeof(DemoSeedResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SeedDemo(CancellationToken cancellationToken)
        {
            if (!_environment.IsDevelopment())
            {
                return NotFound();
            }

            return Ok(await _demoSeedService.SeedAsync(cancellationToken));
        }

        /// <summary>
        /// Removes previously generated demo data from the system.
        /// </summary>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <returns>
        /// A <see cref="DemoSeedResultDto"/> describing the cleanup results.
        /// </returns>
        /// <response code="200">
        /// Demo data was successfully removed.
        /// </response>
        /// <response code="404">
        /// The endpoint is unavailable because the application is not running
        /// in a development environment.
        /// </response>
        [HttpDelete("demo")]
        [ProducesResponseType(typeof(DemoSeedResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CleanupDemo(CancellationToken cancellationToken)
        {
            if (!_environment.IsDevelopment())
            {
                return NotFound();
            }

            return Ok(await _demoSeedService.CleanupAsync(cancellationToken));
        }
    }
}
