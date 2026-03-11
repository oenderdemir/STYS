using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Bildirimler.Entities;

public class Bildirim : BaseEntity<int>
{
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(64)]
    public string Tip { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Baslik { get; set; } = string.Empty;

    [Required]
    [MaxLength(1024)]
    public string Mesaj { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Link { get; set; }

    [Required]
    [MaxLength(16)]
    public string Severity { get; set; } = "info";

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }
}
