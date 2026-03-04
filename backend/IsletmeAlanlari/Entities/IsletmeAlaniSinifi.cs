using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.IsletmeAlanlari.Entities;

public class IsletmeAlaniSinifi : BaseEntity<int>
{
    [Required]
    [MaxLength(64)]
    public string Kod { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Ad { get; set; } = string.Empty;

    public bool AktifMi { get; set; } = true;

    public ICollection<IsletmeAlani> IsletmeAlanlari { get; set; } = [];
}
