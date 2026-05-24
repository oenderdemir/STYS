using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri.Enums;

namespace STYS.Muhasebe.SatisBelgeleri.Dtos;

/// <summary>
/// Operasyon modüllerinin (otel, restoran, kamp vb.) ortak fatura altyapısına
/// taslak oluşturmak için göndereceği request modeli.
/// </summary>
public class SatisBelgesiTaslakOlusturRequest
{
    public SatisKaynakModulu KaynakModul { get; set; }
    public string KaynakTipi { get; set; } = string.Empty;
    public string KaynakId { get; set; } = string.Empty;

    public int? TesisId { get; set; }

    public DateTime BelgeTarihi { get; set; }
    public DateTime? VadeTarihi { get; set; }

    public bool KurumsalMi { get; set; }

    public string? MusteriUnvan { get; set; }
    public string? MusteriAdSoyad { get; set; }
    public string? MusteriVergiNo { get; set; }
    public string? MusteriTcKimlikNo { get; set; }
    public string? MusteriVergiDairesi { get; set; }
    public string? MusteriAdres { get; set; }
    public string? MusteriEposta { get; set; }
    public string? MusteriTelefon { get; set; }

    public string? Aciklama { get; set; }

    public List<SatisBelgesiTaslakSatirRequest> Satirlar { get; set; } = [];
}

/// <summary>
/// Taslak satır request modeli. SiraNo yoktur; servis tarafından liste sırasına göre
/// 1'den başlayarak atanır.
/// </summary>
public class SatisBelgesiTaslakSatirRequest
{
    public SatisBelgesiSatirTipi SatirTipi { get; set; } = SatisBelgesiSatirTipi.Diger;

    public string Aciklama { get; set; } = string.Empty;

    public decimal Miktar { get; set; }
    public decimal BirimFiyat { get; set; }

    public KdvUygulamaTipi KdvUygulamaTipi { get; set; } = KdvUygulamaTipi.Kdvli;
    public decimal KdvOrani { get; set; }

    public int? KdvIstisnaTanimId { get; set; }

    public string? KaynakSatirId { get; set; }
}
