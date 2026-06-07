namespace Spectrum.API.Utilities
{
    /// <summary>
    /// A centralized repository for application-wide static constants.
    /// This class helps eliminate "magic strings" and ensures consistency
    /// across authentication, authorization, and error handling mechanisms.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Defines the standard role identifiers used for Role-Based Access Control (RBAC)
        /// throughout the Spectrum ecosystem.
        /// </summary>
        public static class Roles
        {
            /// <summary>
            /// Identifier for users with full administrative privileges, capable of 
            /// managing events, moderating content, and overseeing the platform.
            /// </summary>
            public const string Admin = "ADMIN";

            /// <summary>
            /// Identifier for standard users who can actively participate by 
            /// publishing reviews, casting votes, and claiming drop keys.
            /// </summary>
            public const string Reviewer = "REVIEWER";

            /// <summary>
            /// Identifier for users with read-only access. They can browse content 
            /// but cannot create reviews, vote, or claim keys.
            /// </summary>
            public const string Reader = "READER";
        }

        /// <summary>
        /// A collection of standardized error codes used across the API to provide 
        /// consistent and localized responses to the client application.
        /// </summary>
        public static class ErrorMessages
        {
            public const string InvalidToken = "invalidToken";
            public const string EmailAlreadyRegistered = "emailAlreadyRegistered";
            public const string UserNotFound = "userNotFound";
            public const string InvalidCredentials = "invalidCredentials";
            public const string AuthProviderError = "authProviderError";
            public const string AccountSuspended = "accountSuspended";
            public const string AccountBanned = "accountBanned";
            public const string AccountDeleted = "accountDeleted";
            public const string AccountAlreadySuspended = "accountAlreadySuspended";
            public const string AccountAlreadyBanned = "accountAlreadyBanned";
            public const string AccountAlreadyActive = "accountAlreadyActive";
            public const string UsernameAlreadyTaken = "usernameAlreadyTaken";
            public const string InvalidAdminKey = "invalidAdminKey";
            public const string Unauthorized = "unauthorized";
            public const string MissingRequiredParameter = "missingRequiredParameter";
            public const string InvalidParameterFormat = "invalidParameterFormat";
            public const string EmptyContent = "emptyContent";
            public const string ContentTooLong = "contentTooLong";
            public const string InvalidImageFormat = "invalidImageFormat";
            public const string ImageTooLarge = "imageTooLarge";
            public const string TokenExpired = "tokenExpired";
            public const string InsufficientPermissions = "insufficientPermissions";
            public const string SelfVoteNotAllowed = "selfVoteNotAllowed";
            public const string AdminSanctionForbidden = "adminSanctionForbidden";
            public const string ResourceNotFound = "resourceNotFound";
            public const string DuplicateParticipation = "duplicateParticipation";
            public const string EventKeysExhausted = "eventKeysExhausted";
            public const string CouldNotClaimKey = "couldNotClaimKey";
            public const string InternalServerError = "internalServerError";
            public const string ExternalCatalogUnavailable = "externalCatalogUnavailable";
            public const string RpcServiceUnavailable = "rpcServiceUnavailable";
            public const string DatabaseTimeout = "databaseTimeout";
            public const string AccountNotVerified = "accountNotVerified";
            public const string VerificationCodeInvalid = "verificationCodeInvalid";
            public const string VerificationCodeExpired = "verificationCodeExpired";
            public const string VerificationCodeTooManyAttempts = "verificationCodeTooManyAttempts";
            public const string VerificationCodeSent = "verificationCodeSent";
            public const string PasswordResetInstructionsSent = "passwordResetInstructionsSent";
            public const string EmailNotRegistered = "emailNotRegistered";
            public const string PasswordPolicyViolation = "passwordPolicyViolation";
            public const string SmtpConfigurationInvalid = "smtpConfigurationInvalid";
            public const string ResendCodeTooSoon = "resendCodeTooSoon";
        }
    }
}
