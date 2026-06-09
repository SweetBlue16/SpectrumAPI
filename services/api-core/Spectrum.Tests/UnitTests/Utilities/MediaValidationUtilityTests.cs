using Microsoft.AspNetCore.Http;
using Spectrum.API.Exceptions;
using Spectrum.API.Utilities;

namespace Spectrum.Tests.UnitTests.Utilities
{
    public class MediaValidationUtilityTests
    {
        [Fact]
        public void ValidateImageWhenJpegWithinLimitShouldPass()
        {
            var file = CreateFormFile("cover.jpg", "image/jpeg", length: 1024);

            MediaValidationUtility.ValidateImage(file, maxSizeMb: 1);
        }

        [Fact]
        public void ValidateImageWhenFileIsTooLargeShouldThrow()
        {
            var file = CreateFormFile("cover.jpg", "image/jpeg", length: 2 * 1024 * 1024);

            var exception = Assert.Throws<SpectrumFileValidationException>(() =>
                MediaValidationUtility.ValidateImage(file, maxSizeMb: 1));

            Assert.Contains("maximum allowed size", exception.Message);
        }

        [Theory]
        [InlineData("cover.gif", "image/gif", "Invalid image content type")]
        [InlineData("cover.bmp", "image/png", "Invalid image format")]
        public void ValidateImageWhenContentTypeOrExtensionIsInvalidShouldThrow(
            string fileName,
            string contentType,
            string expectedMessage)
        {
            var file = CreateFormFile(fileName, contentType, length: 1024);

            var exception = Assert.Throws<SpectrumFileValidationException>(() =>
                MediaValidationUtility.ValidateImage(file, maxSizeMb: 1));

            Assert.Contains(expectedMessage, exception.Message);
        }

        [Fact]
        public void ValidateReviewAttachmentWhenVideoHasAllowedTypeAndExtensionShouldSkipDurationCheck()
        {
            var file = CreateFormFile("clip.mp4", "video/mp4", length: 1024);

            MediaValidationUtility.ValidateReviewAttachment(file);
        }

        [Theory]
        [InlineData("clip.avi", "video/mp4", "Invalid video format")]
        [InlineData("notes.txt", "text/plain", "Invalid attachment format")]
        public void ValidateReviewAttachmentWhenFormatIsUnsupportedShouldThrow(
            string fileName,
            string contentType,
            string expectedMessage)
        {
            var file = CreateFormFile(fileName, contentType, length: 1024);

            var exception = Assert.Throws<SpectrumFileValidationException>(() =>
                MediaValidationUtility.ValidateReviewAttachment(file));

            Assert.Contains(expectedMessage, exception.Message);
        }

        [Fact]
        public void ValidateVideoWhenContentTypeIsUnsupportedShouldThrowBeforeReadingMetadata()
        {
            var file = CreateFormFile("clip.mp4", "video/x-msvideo", length: 1024);

            var exception = Assert.Throws<SpectrumFileValidationException>(() =>
                MediaValidationUtility.ValidateVideo(file, maxSizeMb: 1, maxDurationSeconds: 16));

            Assert.Contains("Invalid video content type", exception.Message);
        }

        private static IFormFile CreateFormFile(string fileName, string contentType, long length)
        {
            var bytes = new byte[length];
            var stream = new MemoryStream(bytes);

            return new FormFile(stream, 0, length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
        }
    }
}
