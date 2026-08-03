using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>
/// E-belge UBL kesim öncesi kapısının tarih sınırı ihlal edildiğinde fırlatılır. Bkz.
/// docs/e-belge-ubl-pdf-eposta-renderer-hazirlik-raporu.md - "Kesin ürün kararı: devreye alma
/// tarihi": 14.09.2026 Türkiye yerel tarihinden önce hiçbir belge kesilemez. Bu kontrol yalnız
/// <see cref="EBelgeUblOptions.Enabled"/> açıkken çalışır (bkz. <c>EBelgeUblOptions</c>).
/// </summary>
public sealed class EBelgeInvoiceDateBeforeGoLiveException : BaseException
{
    public const int HttpStatusCode = 400;
    public const string SafeErrorCode = "EBELGE_INVOICE_DATE_BEFORE_GO_LIVE";

    public string HataKodu { get; } = SafeErrorCode;

    public EBelgeInvoiceDateBeforeGoLiveException(DateTime belgeTarihi, DateTime planlananKesimTarihiTrt, DateTime devreyeAlmaTarihiTrt)
        : base(
            $"E-belge işlemleri {devreyeAlmaTarihiTrt:dd.MM.yyyy} tarihinden önce canlı kullanıma alınamaz. " +
            $"(BelgeTarihi: {belgeTarihi:dd.MM.yyyy}, planlanan kesim tarihi (TRT): {planlananKesimTarihiTrt:dd.MM.yyyy})",
            HttpStatusCode)
    {
    }
}
