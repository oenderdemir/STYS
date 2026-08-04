using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>
/// Kesim öncesi UBL hazırlık kapısının hata sınıfları. Her sınıf tek, sabit bir hata kodu taşır
/// ve yalnız o anlam için kullanılır (bkz. görev sonuç raporu, "Hata sınıflarını anlamlı biçimde
/// ayır"). Tarih sınırı ihlali için ayrıca bkz. <see cref="EBelgeInvoiceDateBeforeGoLiveException"/>
/// (Faz 2B.4.1'de tanımlı, buradan tekrar edilmez).
/// </summary>
public sealed class EBelgeUblFeatureDisabledException : BaseException
{
    public const int HttpStatusCode = 503;
    public const string SafeErrorCode = "EBELGE_UBL_FEATURE_DISABLED";
    public const string SafeMessage = "E-belge UBL hazırlığı bu ortamda henüz etkinleştirilmedi.";

    public string HataKodu { get; } = SafeErrorCode;

    public EBelgeUblFeatureDisabledException()
        : base(SafeMessage, HttpStatusCode)
    {
    }
}

/// <summary>İlk dalganın dar kapsamı (yalnız e-Arşiv, SatisFaturasi, TRY, Kur=1, standart KDV, Adet birimi, iadesiz) dışında kalan belge/kanal/vergi/birim kombinasyonları için.</summary>
public sealed class EBelgeUblScopeUnsupportedException : BaseException
{
    public const int HttpStatusCode = 400;
    public const string SafeErrorCode = "EBELGE_UBL_SCOPE_UNSUPPORTED";

    public string HataKodu { get; } = SafeErrorCode;

    public EBelgeUblScopeUnsupportedException(string mesaj)
        : base(mesaj, HttpStatusCode)
    {
    }
}

/// <summary>Belge kapsam içinde olsa da (örn. SatisFaturasi + e-Arşiv) V2 snapshot için zorunlu otoriter alan (unvan/VKN, ad/soyad/TCKN, yapısal adres) eksikse.</summary>
public sealed class EBelgeUblAuthoritativeFieldMissingException : BaseException
{
    public const int HttpStatusCode = 400;
    public const string SafeErrorCode = "EBELGE_UBL_AUTHORITATIVE_FIELD_MISSING";

    public string HataKodu { get; } = SafeErrorCode;

    public EBelgeUblAuthoritativeFieldMissingException(string mesaj)
        : base(mesaj, HttpStatusCode)
    {
    }
}

/// <summary>Belge/satır toplamları merkezî mali doğrulayıcıya (SatisBelgesiTutarHesaplayici.DogrulaBelgeToplamlari) göre tutarsızsa.</summary>
public sealed class EBelgeUblMonetaryTotalMismatchException : BaseException
{
    public const int HttpStatusCode = 422;
    public const string SafeErrorCode = "EBELGE_UBL_MONETARY_TOTAL_MISMATCH";

    public string HataKodu { get; } = SafeErrorCode;

    public IReadOnlyList<SatisBelgesiTutarHesaplayici.BelgeToplamUyusmazligi> Uyusmazliklar { get; }

    public EBelgeUblMonetaryTotalMismatchException(IReadOnlyList<SatisBelgesiTutarHesaplayici.BelgeToplamUyusmazligi> uyusmazliklar)
        : base(BuildMesaj(uyusmazliklar), HttpStatusCode)
    {
        Uyusmazliklar = uyusmazliklar;
    }

    private static string BuildMesaj(IReadOnlyList<SatisBelgesiTutarHesaplayici.BelgeToplamUyusmazligi> uyusmazliklar)
    {
        var detay = string.Join(
            "; ",
            uyusmazliklar.Select(u => $"{u.Alan}: hesaplanan={u.HesaplananDeger}, mevcut={u.MevcutDeger}"));

        return $"Belge toplamları satır toplamlarıyla uyuşmuyor: {detay}";
    }
}
