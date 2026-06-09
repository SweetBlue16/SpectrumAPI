using System.ComponentModel.DataAnnotations;

namespace Spectrum.API.Dtos.Admin
{
    /// <summary>
    /// Represents the profile information of an administrator account.
    /// </summary>
    public class AdminProfileDto
    {
        /// <summary>
        /// Unique identifier of the administrator.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Username used for authentication and display purposes.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Email address associated with the administrator account.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Administrator's first name.
        /// </summary>
        public string FirstName { get; set; } = string.Empty;

        /// <summary>
        /// Administrator's last name.
        /// </summary>
        public string LastName { get; set; } = string.Empty;

        /// <summary>
        /// Contact phone number.
        /// </summary>
        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>
        /// Physical address registered for the administrator.
        /// </summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// Tax identification number (RFC).
        /// </summary>
        public string Rfc { get; set; } = string.Empty;

        /// <summary>
        /// URL of the administrator profile picture.
        /// </summary>
        public string? ProfilePicture { get; set; }

        /// <summary>
        /// Assigned system role.
        /// </summary>
        public string Role { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request payload used to update administrator profile information.
    /// </summary>
    public class UpdateAdminProfileDto
    {
        /// <summary>
        /// Updated username for the administrator account.
        /// </summary>
        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Updated first name.
        /// </summary>
        [Required]
        [StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        /// <summary>
        /// Updated last name.
        /// </summary>
        [Required]
        [StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        /// <summary>
        /// Updated contact phone number in E.164 format.
        /// </summary>
        [Required]
        [RegularExpression(@"^\+?[1-9]\d{1,14}$")]
        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>
        /// Updated physical address.
        /// </summary>
        [Required]
        [StringLength(255, MinimumLength = 5)]
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// Updated profile picture URL.
        /// </summary>
        [StringLength(2048)]
        public string? ProfilePicture { get; set; }
    }
}
