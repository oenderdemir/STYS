using System.ComponentModel.DataAnnotations;
using STYS.Binalar.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.IsletmeAlanlari.Entities;

public class IsletmeAlani : BaseEntity<int>
{
    [Required]
    [MaxLength(200)]
    public string Ad { get; set; } = string.Empty;

    public int BinaId { get; set; }

    public bool AktifMi { get; set; } = true;

    public Bina? Bina { get; set; }
}