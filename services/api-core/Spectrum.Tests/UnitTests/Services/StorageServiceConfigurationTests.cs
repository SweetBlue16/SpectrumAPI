using Microsoft.Extensions.Configuration;
using Spectrum.API.Services.Storage;

namespace Spectrum.Tests.UnitTests.Services
{
    public class StorageServiceConfigurationTests
    {
        [Theory]
        [InlineData("AWS:AccessKey", "AWS:AccessKey is not configured.")]
        [InlineData("AWS:SecretKey", "AWS:SecretKey is not configured.")]
        [InlineData("AWS:Region", "AWS:Region is not configured.")]
        [InlineData("AWS:BucketName", "AWS:BucketName is not configured.")]
        public void ImageStorageServiceWhenRequiredAwsConfigurationIsMissingShouldFailFast(string missingKey, string expectedMessage)
        {
            var configuration = BuildConfiguration(missingKey);

            var exception = Assert.Throws<InvalidOperationException>(() => new ImageStorageService(configuration));

            Assert.Equal(expectedMessage, exception.Message);
        }

        [Theory]
        [InlineData("AWS:AccessKey", "AWS:AccessKey is not configured.")]
        [InlineData("AWS:SecretKey", "AWS:SecretKey is not configured.")]
        [InlineData("AWS:Region", "AWS:Region is not configured.")]
        [InlineData("AWS:BucketName", "AWS:BucketName is not configured.")]
        public void VideoStorageServiceWhenRequiredAwsConfigurationIsMissingShouldFailFast(string missingKey, string expectedMessage)
        {
            var configuration = BuildConfiguration(missingKey);

            var exception = Assert.Throws<InvalidOperationException>(() => new VideoStorageService(configuration));

            Assert.Equal(expectedMessage, exception.Message);
        }

        [Fact]
        public void StorageServicesWhenAwsConfigurationIsCompleteShouldConstructWithoutNetworkCalls()
        {
            var configuration = BuildConfiguration(missingKey: null);

            var imageStorage = new ImageStorageService(configuration);
            var videoStorage = new VideoStorageService(configuration);

            Assert.NotNull(imageStorage);
            Assert.NotNull(videoStorage);
        }

        private static IConfiguration BuildConfiguration(string? missingKey)
        {
            var values = new Dictionary<string, string?>
            {
                ["AWS:AccessKey"] = "access-key",
                ["AWS:SecretKey"] = "secret-key",
                ["AWS:Region"] = "us-east-1",
                ["AWS:BucketName"] = "spectrum-test"
            };

            if (missingKey is not null)
            {
                values.Remove(missingKey);
            }

            return new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
        }
    }
}
