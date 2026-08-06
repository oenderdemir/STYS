using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.SatisBelgeleri.Entities;

/// <summary>
/// Faz 2B.10 - <see cref="KurumEBelgePolitikasi"/> üzerindeki HER anlamlı değişikliğin
/// önceki/yeni değerlerini KALICI olarak saklayan immutable audit revizyonu (bkz. görev md.16 -
/// mevcut `BaseEntity` audit alanları (`CreatedBy`/`UpdatedBy`) yalnız KİM/NE ZAMAN'ı tutar, ÖNCEKİ
/// değeri TUTMAZ). Actor bilgisi (kim değiştirdi) bu entity'nin KENDİ `CreatedBy` alanından (mevcut
/// `StysAppDbContext.ApplyAuditInfo` mekanizmasıyla, authenticated current user'dan) OTOMATİK gelir -
/// request body'den ALINMAZ.
///
/// VKN/TCKN, token, parola, sertifika, endpoint secret veya müşteri bilgisi TUTULMAZ - yalnız
/// yöntem/aktiflik/sürüm geçişleri ve zorunlu, sınırlı uzunluklu bir değişiklik nedeni.
/// </summary>
public class KurumEBelgePolitikaRevizyonu : BaseEntity<int>, ITenantEntity
{
    public int KurumId { get; set; }

    public int KurumEBelgePolitikasiId { get; set; }
    public KurumEBelgePolitikasi KurumEBelgePolitikasi { get; set; } = null!;

    public int EskiSurum { get; set; }

    public int YeniSurum { get; set; }

    public EBelgeEntegrasyonYontemi EskiYontem { get; set; }

    public EBelgeEntegrasyonYontemi YeniYontem { get; set; }

    public bool EskiAktifMi { get; set; }

    public bool YeniAktifMi { get; set; }

    public DateTime DegisiklikZamaniUtc { get; set; }

    public string DegisiklikNedeni { get; set; } = string.Empty;
}
