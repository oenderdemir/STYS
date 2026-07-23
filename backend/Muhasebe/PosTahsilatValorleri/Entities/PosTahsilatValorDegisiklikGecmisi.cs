using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.PosTahsilatValorleri.Entities;

/// <summary>
/// PosTahsilatValor uzerinde yapilan manuel komisyon/net/hesap degisikliklerinin degistirilemez
/// audit izi. RezervasyonDegisiklikGecmisi ile birebir ayni desen - yalnizca INSERT edilir,
/// hicbir servis/endpoint guncelleme/silme saglamaz. Kim/ne zaman bilgisi BaseEntity.CreatedBy/
/// CreatedAt uzerinden gelir.
/// </summary>
public class PosTahsilatValorDegisiklikGecmisi : BaseEntity<int>
{
    public int PosTahsilatValorId { get; set; }

    [Required]
    [MaxLength(64)]
    public string IslemTipi { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Aciklama { get; set; }

    public string? OncekiDegerJson { get; set; }

    public string? YeniDegerJson { get; set; }

    public PosTahsilatValor? PosTahsilatValor { get; set; }
}
