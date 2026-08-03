namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>
/// E-belge UBL hazırlığı için canlıya alma kapısını kontrol eden feature flag.
/// Bkz. docs/e-belge-ubl-pdf-eposta-renderer-hazirlik-raporu.md - "Kesin ürün kararı: devreye
/// alma tarihi". <see cref="Enabled"/> false iken <c>FaturaKesAsync</c> bu fazdan önceki
/// davranışını aynen sürdürür (14.09.2026 kapısı devre dışıdır); true olduğunda Türkiye yerel
/// tarihine göre 14.09.2026 öncesi kesim resmî numara verilmeden reddedilir.
/// </summary>
public sealed class EBelgeUblOptions
{
    public const string SectionName = "EBelgeUbl";

    public bool Enabled { get; set; }
}
