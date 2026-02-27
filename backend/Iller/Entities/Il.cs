using System.ComponentModel.DataAnnotations;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Iller.Entities;

public class Il : BaseEntity<int>
{
    [Required]
    [MaxLength(128)]
    public string Ad { get; set; } = string.Empty;

    public bool AktifMi { get; set; } = true;

    public ICollection<Tesis> Tesisler { get; set; } = [];
}
