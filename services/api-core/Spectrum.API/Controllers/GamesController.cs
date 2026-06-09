using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spectrum.API.Dtos.External;
using Spectrum.API.Dtos.Reviews;
using Spectrum.API.Models;
using Spectrum.API.Services.External;
using Spectrum.API.Utilities;
using System.Security.Claims;

namespace Spectrum.API.Controllers
{
    /// <summary>
    /// Acts as the gateway for interacting with the internal video game catalog.
    /// Provides capabilities to query, filter, and retrieve game metadata from the memory cache.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class GamesController : ControllerBase
    {
        private readonly IGameService _gameService;

        /// <summary>
        /// Initializes a new instance of the <see cref="GamesController"/> class.
        /// </summary>
        /// <param name="gameService">The service orchestrating catalog queries and data mapping.</param>
        public GamesController(IGameService gameService)
        {
            _gameService = gameService;
        }

        /// <summary>
        /// Retrieves a paginated and filtered catalog of video games from the internal memory cache.
        /// </summary>
        /// <param name="queryDto">The data transfer object containing search terms, genres, platforms, and pagination limits.</param>
        /// <returns>A collection of games matching the specified filter criteria.</returns>
        /// <response code="200">Successfully retrieved the filtered games catalog from cache.</response>
        /// <response code="401">The client lacks valid authentication credentials.</response>
        /// <response code="403">The authenticated user does not have the required role to access this resource.</response>
        [HttpGet("search")]
        [Authorize(Roles = $"{Constants.Roles.Reviewer},{Constants.Roles.Admin}")]
        [ProducesResponseType(typeof(PagedResult<Game>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Search([FromQuery] GameQueryDto queryDto)
        {
            var result = await _gameService.SearchGamesAsync(queryDto);

            return Ok(result);
        }

        /// <summary>
        /// Retrieves metadata for a specific video game from the internal catalog.
        /// </summary>
        /// <param name="id">The unique RAWG identifier of the game.</param>
        /// <returns>The metadata profile of the requested game.</returns>
        /// <response code="200">Successfully retrieved the game details.</response>
        /// <response code="401">The client lacks valid authentication credentials.</response>
        /// <response code="404">The requested game ID does not exist in the local catalog.</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(Game), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetDetails(int id)
        {
            var gameDetails = await _gameService.GetGameDetailsAsync(id);
            return Ok(gameDetails);
        }

        /// <summary>
        /// Retrieves the review summary for a specific game, including all associated reviews,
        /// aggregate rating statistics, and permission metadata for the authenticated user.
        /// </summary>
        /// <param name="id">The RAWG identifier of the game whose review details will be retrieved.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>
        /// A <see cref="GameReviewDetailDto"/> containing:
        /// <list type="bullet">
        /// <item><description>The game metadata.</description></item>
        /// <item><description>The list of associated reviews.</description></item>
        /// <item><description>The average rating calculated from available reviews.</description></item>
        /// <item><description>The total number of reviews.</description></item>
        /// <item><description>User-specific permissions such as ownership and deletion rights.</description></item>
        /// </list>
        /// </returns>
        /// <response code="200">The review details were retrieved successfully.</response>
        /// <response code="401">The request was made without valid authentication credentials.</response>
        /// <response code="404">The specified game does not exist in the catalog.</response>
        [HttpGet("{id}/reviews-detail")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetReviewDetail(int id, CancellationToken cancellationToken)
        {
            var detail = await _gameService.GetGameReviewDetailAsync(
                id,
                GetCurrentUserIdOrDefault(),
                User.IsInRole(Constants.Roles.Admin),
                cancellationToken
            );

            return Ok(detail);
        }

        /// <summary>
        /// Attempts to resolve the identifier of the currently authenticated user from the JWT claims.
        /// </summary>
        /// <remarks>
        /// The method checks multiple claim names commonly used by authentication providers:
        /// <c>NameIdentifier</c>, <c>sub</c>, and <c>userId</c>.
        /// If no valid GUID is found, <c>null</c> is returned.
        /// </remarks>
        /// <returns>
        /// The authenticated user's identifier when available; otherwise <c>null</c>.
        /// </returns>
        private Guid? GetCurrentUserIdOrDefault()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                              User.FindFirst("sub")?.Value ??
                              User.FindFirst("userId")?.Value;

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }
}
