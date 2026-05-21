using STYS.Muhasebe.Kdv.Enums;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.Kdv.Entities;

/// <summary>
/// KDV istisna tanımı. E-fatura/e-arşiv veya kurum içi istisna kodlarını temsil eder.
/// </summary>
public class KdvIstisnaTanim : BaseEntity<int>
{
    /// <summary>e-Fatura/e-Arşiv veya kurum içi istisna kodu.</summary>
    public string Kod { get; set; } = string.Empty;

    /// <summary>Kullanıcıya gösterilecek ad.</summary>
    public string Ad { get; set; } = string.Empty;

    /// <summary>Açıklama.</summary>
    public string? Aciklama { get; set; }

    /// <summary>KDV uygulama tipi.</summary>
    public KdvUygulamaTipi UygulamaTipi { get; set; }

    /// <summary>Satış işlemlerinde kullanılabilir mi?</summary>
    public bool SatisIslemlerindeKullanilirMi { get; set; }

    /// <summary>Alış işlemlerinde kullanılabilir mi?</summary>
    public bool AlisIslemlerindeKullanilirMi { get; set; }

    /// <summary>Yüklenilen KDV indirilebilir mi? (Tam/kısmi istisna ayrımında önemli.)</summary>
    public bool YuklenilenKdvIndirilebilirMi { get; set; }

    /// <summary>İade hakkı var mı? (Tam istisna / iade süreçleri için altyapı.)</summary>
    public bool IadeHakkiVarMi { get; set; }

    /// <summary>E-Belge üretiminde kod zorunlu mu?</summary>
    public bool EBelgeKoduZorunluMu { get; set; }

    /// <summary>Aktif mi? Eski kodlar pasif yapılabilir.</summary>
    public bool AktifMi { get; set; } = true;

    /// <summary>Geçerlilik başlangıç tarihi (mevzuat değişikliği için).</summary>
    public DateTime? GecerlilikBaslangicTarihi { get; set; }

    /// <summary>Geçerlilik bitiş tarihi (mevzuat değişikliği için).</summary>
    public DateTime? GecerlilikBitisTarihi { get; set; }
}
