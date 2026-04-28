using System.ComponentModel.DataAnnotations;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.CariKartlar.Entities;

public class MuhasebeHesapKoduSayac : BaseEntity<int>
{
    public int TesisId { get; set; }

    [Required]
    [MaxLength(50)]
    public string AnaHesapKodu { get; set; } = string.Empty;

    public int SonSiraNo { get; set; }

    [MaxLength(512)]
    public string? Aciklama { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public Tesis? Tesis { get; set; }
}

