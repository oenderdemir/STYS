using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kamp.Entities;

public class KampParametre : BaseEntity<int>
{
    [Required]
    [MaxLength(128)]
    public string Kod { get; set; } = string.Empty;

    [Required]
    [MaxLength(512)]
    public string Deger { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Aciklama { get; set; }
}
