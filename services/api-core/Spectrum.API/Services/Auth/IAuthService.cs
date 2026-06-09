using Spectrum.API.Dtos.Auth;
using Spectrum.API.Exceptions;
using Spectrum.API.Models;
using Spectrum.API.Repositories;
using Spectrum.API.Services.Email;
using Spectrum.API.Utilities;
using Google.Apis.Auth;
using static Google.Apis.Auth.GoogleJsonWebSignature;
using System.Security.Cryptography;

namespace Spectrum.API.Services.Auth
{
    /// <summary>
    /// Defines the contracts for identity management, authentication, and user registration in Spectrum.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Registers a new standard user account (Reviewer) using the provided registration details.
        /// </summary>
        /// <param name="registerDto">The registration information for the new user, including username, email, and password. Cannot be null.</param>
        /// <returns>
        /// A <see cref="RegisterResponseDto"/> containing the email address,
        /// verification requirements, and registration status information.
        /// </returns>
        /// <exception cref="SpectrumBusinessException">Thrown when the email or username is already registered.</exception>
        Task<RegisterResponseDto> RegisterAsync(RegisterDto registerDto);

        /// <summary>
        /// Registers a new administrator account using the provided registration details and a master key.
        /// </summary>
        /// <param name="registerAdminDto">The registration information for the new administrator, including personal details and the required admin secret key. Cannot be null.</param>
        /// <returns>An <see cref="AuthResponseDto"/> containing the authentication token and account details for the newly registered administrator.</returns>
        /// <exception cref="SpectrumUnauthorizedException">Thrown if the provided admin secret key is invalid.</exception>
        /// <exception cref="SpectrumBusinessException">Thrown if any required personal detail is missing or if email/username is already taken.</exception>
        Task<AuthResponseDto> RegisterAdminAsync(RegisterAdminDto registerAdminDto);

        /// <summary>
        /// Registers a new administrator account on behalf of an authenticated administrator.
        /// </summary>
        /// <param name="registerAdminDto">
        /// The registration information for the administrator account to be created.
        /// Cannot be null.
        /// </param>
        /// <returns>
        /// An <see cref="AuthResponseDto"/> containing the authentication token and account details
        /// for the newly created administrator.
        /// </returns>
        /// <exception cref="SpectrumBusinessException">
        /// Thrown if the email or username is already registered, or if required information is invalid.
        /// </exception>
        Task<AuthResponseDto> RegisterAdminByAdminAsync(RegisterAdminDto registerAdminDto);

        /// <summary>
        /// Authenticates a user with the provided local login credentials and generates a JWT token upon successful authentication.
        /// </summary>
        /// <param name="loginDto">An object containing the user's login credentials, including email and password. Cannot be null.</param>
        /// <returns>An <see cref="AuthResponseDto"/> containing the generated JWT token and user information if authentication is successful.</returns>
        /// <exception cref="SpectrumUnauthorizedException">Thrown if the credentials are invalid, the user does not exist, or the account is suspended.</exception>
        Task<AuthResponseDto> LoginAsync(LoginDto loginDto);

        /// <summary>
        /// Handles the Google Single Sign-On (SSO) process by validating the provided Google OAuth token,
        /// creating or retrieving the corresponding user, and generating a local JWT for authentication.
        /// </summary>
        /// <param name="googleAuthDto">The Google authentication data transfer object containing the OAuth credential token.</param>
        /// <returns>An <see cref="AuthResponseDto"/> containing the local JWT, username, and email of the authenticated user.</returns>
        /// <exception cref="SpectrumUnauthorizedException">Thrown if the Google token is invalid or if the associated local account is suspended.</exception>
        Task<AuthResponseDto> GoogleLoginAsync(GoogleAuthDto googleAuthDto);

        /// <summary>
        /// Verifies a user's registration code and activates the account.
        /// </summary>
        /// <param name="verifyDto">
        /// The verification request containing the user's email address and registration code.
        /// Cannot be null.
        /// </param>
        /// <returns>
        /// An <see cref="AuthResponseDto"/> containing the authenticated user's information
        /// and a newly issued JWT token.
        /// </returns>
        /// <exception cref="SpectrumUnauthorizedException">
        /// Thrown when the verification code is invalid, expired, or does not match the specified email address.
        /// </exception>
        Task<AuthResponseDto> VerifyRegistrationAsync(VerifyRegistrationCodeDto verifyDto);

        /// <summary>
        /// Generates and sends a new registration verification code to a pending user account.
        /// </summary>
        /// <param name="resendDto">
        /// The request containing the email address associated with the account awaiting verification.
        /// Cannot be null.
        /// </param>
        /// <returns>
        /// A <see cref="MessageResponseDto"/> confirming that a new verification code has been issued.
        /// </returns>
        /// <exception cref="SpectrumBusinessException">
        /// Thrown when a verification code was recently issued and the resend cooldown period has not yet elapsed.
        /// </exception>
        Task<MessageResponseDto> ResendRegistrationCodeAsync(ResendRegistrationCodeDto resendDto);

        /// <summary>
        /// Initiates the password recovery workflow by generating and sending a verification code.
        /// </summary>
        /// <param name="forgotPasswordDto">
        /// The password recovery request containing the user's email address.
        /// Cannot be null.
        /// </param>
        /// <returns>
        /// A <see cref="MessageResponseDto"/> confirming that the recovery process has been initiated.
        /// </returns>
        Task<MessageResponseDto> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto);

        /// <summary>
        /// Validates a password recovery verification code and creates a temporary verification session.
        /// </summary>
        /// <param name="verifyDto">
        /// The verification request containing the user's email address and recovery code.
        /// Cannot be null.
        /// </param>
        /// <returns>
        /// A <see cref="PasswordCodeVerifiedDto"/> containing a temporary verification token
        /// required to complete the password reset process.
        /// </returns>
        /// <exception cref="SpectrumUnauthorizedException">
        /// Thrown when the verification code is invalid, expired, or exceeds the maximum number of allowed attempts.
        /// </exception>
        Task<PasswordCodeVerifiedDto> VerifyPasswordResetCodeAsync(VerifyPasswordCodeDto verifyDto);

        /// <summary>
        /// Completes the password reset workflow using a previously issued verification token.
        /// </summary>
        /// <param name="resetPasswordDto">
        /// The password reset request containing the user's email address,
        /// verification token, and new password.
        /// Cannot be null.
        /// </param>
        /// <returns>
        /// A <see cref="MessageResponseDto"/> confirming that the password has been successfully updated.
        /// </returns>
        /// <exception cref="SpectrumUnauthorizedException">
        /// Thrown when the verification token is invalid, expired, or does not match the specified email address.
        /// </exception>
        Task<MessageResponseDto> ResetPasswordAsync(ResetPasswordDto resetPasswordDto);
    }

    /// <summary>
    /// Service responsible for handling authentication-related operations, 
    /// including user registration, local login, and Google OAuth integration.
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IAdminDetailRepository _adminDetailRepository;
        private readonly IConfiguration _configuration;
        private readonly IVerificationCodeService _verificationCodeService;
        private readonly IEmailService _emailService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthService"/> class with the specified repositories 
        /// and configuration settings.
        /// </summary>
        /// <param name="userRepository">The user repository used to access and manage user data. Cannot be null.</param>
        /// <param name="adminDetailRepository">The admin detail repository used to access and manage admin details. Cannot be null.</param>
        /// <param name="configuration">The configuration settings used to retrieve authentication-related options. Cannot be null.</param>
        /// <param name="verificationCodeService">The service responsible for one-time verification codes.</param>
        /// <param name="emailService">The service responsible for delivering transactional email.</param>
        public AuthService(
            IUserRepository userRepository,
            IAdminDetailRepository adminDetailRepository,
            IConfiguration configuration,
            IVerificationCodeService verificationCodeService,
            IEmailService emailService)
        {
            _userRepository = userRepository;
            _configuration = configuration;
            _adminDetailRepository = adminDetailRepository;
            _verificationCodeService = verificationCodeService;
            _emailService = emailService;
        }

        /// <inheritdoc />
        public async Task<AuthResponseDto> GoogleLoginAsync(GoogleAuthDto googleAuthDto)
        {
            Payload payload;
            try
            {
                var settings = new ValidationSettings
                {
                    Audience = new[] { _configuration["GoogleAuth:ClientId"] }
                };
                payload = await ValidateAsync(googleAuthDto.Credential, settings);
            }
            catch (InvalidJwtException)
            {
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.Unauthorized);
            }

            var user = await CreateOrGetGoogleUserAsync(payload);
            return new AuthResponseDto
            {
                Token = AuthUtilities.GenerateJwtToken(user, _configuration),
                Username = user.Username,
                Email = user.Email,
                Role = user.Role
            };
        }

        /// <inheritdoc />
        public async Task<AuthResponseDto> LoginAsync(LoginDto loginDto)
        {
            var user = await _userRepository.GetUserByEmailIncludingDeletedAsync(NormalizeEmail(loginDto.Email)) ??
                await _userRepository.GetUserByEmailAsync(NormalizeEmail(loginDto.Email));
            await AuthUtilities.ValidateLoginInput(user, loginDto);
            var authenticatedUser = user!;

            return BuildAuthResponse(authenticatedUser);
        }

        /// <inheritdoc />
        public async Task<AuthResponseDto> RegisterAdminAsync(RegisterAdminDto registerAdminDto)
        {
            var masterKey = _configuration["AdminSettings:MasterKey"];
            await AuthUtilities.ValidateRegisterInput(registerAdminDto, _userRepository);
            await AuthUtilities.ValidateRegisterAdminInput(registerAdminDto, _adminDetailRepository, masterKey);

            return await CreateAdminAsync(registerAdminDto);
        }

        /// <inheritdoc />
        public async Task<AuthResponseDto> RegisterAdminByAdminAsync(RegisterAdminDto registerAdminDto)
        {
            await AuthUtilities.ValidateRegisterInput(registerAdminDto, _userRepository);
            await ValidateAdminProfileAsync(registerAdminDto);

            return await CreateAdminAsync(registerAdminDto);
        }

        private async Task<AuthResponseDto> CreateAdminAsync(RegisterAdminDto registerAdminDto)
        {
            var user = new User
            {
                Username = registerAdminDto.Username.Trim(),
                Email = NormalizeEmail(registerAdminDto.Email),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerAdminDto.Password),
                CreatedAt = DateTime.UtcNow,
                Role = Constants.Roles.Admin,
                IsEmailVerified = true
            };
            var createdUser = await _userRepository.AddUserAsync(user);

            var adminDetail = new AdminDetail
            {
                Id = Guid.NewGuid(),
                UserId = createdUser.Id,
                FirstName = registerAdminDto.FirstName.Trim(),
                LastName = registerAdminDto.LastName.Trim(),
                PhoneNumber = registerAdminDto.PhoneNumber.Trim(),
                Address = registerAdminDto.Address.Trim(),
                Rfc = registerAdminDto.Rfc.Trim().ToUpperInvariant()
            };
            await _adminDetailRepository.AddAdminDetailAsync(adminDetail);

            return new AuthResponseDto
            {
                Token = AuthUtilities.GenerateJwtToken(user, _configuration),
                Username = user.Username,
                Email = user.Email,
                Role = user.Role
            };
        }

        /// <inheritdoc />
        public async Task<RegisterResponseDto> RegisterAsync(RegisterDto registerDto)
        {
            await AuthUtilities.ValidateRegisterInput(registerDto, _userRepository);
            var user = new User
            {
                Username = registerDto.Username,
                Email = NormalizeEmail(registerDto.Email),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
                CreatedAt = DateTime.UtcNow,
                Role = Constants.Roles.Reviewer,
                IsSuspended = false,
                IsDeleted = false,
                IsEmailVerified = false
            };

            await _userRepository.AddUserAsync(user);
            var code = await _verificationCodeService.CreateCodeAsync(VerificationPurpose.RegisterVerification, user.Email, user.Id);
            try
            {
                await _emailService.SendRegistrationVerificationAsync(user.Email, code);
            }
            catch (SpectrumServiceUnavailableException)
            {
                user.IsDeleted = true;
                await _userRepository.UpdateUserAsync(user);
                throw;
            }

            return new RegisterResponseDto
            {
                Email = user.Email,
                RequiresVerification = true,
                Message = Constants.ErrorMessages.VerificationCodeSent
            };
        }

        /// <inheritdoc />
        public async Task<AuthResponseDto> VerifyRegistrationAsync(VerifyRegistrationCodeDto verifyDto)
        {
            var email = NormalizeEmail(verifyDto.Email);
            var user = await _userRepository.GetUserByEmailAsync(email);
            if (user == null)
            {
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.VerificationCodeInvalid);
            }

            await _verificationCodeService.ConsumeCodeAsync(
                VerificationPurpose.RegisterVerification,
                email,
                verifyDto.Code,
                user.Id
            );

            user.IsEmailVerified = true;
            await _userRepository.UpdateUserAsync(user);

            return BuildAuthResponse(user);
        }

        /// <inheritdoc />
        public async Task<MessageResponseDto> ResendRegistrationCodeAsync(ResendRegistrationCodeDto resendDto)
        {
            var email = NormalizeEmail(resendDto.Email);
            var user = await _userRepository.GetUserByEmailAsync(email);
            if (user == null || user.IsEmailVerified)
            {
                return new MessageResponseDto { Message = Constants.ErrorMessages.VerificationCodeSent };
            }

            var code = await _verificationCodeService.CreateCodeAsync(VerificationPurpose.RegisterVerification, email, user.Id);
            await _emailService.SendRegistrationVerificationAsync(email, code);

            return new MessageResponseDto { Message = Constants.ErrorMessages.VerificationCodeSent };
        }

        /// <inheritdoc />
        public async Task<MessageResponseDto> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto)
        {
            var email = NormalizeEmail(forgotPasswordDto.Email);
            var user = await _userRepository.GetUserByEmailIncludingDeletedAsync(email);
            if (user == null)
            {
                throw new SpectrumBusinessException(Constants.ErrorMessages.EmailNotRegistered);
            }

            if (user.IsDeleted)
            {
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.AccountDeleted);
            }

            if (user.IsBanned)
            {
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.AccountBanned);
            }

            if (user.IsSuspended)
            {
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.AccountSuspended);
            }

            if (user.IsEmailVerified)
            {
                try
                {
                    var code = await _verificationCodeService.CreateCodeAsync(VerificationPurpose.PasswordReset, email, user.Id);
                    await _emailService.SendPasswordResetAsync(email, code);
                }
                catch (SpectrumBusinessException)
                {
                    return new MessageResponseDto { Message = Constants.ErrorMessages.PasswordResetInstructionsSent };
                }
            }

            return new MessageResponseDto { Message = Constants.ErrorMessages.PasswordResetInstructionsSent };
        }

        /// <inheritdoc />
        public async Task<PasswordCodeVerifiedDto> VerifyPasswordResetCodeAsync(VerifyPasswordCodeDto verifyDto)
        {
            var email = NormalizeEmail(verifyDto.Email);
            var user = await _userRepository.GetUserByEmailAsync(email);
            if (user == null)
            {
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.VerificationCodeInvalid);
            }

            var token = await _verificationCodeService.VerifyCodeAndCreateSessionAsync(
                VerificationPurpose.PasswordReset,
                email,
                verifyDto.Code,
                user.Id
            );

            return new PasswordCodeVerifiedDto
            {
                VerificationToken = token,
                Message = "verificationCodeVerified"
            };
        }

        /// <inheritdoc />
        public async Task<MessageResponseDto> ResetPasswordAsync(ResetPasswordDto resetPasswordDto)
        {
            var email = NormalizeEmail(resetPasswordDto.Email);
            var user = await _userRepository.GetUserByEmailAsync(email);
            if (user == null)
            {
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.VerificationCodeInvalid);
            }

            await _verificationCodeService.ConsumeSessionAsync(
                VerificationPurpose.PasswordReset,
                email,
                resetPasswordDto.VerificationToken,
                user.Id
            );

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(resetPasswordDto.NewPassword);
            await _userRepository.UpdateUserAsync(user);

            return new MessageResponseDto { Message = "passwordUpdated" };
        }

        /// <summary>
        /// Creates a new user account based on Google authentication information if no existing user is found, or
        /// retrieves the existing user associated with the provided email address.
        /// </summary>
        /// <param name="payload">The Google authentication payload containing user information such as email and name. Cannot be null.</param>
        /// <returns>A user entity corresponding to the provided Google account. If the user does not exist, a new user is created and returned.</returns>
        /// <exception cref="SpectrumUnauthorizedException">Thrown if the existing user account associated with the provided email is suspended.</exception>
        private async Task<User> CreateOrGetGoogleUserAsync(Payload payload)
        {
            var user = await _userRepository.GetUserByEmailIncludingDeletedAsync(payload.Email);
            if (user == null)
            {
                user = new User
                {
                    Username = payload.Name.Replace(" ", "_").ToLower() + RandomNumberGenerator.GetInt32(1000, 10000),
                    Email = payload.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                    CreatedAt = DateTime.UtcNow,
                    Role = Constants.Roles.Reviewer,
                    IsSuspended = false,
                    IsDeleted = false,
                    IsEmailVerified = payload.EmailVerified
                };
                await _userRepository.AddUserAsync(user);
            }
            else if (user.IsDeleted)
            {
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.AccountDeleted);
            }
            else if (user.IsBanned)
            {
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.AccountBanned);
            }
            else if (user.IsSuspended)
            {
                throw new SpectrumUnauthorizedException(Constants.ErrorMessages.AccountSuspended);
            }
            else if (payload.EmailVerified && !user.IsEmailVerified)
            {
                user.IsEmailVerified = true;
                await _userRepository.UpdateUserAsync(user);
            }
            return user;
        }

        private AuthResponseDto BuildAuthResponse(User user)
        {
            return new AuthResponseDto
            {
                Token = AuthUtilities.GenerateJwtToken(user, _configuration),
                Username = user.Username,
                Email = user.Email,
                Role = user.Role
            };
        }

        private async Task ValidateAdminProfileAsync(RegisterAdminDto registerAdminDto)
        {
            if (string.IsNullOrWhiteSpace(registerAdminDto.FirstName) ||
                string.IsNullOrWhiteSpace(registerAdminDto.LastName) ||
                string.IsNullOrWhiteSpace(registerAdminDto.PhoneNumber) ||
                string.IsNullOrWhiteSpace(registerAdminDto.Address) ||
                string.IsNullOrWhiteSpace(registerAdminDto.Rfc))
            {
                throw new SpectrumBusinessException(Constants.ErrorMessages.MissingRequiredParameter);
            }

            if (await _adminDetailRepository.RfcExistsAsync(registerAdminDto.Rfc))
            {
                throw new SpectrumBusinessException("rfcAlreadyRegistered");
            }
        }

        private static string NormalizeEmail(string email)
        {
            return email.Trim().ToLowerInvariant();
        }
    }
}
