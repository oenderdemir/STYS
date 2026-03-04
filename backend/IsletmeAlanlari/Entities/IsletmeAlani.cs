using System.ComponentModel.DataAnnotations;
using STYS.Binalar.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.IsletmeAlanlari.Entities;

public class IsletmeAlani : BaseEntity<int>
{
    public int BinaId { get; set; }

    public int IsletmeAlaniSinifiId { get; set; }

    [MaxLength(200)]
    public string? OzelAd { get; set; }

    public bool AktifMi { get; set; } = true;

    public Bina? Bina { get; set; }

    public IsletmeAlaniSinifi? IsletmeAlaniSinifi { get; set; }
}
