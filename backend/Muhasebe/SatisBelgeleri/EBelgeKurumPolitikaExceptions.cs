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
