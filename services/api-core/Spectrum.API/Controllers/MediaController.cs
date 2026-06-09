using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spectrum.API.Dtos.Media;
using Spectrum.API.Services.Storage;
using Spectrum.API.Services.Clips;
using Spectrum.API.Dtos.Votes;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace Spectrum.API.Controllers
{
    /// <summary>
    /// Entry point for managing media file uploads.
    /// Handles HTTP routing and status codes, delegating all validation and storage logic to specialized services.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize]
    public class MediaController : ControllerBase
    {
        private readonly IImageStorageService imageStorageService;
        private readonly IVideoStorageService videoStorageService;
        private readonly IGameClipService gameClipService;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaController"/> class.
        /// </summary>
        /// <param name="imageStorageService">The service handling single-part image uploads.</param>
        /// <param name="videoStorageService">The service handling chunked video uploads.</param>
        /// <param name="gameClipService">The service handling clip business logic and PostgreSQL persistence.</param>
        public MediaController(IImageStorageService imageStorageService, IVideoStorageService videoStorageService, IGameClipService gameClipService)
        {
            this.imageStorageService = imageStorageService;
            this.videoStorageService = videoStorageService;
            this.gameClipService = gameClipService;
        }

        /// <summary>
        /// Uploads an image file (JPG/PNG) directly to the specified storage folder.
        /// </summary>
        /// <param name="file">The image file payload.</param>
        /// <param name="folder">The target folder name (defaults to "imagesReviews").</param>
        /// <returns>The public access URL for the uploaded image.</returns>
        /// <response code="200">The image was successfully validated and uploaded.</response>
        /// <response code="400">The image failed size or format validations.</response>
        [HttpPost("upload-image")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadImage(IFormFile file, [FromQuery] string folder = "imagesReviews")
        {
            var url = await imageStorageService.UploadImageAsync(file, folder);
            return Ok(new { url });
        }

        /// <summary>
        /// Uploads an attachment for a review, automatically determining whether it is an image or video.
        /// Images are stored directly, while videos undergo validation and storage through the video pipeline.
        /// </summary>
        /// <param name="file">The media file to upload.</param>
        /// <returns>
        /// An object containing the public URL of the uploaded file and its detected media type.
        /// </returns>
        /// <response code="200">The attachment was successfully uploaded.</response>
        /// <response code="400">The file format, size, or content type is invalid.</response>
        [HttpPost("reviews/attachment")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadReviewAttachment(IFormFile file)
        {
            if (file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                var imageUrl = await imageStorageService.UploadImageAsync(file, "review-attachments", maxSizeMb: 5);
                return Ok(new { url = imageUrl, mediaType = "image" });
            }

            var videoUrl = await videoStorageService.UploadReviewVideoAsync(file, "review-attachments");
            return Ok(new { url = videoUrl, mediaType = "video" });
        }

        /// <summary>
        /// Validates a video file and initiates an AWS S3 multipart upload session.
        /// </summary>
        /// <param name="file">The full video file used for preliminary metadata validation.</param>
        /// <returns>The initialization details including the active UploadId and KeyName.</returns>
        /// <response code="200">The video metadata is valid and the session has started.</response>
        /// <response code="400">The video exceeds duration or size limits, or has an invalid format.</response>
        [HttpPost("clips/start")]
        [ProducesResponseType(typeof(MultipartInitResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> StartVideoUpload(IFormFile file)
        {
            // We pass the file to validate duration and size before talking to AWS S3
            var response = await videoStorageService.StartVideoUploadAsync(file, "clips");
            return Ok(response);
        }

        /// <summary>
        /// Uploads a single 5MB byte chunk to an active multipart upload session.
        /// </summary>
        /// <param name="file">The individual binary chunk payload.</param>
        /// <param name="uploadId">The active session identifier.</param>
        /// <param name="partNumber">The sequential position of the chunk (1-indexed).</param>
        /// <param name="keyName">The exact path of the object in S3.</param>
        /// <returns>The unique ETag generated by the storage provider for this chunk.</returns>
        /// <response code="200">The chunk was successfully received and stored in S3.</response>
        /// <response code="400">The chunk size exceeds the strict streaming constraints.</response>
        [HttpPost("clips/upload-chunk")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadVideoChunk(
            IFormFile file,
            [FromQuery] string uploadId,
            [FromQuery] int partNumber,
            [FromQuery] string keyName)
        {
            var eTag = await videoStorageService.UploadVideoChunkAsync(file, uploadId, partNumber, keyName);
            return Ok(new { eTag });
        }

        /// <summary>
        /// Finalizes the multipart upload by ordering the storage provider to assemble all chunks and persists metadata.
        /// </summary>
        /// <param name="request">The complete assembly payload containing the part mapping and session IDs.</param>
        /// <returns>The final public access URL for the assembled video file.</returns>
        /// <response code="200">All chunks were successfully merged into a single file and recorded in the database.</response>
        /// <response code="401">The user is not authenticated or the token is invalid.</response>
        [HttpPost("clips/complete")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CompleteVideoUpload([FromBody] CompleteUploadRequestDto request)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var url = await videoStorageService.CompleteVideoUploadAsync(request);
            await gameClipService.CreateClipAsync(userId, request, url);

            return Ok(new { url });
        }

        /// <summary>
        /// Retrieves a summary list of all game clips uploaded by a specific user.
        /// </summary>
        /// <param name="userId">The unique identifier of the target user.</param>
        /// <returns>A collection of game clip summary objects matching the profile view constraints.</returns>
        /// <response code="200">The collection was successfully retrieved (can be empty).</response>
        [HttpGet("/api/clips/user/{userId}")] 
        [ProducesResponseType(typeof(IEnumerable<GameClipSummaryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUserClips(Guid userId)
        {
            var clips = await gameClipService.GetClipsByUserIdAsync(userId);
            return Ok(clips);
        }

        /// <summary>
        /// Deletes a specific game clip from the system database and removes its source file from AWS S3 storage.
        /// </summary>
        /// <param name="clipId">The unique identifier of the clip to be removed.</param>
        /// <returns>A 204 No Content response if the deletion is authorized and successful.</returns>
        /// <response code="204">The clip was successfully deleted from both storage and database layers.</response>
        /// <response code="401">The user is not authenticated or the token is invalid.</response>
        /// <response code="403">The requesting user is neither the owner of the clip nor an administrative user.</response>
        /// <response code="404">The specified game clip identifier does not exist in the system.</response>
        [HttpDelete("clips/{clipId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteClip(Guid clipId)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            await gameClipService.DeleteClipAsync(clipId, userId);

            return NoContent();
        }

        /// <summary>
        /// Casts a positive or negative vote on a game clip.
        /// </summary>
        /// <param name="clipId">The unique identifier of the clip being voted on.</param>
        /// <param name="request">The vote payload indicating whether the vote is positive or negative.</param>
        /// <returns>
        /// The updated voting result including current totals and user vote state.
        /// </returns>
        /// <response code="200">The vote was successfully registered.</response>
        /// <response code="401">The user is not authenticated.</response>
        /// <response code="403">The user is not allowed to vote on the specified clip.</response>
        /// <response code="404">The requested clip does not exist.</response>
        [HttpPost("clips/{clipId}/vote")]
        [ProducesResponseType(typeof(VoteResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> VoteClip(Guid clipId, [FromBody] CastReviewVoteDto request)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            return Ok(await gameClipService.CastClipVoteAsync(clipId, userId, request.IsPositive));
        }

        /// <summary>
        /// Attempts to extract the authenticated user's identifier from the JWT claims collection.
        /// </summary>
        /// <param name="userId">
        /// When this method returns, contains the parsed user identifier if extraction succeeded;
        /// otherwise, contains <see cref="Guid.Empty"/>.
        /// </param>
        /// <returns>
        /// <c>true</c> if a valid user identifier was found and parsed; otherwise, <c>false</c>.
        /// </returns>
        private bool TryGetUserId(out Guid userId)
        {
            var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                             ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(userIdClaim, out userId);
        }
    }
}
