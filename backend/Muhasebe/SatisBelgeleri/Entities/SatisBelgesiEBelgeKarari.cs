using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.SatisBelgeleri.Entities;

/// <summary>
/// Faz 2B.10 - bir satış belgesinin e-belge süreciyle İLGİLİ olarak, kesim ANINDA verilen kararın
/// IMMUTABLE snapshot'ı. Kurum politikası DAHA SONRA değiştirilse (hatta silinse) BİLE, bu kayıt
/// o ANDA geçerli olan yöntem/yetenek planını KALICI olarak SAKLAR - geçmiş satış belgeleri asla
/// yeniden yorumlanmaz (bkz. görev md.10).
///
/// `YerelSnapshotOlustur`/`YerelUnsignedUblOlustur`/`YerelImzaOlustur`/`OtomatikGonderimYap`
/// alanları KULLANICI CONFIG'i DEĞİLDİR - karar anında <see cref="IEBelgeYontemYetenekSaglayici"/>
/// üzerinden type-safe olarak TÜRETİLMİŞ, salt-okunur bir plan snapshot'ıdır.
///
/// Oluşturulduktan SONRA update/delete EDİLEMEZ (bkz. `StysAppDbContext.ApplyAuditInfo`, EBelgeSnapshot/
/// EBelgeArtifact ile AYNI immutability deseni).
/// </summary>
public class SatisBelgesiEBelgeKarari : BaseEntity<int>, ITenantEntity
{
    public int KurumId { get; set; }

    public int SatisBelgesiId { get; set; }
    public SatisBelgesi SatisBelgesi { get; set; } = null!;

    /// <summary>Karar anında kullanılan politika kaydı - politika SONRADAN silinse/pasife alınsa bile bu referans (varsa) KORUNUR (Restrict delete).</summary>
    public int? KurumEBelgePolitikasiId { get; set; }
    public KurumEBelgePolitikasi? KurumEBelgePolitikasi { get; set; }

    /// <summary>Karar anındaki politika sürümü - politika kaydı YOKSA (ör. `PolitikaYapilandirilmadi`) null'dur.</summary>
    public int? PolitikaSurumu { get; set; }

    public EBelgeEntegrasyonYontemi EntegrasyonYontemi { get; set; }

    public EBelgeKurumPolitikaKararNedeni KararNedeni { get; set; }

    public bool YerelSnapshotOlustur { get; set; }

    public bool YerelUnsignedUblOlustur { get; set; }

    public bool YerelImzaOlustur { get; set; }

    public bool OtomatikGonderimYap { get; set; }

    public DateTime KararZamaniUtc { get; set; }

    /// <summary>Karardan SONRA (yerel pipeline gerekiyorsa) oluşturulan EBelgeKaydi - yalnız AYNI kurum/satış belgesine ait olabilir.</summary>
    public int? EBelgeKaydiId { get; set; }
    public EBelgeKaydi? EBelgeKaydi { get; set; }
}
