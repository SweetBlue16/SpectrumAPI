using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spectrum.API.Dtos.Search;
using Spectrum.API.Services.Search;

namespace Spectrum.API.Controllers
{
    /// <summary>
    /// Provides endpoints for performing global searches across multiple Spectrum resources.
    /// Aggregates results from users, games, reviews, and other searchable entities.
    /// </summary>
    [ApiController]
    [Route("api/search")]
    [Authorize]
    public class SearchController : ControllerBase
    {
        private readonly IGlobalSearchService _globalSearchService;

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchController"/> class.
        /// </summary>
        /// <param name="globalSearchService">
        /// The service responsible for executing aggregated search queries.
        /// </param>
        public SearchController(IGlobalSearchService globalSearchService)
        {
            _globalSearchService = globalSearchService;
        }

        /// <summary>
        /// Performs a global search across all supported searchable resources.
        /// </summary>
        /// <param name="q">
        /// The search text used to match users, games, reviews, and other indexed entities.
        /// </param>
        /// <param name="cancellationToken">
        /// A token used to cancel the search operation.
        /// </param>
        /// <returns>
        /// A consolidated set of search results grouped by resource type.
        /// </returns>
        /// <response code="200">
        /// The search operation completed successfully.
        /// </response>
        /// <response code="401">
        /// The user is not authenticated.
        /// </response>
        [HttpGet("global")]
        [ProducesResponseType(typeof(GlobalSearchResultDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Search([FromQuery] string q, CancellationToken cancellationToken)
        {
            return Ok(await _globalSearchService.SearchAsync(q, cancellationToken));
        }
    }
}
