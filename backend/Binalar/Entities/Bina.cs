using System.ComponentModel.DataAnnotations;
using STYS.IsletmeAlanlari.Entities;
using STYS.Odalar.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Binalar.Entities;

public class Bina : BaseEntity<int>
{
    [Required]
    [MaxLength(200)]
    public string Ad { get; set; } = string.Empty;

    public int TesisId { get; set; }

    public int KatSayisi { get; set; }

    public bool AktifMi { get; set; } = true;

    public Tesis? Tesis { get; set; }

    public ICollection<IsletmeAlani> IsletmeAlanlari { get; set; } = [];

    public ICollection<Oda> Odalar { get; set; } = [];
}