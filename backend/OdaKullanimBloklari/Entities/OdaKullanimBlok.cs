using System.ComponentModel.DataAnnotations;
using STYS.Odalar.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.OdaKullanimBloklari.Entities;

public class OdaKullanimBlok : BaseEntity<int>
{
    public int TesisId { get; set; }

    public int OdaId { get; set; }

    [Required]
    [MaxLength(16)]
    public string BlokTipi { get; set; } = OdaKullanimBlokTipleri.Bakim;

    public DateTime BaslangicTarihi { get; set; }

    public DateTime BitisTarihi { get; set; }

    [MaxLength(512)]
    public string? Aciklama { get; set; }

    public bool AktifMi { get; set; } = true;

    public Tesis? Tesis { get; set; }

    public Oda? Oda { get; set; }
}

