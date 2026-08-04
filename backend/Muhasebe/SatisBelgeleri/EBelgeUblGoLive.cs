namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>
/// Bkz. docs/e-belge-ubl-pdf-eposta-renderer-hazirlik-raporu.md - "Kesin ürün kararı: devreye
/// alma tarihi". Hem <c>SatisBelgesiService.EnsureCutoverTarihGecerli</c> hem
/// <see cref="IEBelgeUblPreCutValidator"/> AYNI sabiti kullanır — iki ayrı hardcoded tarih
/// zamanla farklılaşamaz.
/// </summary>
public static class EBelgeUblGoLive
{
    public static readonly DateTime Trt = new(2026, 9, 14, 0, 0, 0, DateTimeKind.Unspecified);
}
