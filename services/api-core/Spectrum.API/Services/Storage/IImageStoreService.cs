using Amazon.S3;
using Amazon.S3.Model;
using Spectrum.API.Utilities;

namespace Spectrum.API.Services.Storage
{
    /// <summary>
    /// Defines the contract for direct, single-part image uploads to AWS S3.
    /// Handles validation and storage for profile pictures and review images.
    /// </summary>
    public interface IImageStorageService
    {
        /// <summary>
        /// Validates and uploads an image file to the specified S3 folder.
        /// </summary>
        /// <param name="file">The image file (JPG/PNG).</param>
        /// <param name="folder">The target folder name (e.g., "photoProfiles").</param>
        /// <param name="maxSizeMb">Maximum allowed size in megabytes.</param>
        /// <returns>The public URL of the uploaded image.</returns>
        Task<string> UploadImageAsync(IFormFile file, string folder, int maxSizeMb = 6);
    }

    /// <summary>
    /// Implementation of <see cref="IImageStorageService"/> using AWS SDK.
    /// </summary>
    public class ImageStorageService : IImageStorageService
    {
        private readonly IAmazonS3 s3Client;
        private readonly string bucketName;
        private readonly string region;

        public ImageStorageService(IConfiguration config)
        {
            var credentials = new Amazon.Runtime.BasicAWSCredentials(
                config["AWS:AccessKey"] ?? throw new InvalidOperationException("AWS:AccessKey is not configured."),
                config["AWS:SecretKey"] ?? throw new InvalidOperationException("AWS:SecretKey is not configured.")
            );

            region = config["AWS:Region"] ?? throw new InvalidOperationException("AWS:Region is not configured.");
            var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);

            s3Client = new AmazonS3Client(credentials, regionEndpoint);
            bucketName = config["AWS:BucketName"] ?? throw new InvalidOperationException("AWS:BucketName is not configured.");
        }

        /// <inheritdoc />
        public async Task<string> UploadImageAsync(IFormFile file, string folder, int maxSizeMb = 6)
        {
            MediaValidationUtility.ValidateImage(file, maxSizeMb);

            string uniqueName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            string key = $"{folder}/{uniqueName}";

            using var stream = file.OpenReadStream();
            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = key,
                InputStream = stream,
            };

            await s3Client.PutObjectAsync(request);

            return $"https://{bucketName}.s3.{region}.amazonaws.com/{key}";
        }
    }
}
