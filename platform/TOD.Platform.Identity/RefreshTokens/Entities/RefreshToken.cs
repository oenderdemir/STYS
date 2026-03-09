using System.ComponentModel.DataAnnotations;
using TOD.Platform.Identity.Users.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace TOD.Platform.Identity.RefreshTokens.Entities;

public class RefreshToken : BaseEntity<Guid>
{
    [Required]
    [MaxLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    [MaxLength(512)]
    public string? ReplacedByTokenHash { get; set; }

    [MaxLength(256)]
    public string? RevokeReason { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;
}
