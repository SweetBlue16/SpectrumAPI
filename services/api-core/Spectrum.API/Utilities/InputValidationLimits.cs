namespace Spectrum.API.Utilities
{
    /// <summary>
    /// Shared input limits for API DTOs and services that enforce Spectrum business rules.
    /// </summary>
    public static class InputValidationLimits
    {
        public const int ShortText = 120;
        public const int MediumText = 300;
        public const int ReportDescription = 500;
        public const int ReviewContent = 2000;
        public const int CommentContent = 500;
        public const int DropRewardCode = 50;
        public const int DropCodesPayload = 2000;
    }
}
