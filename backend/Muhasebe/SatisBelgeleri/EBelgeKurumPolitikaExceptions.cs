using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>
/// Faz 2B.10 görev md.9 - global e-belge süreci aktif olduktan SONRA, kurum politikasının
/// fail-closed nedenlerinden biri (politika yok/yapılandırılmadı, pasif, kurum aktivasyon tarihi
/// gelmedi, yöntem henüz desteklenmiyor, politika geçersiz) satış/faturalama akışını sessizce
/// tamamlatmaz - bu istisna fırlatılır. `GlobalKapali`/`GlobalAktivasyonTarihiGelmedi` (henüz
/// production açık DEĞİLKEN) VE `YontemKullanilmayacak`/`HariciSistemSorumlu` (açıkça HATA
/// OLMAYAN durumlar) BU istisnayı TETİKLEMEZ - satış normal akışla TAMAMLANIR.
/// </summary>
public sealed class EBelgeKurumPolitikaEngelliException : BaseException
{
    public const int HttpStatusCode = 400;

    public string HataKodu { get; }

    public EBelgeKurumPolitikaKararNedeni Neden { get; }

    public EBelgeKurumPolitikaEngelliException(EBelgeKurumPolitikaKararNedeni neden)
        : base(GuvenliMesaj(neden), HttpStatusCode)
    {
        Neden = neden;
        HataKodu = GuvenliKod(neden);
    }

    private static string GuvenliKod(EBelgeKurumPolitikaKararNedeni neden) => neden switch
    {
        EBelgeKurumPolitikaKararNedeni.PolitikaYapilandirilmadi => "EBELGE_KURUM_POLICY_NOT_CONFIGURED",
        EBelgeKurumPolitikaKararNedeni.PolitikaPasif => "EBELGE_KURUM_POLICY_INACTIVE",
        EBelgeKurumPolitikaKararNedeni.KurumAktivasyonTarihiGelmedi => "EBELGE_KURUM_POLICY_BEFORE_ACTIVATION_DATE",
        EBelgeKurumPolitikaKararNedeni.YontemHenuzDesteklenmiyor => "EBELGE_KURUM_POLICY_METHOD_NOT_IMPLEMENTED",
        _ => "EBELGE_KURUM_POLICY_INVALID",
    };

    private static string GuvenliMesaj(EBelgeKurumPolitikaKararNedeni neden) => neden switch
    {
        EBelgeKurumPolitikaKararNedeni.PolitikaYapilandirilmadi => "Kurum için e-belge politikası yapılandırılmamış; fatura kesilemez.",
        EBelgeKurumPolitikaKararNedeni.PolitikaPasif => "Kurum e-belge politikası pasif durumda; fatura kesilemez.",
        EBelgeKurumPolitikaKararNedeni.KurumAktivasyonTarihiGelmedi => "Kurum e-belge politikasının aktivasyon tarihi henüz gelmedi; fatura kesilemez.",
        EBelgeKurumPolitikaKararNedeni.YontemHenuzDesteklenmiyor => "Kurum e-belge politikasının entegrasyon yöntemi henüz desteklenmiyor; fatura kesilemez.",
        _ => "Kurum e-belge politikası geçersiz; fatura kesilemez.",
    };
}

/// <summary>Faz 2B.10 - politika kaydı başka bir kuruma ait olduğunda (cross-tenant erişim denemesi) fırlatılır.</summary>
public sealed class EBelgeKurumPolitikaTenantUyumsuzlugu : BaseException
{
    public const int HttpStatusCode = 403;
    public const string SafeErrorCode = "EBELGE_KURUM_POLICY_TENANT_MISMATCH";

    public string HataKodu { get; } = SafeErrorCode;

    public EBelgeKurumPolitikaTenantUyumsuzlugu()
        : base("Bu kurum e-belge politikasına erişim yetkiniz yok.", HttpStatusCode)
    {
    }
}

/// <summary>Faz 2B.10 görev md.15 - optimistic concurrency (RowVersion) çakışması.</summary>
public sealed class EBelgeKurumPolitikaConcurrencyException : BaseException
{
    public const int HttpStatusCode = 409;
    public const string SafeErrorCode = "EBELGE_KURUM_POLICY_CONCURRENCY_CONFLICT";

    public string HataKodu { get; } = SafeErrorCode;

    public EBelgeKurumPolitikaConcurrencyException()
        : base("Kurum e-belge politikası başka bir işlem tarafından değiştirildi; lütfen güncel veriyi yeniden alıp tekrar deneyin.", HttpStatusCode)
    {
    }
}

/// <summary>Faz 2B.10 görev md.15 - kurumun devam eden (non-terminal) e-belge işi varken entegrasyon yöntemi başka bir yönteme çevrilmeye çalışıldığında fırlatılır.</summary>
public sealed class EBelgeKurumPolitikaDegisiklikEngellendiException : BaseException
{
    public const int HttpStatusCode = 409;
    public const string SafeErrorCode = "EBELGE_KURUM_POLICY_CHANGE_BLOCKED";

    public string HataKodu { get; } = SafeErrorCode;

    public EBelgeKurumPolitikaDegisiklikEngellendiException()
        : base("Kurumun devam eden e-belge işlemleri varken entegrasyon yöntemi değiştirilemez.", HttpStatusCode)
    {
    }
}

/// <summary>
/// Faz 2B.10.1 görev md.3/md.9 - `FaturaKesAsync` içinde, belge zaten `FaturalamaDurumu=Kesildi`
/// (idempotent tekrar) OLMASINA RAĞMEN buna karşılık gelen bir immutable
/// <see cref="STYS.Muhasebe.SatisBelgeleri.Entities.SatisBelgesiEBelgeKarari"/> BULUNAMADIĞINDA
/// fırlatılır - Faz 2B.10 ÖNCESİ (legacy) kesilmiş bir belge olabilir. Bu durum ASLA otomatik
/// yorumlanmaz/varsayılmaz (ör. "DogrudanGib kabul et") - manuel inceleme ve kontrollü, kurum/
/// satış bazlı bir backfill kararı gerektirir (bkz. görev md.3, "Bu turda otomatik legacy
/// backfill yazma").
/// </summary>
public sealed class EBelgeKurumPolitikaKararBulunamadiException : BaseException
{
    public const int HttpStatusCode = 500;
    public const string SafeErrorCode = "EBELGE_KURUM_POLICY_DECISION_NOT_FOUND";

    public string HataKodu { get; } = SafeErrorCode;

    public EBelgeKurumPolitikaKararBulunamadiException(string mesaj)
        : base(mesaj, HttpStatusCode)
    {
    }
}

/// <summary>
/// Faz 2B.10.1 görev md.11 - kurum politikası değerlendirmesi İLE immutable kararın PERSIST
/// edilmesi ARASINDA (aynı transaction içinde bile, READ COMMITTED altında başka bir oturumun
/// eşzamanlı `PUT`'u nedeniyle) politika sürümü DEĞİŞTİYSE fırlatılır - eski bir politika
/// sürümüne göre karar YAZILMAZ, tüm satış kesim işlemi (resmî numara/sayaç dahil) rollback olur.
/// </summary>
public sealed class EBelgeKurumPolitikaKararCakismasiException : BaseException
{
    public const int HttpStatusCode = 409;
    public const string SafeErrorCode = "EBELGE_KURUM_POLICY_DECISION_CONFLICT";

    public string HataKodu { get; } = SafeErrorCode;

    public EBelgeKurumPolitikaKararCakismasiException()
        : base("Kurum e-belge politikası, karar değerlendirmesinden sonra değiştirildi; fatura kesimi güvenli şekilde iptal edildi, lütfen tekrar deneyin.", HttpStatusCode)
    {
    }
}
