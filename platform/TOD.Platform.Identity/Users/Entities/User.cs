using System.ComponentModel.DataAnnotations;
using TOD.Platform.Identity.Common.Enums;
using TOD.Platform.Identity.UserUserGroups.Entities;
using TOD.Platform.Persistence.RDBMS.Entities;

namespace TOD.Platform.Identity.Users.Entities;

public class User : BaseEntity<Guid>
{
    [Required]
    public string UserName { get; set; } = string.Empty;

    [MaxLength(11)]
    public string? NationalId { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Email { get; set; }

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public string? AvatarPath { get; set; }

    public UserStatus Status { get; set; } = UserStatus.MustChangePassword;

    public ICollection<UserUserGroup> UserUserGroups { get; set; } = new List<UserUserGroup>();
}
