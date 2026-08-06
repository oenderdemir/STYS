using STYS.Kurumlar.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.SatisBelgeleri.Entities;

/// <summary>
/// Faz 2B.10 - bir kurumun e-belge sürecini STYS ile HANGİ yöntemle yürüttüğüne dair, kurum
/// başına EN FAZLA BİR kaydı olan politika. `VergiNo`/`VergiDairesi`/`TenantKey` gibi kurum
/// kimlik bilgileri BURADA TEKRAR EDİLMEZ - yalnız <see cref="Kurum"/> ilişkisi üzerinden
/// erişilir (bkz. görev md.4). PIN/parola/token/endpoint secret/sertifika bilgisi TUTULMAZ.
///
/// Bu entity'nin KENDİSİ üzerinde yapılan bir değişiklik (ör. `Kullanilmayacak` -> `GibPortal`),
/// GEÇMİŞ satış belgelerinin e-belge kararını YENİDEN YORUMLAMAZ - geçmiş kararlar
/// <see cref="SatisBelgesiEBelgeKarari"/>'nda immutable olarak SAKLIDIR.
/// </summary>
public class KurumEBelgePolitikasi : BaseEntity<int>, ITenantEntity
{
    public int KurumId { get; set; }

    public Kurum Kurum { get; set; } = null!;

    public EBelgeEntegrasyonYontemi EntegrasyonYontemi { get; set; } = EBelgeEntegrasyonYontemi.Yapilandirilmadi;

    public bool AktifMi { get; set; }

    /// <summary>Türkiye yerel takvim tarihi (saat bileşeni yok) - global aktivasyon tarihinden ÖNCE OLAMAZ (bkz. `EBelgeKurumPolitikaYonetimServisi`).</summary>
    public DateTime? AktivasyonYerelTarihi { get; set; }

    /// <summary>Yalnız <see cref="EBelgeEntegrasyonYontemi.HariciMuhasebeSistemi"/> için ZORUNLU - secret DEĞİLDİR, yalnız kontrollü/sınırlı uzunlukta bir tanımlayıcı koddur.</summary>
    public string? HariciSistemKodu { get; set; }

    /// <summary>Her anlamlı değişiklikte artırılır - ilk sürüm 1'dir. İmmutable karar snapshot'larının hangi politika SÜRÜMÜYLE alındığını izlemek için kullanılır.</summary>
    public int PolitikaSurumu { get; set; } = 1;

    /// <summary>Optimistic concurrency token.</summary>
    public byte[] RowVersion { get; set; } = [];
}
